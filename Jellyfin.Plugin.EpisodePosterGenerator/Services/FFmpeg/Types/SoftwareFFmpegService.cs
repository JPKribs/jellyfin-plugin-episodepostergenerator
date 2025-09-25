using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Software fallback FFmpeg service; builds FFmpeg args for frame extraction and optionally tone mapping.
    /// </summary>
    public class SoftwareFFmpegService : IFFmpegService
    {
        /// <summary>Logger for this service</summary>
        private readonly ILogger<SoftwareFFmpegService> _logger;

        // MARK: Constructor
        public SoftwareFFmpegService(ILogger<SoftwareFFmpegService> logger)
        {
            _logger = logger;
        }

        // MARK: BuildFFmpegArgs
        public string? BuildFFmpegArgs(string outputPath, EpisodeMetadata metadata, EncodingOptions encodingOptions)
        {
            var video = metadata.VideoMetadata;
            if (video?.EpisodeFilePath == null)
            {
                _logger.LogError("Invalid video metadata in SoftwareFFmpegService");
                return null;
            }

            var input = video.EpisodeFilePath;
            var seekSeconds = 10; // fallback default timestamp

            string toneMapFilter = string.Empty;

            try
            {
                // Only apply tone mapping if HDR and enabled
                if (encodingOptions.EnableTonemapping && video.VideoHdrType != VideoRangeType.SDR)
                {
                    toneMapFilter = ToneMapFilterService.GetToneMapFilter(
                        encodingOptions,
                        video,
                        HardwareAccelerationType.none // software path
                    ) ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tone mapping filter generation failed; falling back to simple extraction");
                toneMapFilter = string.Empty;
            }

            // Build FFmpeg arguments
            var args = $"-y -ss {seekSeconds} -i \"{input}\"";

            if (!string.IsNullOrWhiteSpace(toneMapFilter))
            {
                args += $" {toneMapFilter}";
            }
            else
            {
                _logger.LogDebug("No tone mapping applied for SoftwareFFmpegService");
            }

            args += $" -frames:v 1 -q:v 2 \"{outputPath}\"";

            return args;
        }

        // MARK: CanProcess
        public bool CanProcess(EpisodeMetadata metadata, EncodingOptions encodingOptions)
        {
            // Software decoding can handle everything
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