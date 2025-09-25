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
    /// Handles video frame extraction using FFmpeg with hardware/software fallback and QA checks.
    /// </summary>
    public class FFmpegService
    {
        /// <summary>Logger for this service</summary>
        private readonly ILogger<FFmpegService> _logger;

        /// <summary>Hardware FFmpeg processing service</summary>
        private readonly HardwareFFmpegService _hardwareService;

        /// <summary>Software FFmpeg processing service</summary>
        private readonly SoftwareFFmpegService _softwareService;

        /// <summary>Server configuration manager</summary>
        private readonly IServerConfigurationManager _configurationManager;

        /// <summary>Quality assurance service for frame brightness</summary>
        private readonly QualityAssuranceService _qaService;

        /// <summary>Maximum number of retries for extracting a non-dark frame</summary>
        private const int MaxRetries = 50;

        // MARK: Constructor
        public FFmpegService(
            ILogger<FFmpegService> logger,
            IServerConfigurationManager configurationManager,
            HardwareFFmpegService hardwareService,
            SoftwareFFmpegService softwareService,
            ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _configurationManager = configurationManager;
            _hardwareService = hardwareService;
            _softwareService = softwareService;

            _qaService = new QualityAssuranceService(
                loggerFactory.CreateLogger<QualityAssuranceService>(), 
                darkThreshold: 0.05);
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

            _logger.LogDebug("Selected {ServiceType} for scene extraction", service.GetType().Name);

            string? bestFramePath = null;
            double bestBrightness = 0;

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                // Build output path and FFmpeg args
                var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.{config.PosterFileType.ToString().ToLowerInvariant()}");
                string? args = service.BuildFFmpegArgs(outputPath, metadata, encodingOptions);

                if (string.IsNullOrWhiteSpace(args))
                {
                    _logger.LogError("FFmpeg args could not be built, aborting extraction");
                    return null;
                }

                var ffmpegPath = string.IsNullOrWhiteSpace(encodingOptions.EncoderAppPath) ? "ffmpeg" : encodingOptions.EncoderAppPath;
                bool success = await RunFFmpegAsync(ffmpegPath, args, outputPath, cancellationToken).ConfigureAwait(false);

                // Hardware -> software fallback
                if (!success && service == _hardwareService)
                {
                    _logger.LogWarning("Hardware extraction failed, falling back to software");
                    service = _softwareService;
                    args = service.BuildFFmpegArgs(outputPath, metadata, encodingOptions) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(args)) return null;

                    success = await RunFFmpegAsync(ffmpegPath, args, outputPath, cancellationToken).ConfigureAwait(false);
                }

                if (!success) continue;

                // QA brightness check
                if (_qaService.IsFrameBrightEnough(outputPath))
                {
                    _logger.LogInformation("Frame passed QA on attempt {Attempt}", attempt + 1);
                    return outputPath;
                }

                double brightness = GetFrameBrightness(outputPath);
                if (brightness > bestBrightness)
                {
                    bestBrightness = brightness;
                    bestFramePath = outputPath;
                }
                else
                {
                    File.Delete(outputPath);
                }
            }

            _logger.LogWarning("QA failed for all attempts; returning brightest frame found");
            return bestFramePath;
        }

        // MARK: SelectFFmpegService
        private IFFmpegService SelectFFmpegService(EpisodeMetadata metadata, EncodingOptions encodingOptions)
        {
            var hdr = metadata.VideoMetadata.VideoHdrType;
            if (hdr == VideoRangeType.Unknown) return _softwareService;

            return _hardwareService.CanProcess(metadata, encodingOptions) ? _hardwareService : _softwareService;
        }

        // MARK: RunFFmpegAsync
        private async Task<bool> RunFFmpegAsync(string ffmpegPath, string args, string outputPath, CancellationToken cancellationToken)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                string stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                if (process.ExitCode != 0 || !File.Exists(outputPath))
                {
                    _logger.LogError("FFmpeg failed. ExitCode={ExitCode}, stderr={Stderr}", process.ExitCode, stderr);
                    return false;
                }

                _logger.LogInformation("Frame extracted successfully: {OutputPath}", outputPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FFmpeg execution failed");
                return false;
            }
        }

        // MARK: GetFrameBrightness
        private double GetFrameBrightness(string filePath)
        {
            try
            {
                using var input = File.OpenRead(filePath);
                using var bitmap = SKBitmap.Decode(input);
                if (bitmap == null) return 0;

                double totalBrightness = 0;
                int pixelCount = 0;

                for (int y = 0; y < bitmap.Height; y += 2)
                {
                    for (int x = 0; x < bitmap.Width; x += 2)
                    {
                        var color = bitmap.GetPixel(x, y);
                        double brightness = (0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue) / 255.0;
                        totalBrightness += brightness;
                        pixelCount++;
                    }
                }

                return totalBrightness / pixelCount;
            }
            catch
            {
                return 0;
            }
        }
    }
}