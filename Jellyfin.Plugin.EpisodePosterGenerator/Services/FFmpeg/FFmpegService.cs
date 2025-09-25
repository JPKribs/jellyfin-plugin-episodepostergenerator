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
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Handles video frame extraction using FFmpeg with hardware/software fallback.
    /// </summary>
    public class FFmpegService
    {
        private readonly ILogger<FFmpegService> _logger;
        private readonly HardwareFFmpegService _hardwareService;
        private readonly SoftwareFFmpegService _softwareService;
        private readonly IServerConfigurationManager _configurationManager;

        private const int MaxRetries = 50;

        // MARK: Constructor
        public FFmpegService(
            ILogger<FFmpegService> logger,
            IServerConfigurationManager configurationManager,
            HardwareFFmpegService hardwareService,
            SoftwareFFmpegService softwareService)
        {
            _logger = logger;
            _configurationManager = configurationManager;
            _hardwareService = hardwareService;
            _softwareService = softwareService;
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

            var encodingOptions = _configurationManager.GetConfiguration<EncodingOptions>("encoding");
            var service = SelectFFmpegService(metadata, encodingOptions);

            _logger.LogDebug("Selected {ServiceType} for scene extraction", service.GetType().Name);

            string? bestFramePath = null;

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                var outputPath = Path.Combine(
                    Path.GetTempPath(),
                    $"{Guid.NewGuid()}.{config.PosterFileType.ToString().ToLowerInvariant()}");

                string? args = service.BuildFFmpegArgs(outputPath, metadata, encodingOptions);
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
                    _logger.LogWarning("Hardware extraction failed, falling back to software");
                    service = _softwareService;
                    args = service.BuildFFmpegArgs(outputPath, metadata, encodingOptions) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(args)) return null;

                    success = await RunFFmpegAsync(ffmpegPath, args, outputPath, cancellationToken).ConfigureAwait(false);
                }

                if (!success) continue;

                _logger.LogInformation("Frame extracted successfully on attempt {Attempt}: {OutputPath}", 
                    attempt + 1, outputPath);
                    
                bestFramePath = outputPath;
                break;
            }

            if (bestFramePath == null)
            {
                _logger.LogError("Failed to extract frame after {MaxRetries} attempts", MaxRetries);
            }

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

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FFmpeg execution failed");
                return false;
            }
        }
    }
}