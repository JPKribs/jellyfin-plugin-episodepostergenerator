using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Extensions;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    public class SoftwareFFmpegService : IFFmpegService
    {
        private const double DefaultDurationSeconds = 3600;
        private const double DefaultSeekStartPercent = 0.2;
        private const double DefaultSeekEndPercent = 0.8;

        private readonly ILogger<SoftwareFFmpegService> _logger;

        // SoftwareFFmpegService
        // Initializes the software FFmpeg service with a logger.
        public SoftwareFFmpegService(ILogger<SoftwareFFmpegService> logger)
        {
            _logger = logger;
        }

        // BuildFFmpegArgs
        // Constructs FFmpeg command-line arguments for software-based frame extraction with optional tone mapping.
        public string? BuildFFmpegArgs(
            string outputPath,
            EpisodeMetadata metadata,
            EncodingOptions encodingOptions,
            int? seekSeconds = null,
            bool skipToneMapping = false)
        {
            var video = metadata.VideoMetadata;
            if (video?.EpisodeFilePath == null)
            {
                _logger.LogError("Invalid video metadata in SoftwareFFmpegService");
                return null;
            }

            var inputPath = video.EpisodeFilePath;
            var durationSeconds = video.VideoLengthTicks / (double)TimeSpan.TicksPerSecond;
            if (durationSeconds <= 0) durationSeconds = DefaultDurationSeconds;

            var actualSeekSeconds = seekSeconds ?? Random.Shared.Next((int)(durationSeconds * DefaultSeekStartPercent), (int)(durationSeconds * DefaultSeekEndPercent));
            var isHDR = video.VideoHdrType.IsHDR();

            var args = $"-y -ss {actualSeekSeconds} -i \"{inputPath}\"";

            var is10Bit = video.VideoColorBits >= 10;
            var shouldApplyToneMapping = !skipToneMapping && encodingOptions.EnableTonemapping && (isHDR || is10Bit);

            // Tone mapping branch: applies zscale/tonemap filters for HDR or 10-bit content
            if (shouldApplyToneMapping)
            {
                try
                {
                    _logger.LogInformation("Requesting tone mapping filter for VideoRangeType={RangeType}, 10-bit={Is10Bit}, HDR={IsHDR}",
                        video.VideoHdrType, is10Bit, isHDR);

                    var toneMapFilter = ToneMapFilterService.GetToneMapFilter(
                        encodingOptions,
                        video,
                        HardwareAccelerationType.none,
                        _logger
                    );

                    if (!string.IsNullOrEmpty(toneMapFilter))
                    {
                        args += $" -vf \"{toneMapFilter}\"";
                        _logger.LogInformation("Applied software tone mapping filter: {Filter}", toneMapFilter);
                    }
                    else
                    {
                        _logger.LogWarning("Software tone mapping filter was empty for VideoRangeType={RangeType}", video.VideoHdrType);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Software tone mapping filter generation failed; proceeding without tone mapping");
                }
            }
            // Skip branch: tone mapping explicitly disabled for HDR/10-bit content
            else if (skipToneMapping && (isHDR || is10Bit))
            {
                _logger.LogDebug("Tone mapping skipped for HDR/10-bit content as requested");
            }
            // SDR branch: no tone mapping needed for standard content
            else if (!isHDR && !is10Bit)
            {
                _logger.LogDebug("No tone mapping needed for SDR/8-bit content");
            }

            args += $" -frames:v 1 -q:v 2 \"{outputPath}\"";

            return args;
        }

        // CanProcess
        // Returns true because software decoding can handle any video format.
        public bool CanProcess(EpisodeMetadata metadata, EncodingOptions encodingOptions)
        {
            return true;
        }

        // ExtractSceneAsync
        // Returns null because this service only builds arguments; execution is handled externally.
        public Task<string?> ExtractSceneAsync(
            string outputPath,
            EpisodeMetadata metadata,
            EncodingOptions encodingOptions,
            CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("SoftwareFFmpegService does not execute FFmpeg directly. Use BuildFFmpegArgs + FFmpegService.RunFFmpegAsync.");
            return Task.FromResult<string?>(null);
        }
    }
}
