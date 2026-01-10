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
        private readonly ILogger<SoftwareFFmpegService> _logger;

        // MARK: Constructor
        public SoftwareFFmpegService(ILogger<SoftwareFFmpegService> logger)
        {
            _logger = logger;
        }

        // MARK: BuildFFmpegArgs
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
            if (durationSeconds <= 0) durationSeconds = 3600;

            var actualSeekSeconds = seekSeconds ?? new Random().Next((int)(durationSeconds * 0.2), (int)(durationSeconds * 0.8));
            var isHDR = video.VideoHdrType.IsHDR();

            var args = $"-y -ss {actualSeekSeconds} -i \"{inputPath}\"";

            // Check if tone mapping is actually needed (handles both explicit HDR types and 10-bit content)
            var is10Bit = video.VideoColorBits >= 10;
            var shouldApplyToneMapping = !skipToneMapping && encodingOptions.EnableTonemapping && (isHDR || is10Bit);

            if (shouldApplyToneMapping)
            {
                try
                {
                    _logger.LogInformation("Requesting tone mapping filter for VideoRangeType={RangeType}, 10-bit={Is10Bit}, HDR={IsHDR}",
                        video.VideoHdrType, is10Bit, isHDR);

                    // Use ToneMapFilterService for software path
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
            else if (skipToneMapping && (isHDR || is10Bit))
            {
                _logger.LogDebug("Tone mapping skipped for HDR/10-bit content as requested");
            }
            else if (!isHDR && !is10Bit)
            {
                _logger.LogDebug("No tone mapping needed for SDR/8-bit content");
            }

            args += $" -frames:v 1 -q:v 2 \"{outputPath}\"";

            return args;
        }

        // MARK: CanProcess
        public bool CanProcess(EpisodeMetadata metadata, EncodingOptions encodingOptions)
        {
            return true;
        }

        // MARK: ExtractSceneAsync
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