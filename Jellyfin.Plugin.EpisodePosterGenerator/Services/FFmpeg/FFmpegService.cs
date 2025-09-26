using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Controller.Configuration;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;
using SkiaSharp;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Handles video frame extraction using FFmpeg with hardware/software fallback,
    /// brightness retry logic, seek-time randomization, and HDR brightening.
    /// </summary>
    public class FFmpegService
    {
        private readonly ILogger<FFmpegService> _logger;
        private readonly HardwareFFmpegService _hardwareService;
        private readonly SoftwareFFmpegService _softwareService;
        private readonly BrightnessService _brightnessService;
        private readonly IServerConfigurationManager _configurationManager;

        private const int MaxRetries = 50;
        private const double BrightnessThreshold = 0.08; // 8% threshold

        public FFmpegService(
            ILogger<FFmpegService> logger,
            IServerConfigurationManager configurationManager,
            HardwareFFmpegService hardwareService,
            SoftwareFFmpegService softwareService,
            BrightnessService brightnessService)
        {
            _logger = logger;
            _configurationManager = configurationManager;
            _hardwareService = hardwareService;
            _softwareService = softwareService;
            _brightnessService = brightnessService;
        }

        // MARK: ExtractSceneAsync
        public async Task<string?> ExtractSceneAsync(
            EpisodeMetadata metadata,
            PluginConfiguration config,
            CancellationToken cancellationToken = default)
        {
            if (metadata?.VideoMetadata == null)
            {
                _logger.LogError("Invalid metadata provided to FFmpegService");
                return null;
            }

            var encodingOptions = _configurationManager.GetEncodingOptions();
            var service = SelectFFmpegService(metadata, encodingOptions);

            bool toneMappingWasEnabled = encodingOptions.EnableTonemapping && metadata.VideoMetadata.VideoHdrType != VideoRangeType.SDR;

            var videoDurationSeconds = metadata.VideoMetadata.VideoLengthTicks / (double)TimeSpan.TicksPerSecond;
            if (videoDurationSeconds <= 0) videoDurationSeconds = 3600;

            _logger.LogDebug("Selected {ServiceType} for scene extraction", service.GetType().Name);

            string? bestFramePath = null;
            double bestBrightness = 0.0;
            var isHDR = metadata.VideoMetadata.VideoHdrType.IsHDR();
            var brightnessThreshold = isHDR ? BrightnessThreshold * 0.5 : BrightnessThreshold;

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var outputPath = Path.Combine(
                    Path.GetTempPath(),
                    $"{Guid.NewGuid()}.png");

                int seekTime = GenerateSeekTime(videoDurationSeconds, attempt, config);
                string? args = service.BuildFFmpegArgs(outputPath, metadata, encodingOptions, seekTime);
                if (string.IsNullOrWhiteSpace(args))
                {
                    _logger.LogError("FFmpeg args could not be built, aborting extraction");
                    return null;
                }

                var ffmpegPath = string.IsNullOrWhiteSpace(encodingOptions.EncoderAppPath)
                    ? "ffmpeg"
                    : encodingOptions.EncoderAppPath;

                bool success = await RunFFmpegAsync(ffmpegPath, args, outputPath, cancellationToken).ConfigureAwait(false);

                // Hardware -> software fallback
                if (!success && service == _hardwareService)
                {
                    _logger.LogWarning("Hardware extraction failed on attempt {Attempt}, falling back to software", attempt + 1);
                    service = _softwareService;

                    var fallbackOptions = toneMappingWasEnabled 
                        ? new EncodingOptions
                        {
                            EnableTonemapping = false,
                            EncoderAppPath = encodingOptions.EncoderAppPath,
                        }
                        : encodingOptions;

                    if (toneMappingWasEnabled) fallbackOptions.EnableTonemapping = false;

                    args = service.BuildFFmpegArgs(outputPath, metadata, fallbackOptions, seekTime);
                    if (string.IsNullOrWhiteSpace(args)) continue;

                    success = await RunFFmpegAsync(ffmpegPath, args, outputPath, cancellationToken).ConfigureAwait(false);
                }

                if (!success)
                {
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                    continue;
                }

                var brightnessOk = _brightnessService.IsFrameBrightEnough(outputPath, brightnessThreshold);
                var actualBrightness = GetFrameBrightness(outputPath);

                _logger.LogDebug("Attempt {Attempt}: Brightness {Brightness:F3}, Threshold {Threshold:F3}, Acceptable: {Acceptable}",
                    attempt + 1, actualBrightness, brightnessThreshold, brightnessOk);

                if (brightnessOk)
                {
                    if (isHDR && config.BrightenHDR > 0)
                        _brightnessService.Brighten(outputPath, config.BrightenHDR, config.PosterFileType);

                    if (bestFramePath != null && bestFramePath != outputPath && File.Exists(bestFramePath))
                        File.Delete(bestFramePath);

                    return outputPath;
                }

                if (actualBrightness > bestBrightness)
                {
                    if (bestFramePath != null && File.Exists(bestFramePath))
                        File.Delete(bestFramePath);

                    bestFramePath = outputPath;
                    bestBrightness = actualBrightness;

                    _logger.LogDebug("New best frame found with brightness {Brightness:F3}", actualBrightness);
                }
                else
                {
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                }

                // Early exit for HDR
                if (isHDR && actualBrightness > 0.03 && attempt > 10)
                {
                    _logger.LogInformation("Found reasonably bright HDR frame after {Attempts} attempts (Brightness: {Brightness:F3})",
                        attempt + 1, actualBrightness);
                    break;
                }
            }

            if (bestFramePath != null)
            {
                _logger.LogWarning("Using best available frame after {MaxRetries} attempts (Brightness: {Brightness:F3})",
                    MaxRetries, bestBrightness);

                if (isHDR && config.BrightenHDR > 0)
                    _brightnessService.Brighten(bestFramePath, config.BrightenHDR, config.PosterFileType);

                return bestFramePath;
            }

            _logger.LogError("Failed to extract any usable frame after {MaxRetries} attempts", MaxRetries);
            return null;
        }

        // MARK: GenerateSeekTime
        private int GenerateSeekTime(double videoDurationSeconds, int attempt, PluginConfiguration config)
        {
            var random = new Random();
            
            // Convert percentages to decimal (20% = 0.2)
            var startPercent = config.ExtractWindowStart / 100.0;
            var endPercent = config.ExtractWindowEnd / 100.0;
            
            // Ensure valid range
            if (startPercent >= endPercent)
            {
                _logger.LogWarning("Invalid extraction window: start {Start}% >= end {End}%, using default values of 20% & 80%.", 
                    config.ExtractWindowStart, config.ExtractWindowEnd);
                startPercent = 0.2;
                endPercent = 0.8;
            }
            
            var startTime = videoDurationSeconds * startPercent;
            var endTime = videoDurationSeconds * endPercent;
            return (int)(random.NextDouble() * (endTime - startTime) + startTime);
        }

        // MARK: GetFrameBrightness
        private double GetFrameBrightness(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var bitmap = SKBitmap.Decode(stream);
                if (bitmap == null) return 0.0;

                double totalBrightness = 0;
                int sampleCount = 0;
                int stepSize = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 100);

                for (int y = 0; y < bitmap.Height; y += stepSize)
                {
                    for (int x = 0; x < bitmap.Width; x += stepSize)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        var brightness = (0.2126 * pixel.Red + 0.7152 * pixel.Green + 0.0722 * pixel.Blue) / 255.0;
                        totalBrightness += brightness;
                        sampleCount++;
                    }
                }

                return sampleCount > 0 ? totalBrightness / sampleCount : 0.0;
            }
            catch
            {
                return 0.0;
            }
        }

        // MARK: SelectFFmpegService
        private IFFmpegService SelectFFmpegService(EpisodeMetadata metadata, EncodingOptions encodingOptions)
        {
            var hdr = metadata.VideoMetadata.VideoHdrType;
            if (hdr == VideoRangeType.Unknown) return _softwareService;

            return _hardwareService.CanProcess(metadata, encodingOptions)
                ? _hardwareService
                : _softwareService;
        }

        // MARK: RunFFmpegAsync
        private async Task<bool> RunFFmpegAsync(string ffmpegPath, string arguments, string outputPath, CancellationToken cancellationToken)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                };

                _logger.LogDebug("Running FFmpeg: {Path} {Args}", ffmpegPath, arguments);

                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                var output = await outputTask.ConfigureAwait(false);
                var error = await errorTask.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    _logger.LogWarning("FFmpeg failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                    return false;
                }

                if (!File.Exists(outputPath))
                {
                    _logger.LogWarning("FFmpeg completed but output file does not exist: {OutputPath}", outputPath);
                    return false;
                }

                var fileInfo = new FileInfo(outputPath);
                if (fileInfo.Length == 0)
                {
                    _logger.LogWarning("FFmpeg created empty output file: {OutputPath}", outputPath);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running FFmpeg");
                return false;
            }
        }
    }
}