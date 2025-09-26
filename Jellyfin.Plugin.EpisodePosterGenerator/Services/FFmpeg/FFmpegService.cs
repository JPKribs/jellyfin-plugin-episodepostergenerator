using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Extensions;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    public class FFmpegService
    {
        private readonly ILogger<FFmpegService> _logger;
        private readonly HardwareFFmpegService _hardwareService;
        private readonly SoftwareFFmpegService _softwareService;
        private readonly BrightnessService _brightnessService;
        private readonly IServerConfigurationManager _configurationManager;

        private const int MaxRetries = 50;
        private const double BrightnessThreshold = 0.08;

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
            var videoDurationSeconds = metadata.VideoMetadata.VideoLengthTicks / (double)TimeSpan.TicksPerSecond;
            if (videoDurationSeconds <= 0) videoDurationSeconds = 3600;

            var isHDR = metadata.VideoMetadata.VideoHdrType.IsHDR();
            var brightnessThreshold = isHDR ? BrightnessThreshold * 0.5 : BrightnessThreshold;

            string? bestFramePath = null;

            // Strategy 1: Hardware acceleration with tone mapping (if HDR and HWA enabled)
            if (config.EnableHWA && encodingOptions.EnableTonemapping && isHDR)
            {
                _logger.LogInformation("Attempting Strategy 1: Hardware acceleration with tone mapping");
                bestFramePath = await TryExtractionStrategy(
                    metadata, config, encodingOptions, videoDurationSeconds, brightnessThreshold,
                    useHardware: true, useToneMapping: true, cancellationToken).ConfigureAwait(false);
                
                if (bestFramePath != null)
                {
                    _logger.LogInformation("Strategy 1 succeeded");
                    return await ApplyFinalProcessing(bestFramePath, config, isHDR).ConfigureAwait(false);
                }
                _logger.LogWarning("Strategy 1 failed, trying Strategy 2");
            }

            // Strategy 2: Hardware acceleration without tone mapping (if HWA enabled)
            if (config.EnableHWA)
            {
                _logger.LogInformation("Attempting Strategy 2: Hardware acceleration without tone mapping");
                bestFramePath = await TryExtractionStrategy(
                    metadata, config, encodingOptions, videoDurationSeconds, brightnessThreshold,
                    useHardware: true, useToneMapping: false, cancellationToken).ConfigureAwait(false);
                
                if (bestFramePath != null)
                {
                    _logger.LogInformation("Strategy 2 succeeded");
                    return await ApplyFinalProcessing(bestFramePath, config, isHDR).ConfigureAwait(false);
                }
                _logger.LogWarning("Strategy 2 failed, trying Strategy 3");
            }
            else
            {
                _logger.LogInformation("Hardware acceleration disabled, skipping to Strategy 3");
            }

            // Strategy 3: Software with tone mapping (if HDR and enabled)
            if (encodingOptions.EnableTonemapping && isHDR)
            {
                _logger.LogInformation("Attempting Strategy 3: Software with tone mapping");
                bestFramePath = await TryExtractionStrategy(
                    metadata, config, encodingOptions, videoDurationSeconds, brightnessThreshold,
                    useHardware: false, useToneMapping: true, cancellationToken).ConfigureAwait(false);
                
                if (bestFramePath != null)
                {
                    _logger.LogInformation("Strategy 3 succeeded");
                    return await ApplyFinalProcessing(bestFramePath, config, isHDR).ConfigureAwait(false);
                }
                _logger.LogWarning("Strategy 3 failed, trying Strategy 4");
            }

            // Strategy 4: Software without tone mapping (final fallback)
            _logger.LogInformation("Attempting Strategy 4: Software without tone mapping (final fallback)");
            bestFramePath = await TryExtractionStrategy(
                metadata, config, encodingOptions, videoDurationSeconds, brightnessThreshold,
                useHardware: false, useToneMapping: false, cancellationToken).ConfigureAwait(false);
            
            if (bestFramePath != null)
            {
                _logger.LogInformation("Strategy 4 succeeded");
                return await ApplyFinalProcessing(bestFramePath, config, isHDR).ConfigureAwait(false);
            }

            _logger.LogError("All extraction strategies failed");
            return null;
        }

        // MARK: TryExtractionStrategy
        private async Task<string?> TryExtractionStrategy(
            EpisodeMetadata metadata,
            PluginConfiguration config,
            EncodingOptions encodingOptions,
            double videoDurationSeconds,
            double brightnessThreshold,
            bool useHardware,
            bool useToneMapping,
            CancellationToken cancellationToken)
        {
            IFFmpegService service = useHardware ? _hardwareService : _softwareService;
            var strategyName = $"{(useHardware ? "Hardware" : "Software")}{(useToneMapping ? " + ToneMapping" : "")}";

            // Check if strategy is viable
            if (useHardware && !service.CanProcess(metadata, encodingOptions))
            {
                _logger.LogWarning("Hardware service cannot process this content, skipping {Strategy}", strategyName);
                return null;
            }

            string? bestFramePath = null;
            double bestBrightness = 0.0;
            int maxAttemptsForStrategy = useHardware ? 15 : MaxRetries;

            for (int attempt = 0; attempt < maxAttemptsForStrategy; attempt++)
            {
                var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
                var seekTime = GenerateSeekTime(videoDurationSeconds, attempt, config);

                string? args = service.BuildFFmpegArgs(outputPath, metadata, encodingOptions, seekTime, !useToneMapping);
                if (string.IsNullOrWhiteSpace(args))
                {
                    _logger.LogError("Could not build FFmpeg args for {Strategy}", strategyName);
                    break;
                }

                var ffmpegPath = string.IsNullOrWhiteSpace(encodingOptions.EncoderAppPath) ? "ffmpeg" : encodingOptions.EncoderAppPath;
                _logger.LogDebug("{Strategy} Command: {Path} {Args}", strategyName, ffmpegPath, args);

                bool success = await RunFFmpegAsync(ffmpegPath, args, outputPath, cancellationToken).ConfigureAwait(false);

                if (!success)
                {
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                    
                    // For hardware strategies, fail fast on first few attempts
                    if (useHardware && attempt < 3)
                    {
                        _logger.LogWarning("{Strategy} failed on attempt {Attempt}, strategy likely not working", strategyName, attempt + 1);
                        break;
                    }
                    continue;
                }

                var brightnessOk = _brightnessService.IsFrameBrightEnough(outputPath, brightnessThreshold);
                var actualBrightness = GetFrameBrightness(outputPath);

                _logger.LogDebug("{Strategy} Attempt {Attempt}: Brightness {Brightness:F3}, Acceptable: {Acceptable}",
                    strategyName, attempt + 1, actualBrightness, brightnessOk);

                if (brightnessOk)
                {
                    if (bestFramePath != null && File.Exists(bestFramePath))
                        File.Delete(bestFramePath);
                    return outputPath;
                }

                if (actualBrightness > bestBrightness)
                {
                    if (bestFramePath != null && File.Exists(bestFramePath))
                        File.Delete(bestFramePath);
                    bestFramePath = outputPath;
                    bestBrightness = actualBrightness;
                }
                else
                {
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                }

                // Early exit for reasonable HDR frames
                if (metadata.VideoMetadata.VideoHdrType.IsHDR() && actualBrightness > 0.03 && attempt > 5)
                {
                    _logger.LogInformation("{Strategy} found reasonably bright HDR frame after {Attempts} attempts", 
                        strategyName, attempt + 1);
                    break;
                }
            }

            if (bestFramePath != null)
            {
                _logger.LogInformation("{Strategy} using best available frame (Brightness: {Brightness:F3})", 
                    strategyName, bestBrightness);
                return bestFramePath;
            }

            _logger.LogWarning("{Strategy} failed to extract any usable frame", strategyName);
            return null;
        }

        // MARK: ApplyFinalProcessing
        private Task<string> ApplyFinalProcessing(string framePath, PluginConfiguration config, bool isHDR)
        {
            if (isHDR && config.BrightenHDR > 0)
            {
                _logger.LogDebug("Applying HDR brightness adjustment: {Adjustment}", config.BrightenHDR);
                _brightnessService.Brighten(framePath, config.BrightenHDR, config.PosterFileType);
            }

            return Task.FromResult(framePath);
        }

        // MARK: GenerateSeekTime
        private int GenerateSeekTime(double videoDurationSeconds, int attempt, PluginConfiguration config)
        {
            var random = new Random();
            var startPercent = config.ExtractWindowStart / 100.0;
            var endPercent = config.ExtractWindowEnd / 100.0;
            
            if (startPercent >= endPercent)
            {
                _logger.LogWarning("Invalid extraction window: start {Start}% >= end {End}%, using default 20%-80%", 
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

                process.Start();
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    _logger.LogDebug("FFmpeg failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                    return false;
                }

                if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                {
                    _logger.LogDebug("FFmpeg completed but output file is missing or empty: {OutputPath}", outputPath);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error running FFmpeg");
                return false;
            }
        }
    }
}