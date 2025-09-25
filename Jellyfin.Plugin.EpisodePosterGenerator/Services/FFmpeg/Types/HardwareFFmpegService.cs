using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Configuration;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Extensions;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// FFmpeg service for hardware-accelerated frame extraction and optional HDR tone mapping.
    /// </summary>
    public class HardwareFFmpegService : IFFmpegService
    {
        /// <summary>Logger for this service</summary>
        private readonly ILogger<HardwareFFmpegService> _logger;

        // MARK: Constructor
        public HardwareFFmpegService(ILogger<HardwareFFmpegService> logger)
        {
            _logger = logger;
        }

        // MARK: BuildFFmpegArgs
        public string? BuildFFmpegArgs(string outputPath, EpisodeMetadata metadata, EncodingOptions encodingOptions)
        {
            var video = metadata.VideoMetadata;
            if (video?.EpisodeFilePath == null) return null;

            var inputPath = video.EpisodeFilePath;
            var durationSeconds = video.VideoLengthTicks / (double)TimeSpan.TicksPerSecond;
            if (durationSeconds <= 0) durationSeconds = 60;

            // Pick a random frame in the 20%-80% range of the video
            var seekSeconds = new Random().Next((int)(durationSeconds * 0.2), (int)(durationSeconds * 0.8));

            // Hardware acceleration arguments
            var hwAccelArgs = encodingOptions.HardwareAccelerationType.ToFFmpegArg();

            string toneMapFilter = string.Empty;

            // Apply tone mapping if HDR and enabled
            if (encodingOptions.EnableTonemapping && video.VideoHdrType != VideoRangeType.SDR)
            {
                toneMapFilter = ToneMapFilterService.GetToneMapFilter(
                    encodingOptions,
                    video,
                    encodingOptions.HardwareAccelerationType
                ) ?? string.Empty;
            }

            // Build FFmpeg command line
            var args = $"-y {hwAccelArgs} -ss {seekSeconds} -i \"{inputPath}\"";
            if (!string.IsNullOrWhiteSpace(toneMapFilter))
                args += $" {toneMapFilter}";
            args += $" -frames:v 1 -q:v 2 \"{outputPath}\"";

            return args;
        }

        // MARK: CanProcess
        public bool CanProcess(EpisodeMetadata metadata, EncodingOptions encodingOptions)
        {
            var video = metadata.VideoMetadata;
            var hdr = video.VideoHdrType;

            if (hdr == VideoRangeType.Unknown) return false;

            // HDR without proper tonemapping support cannot be processed
            if (hdr != VideoRangeType.SDR &&
                encodingOptions.EnableTonemapping &&
                !encodingOptions.EnableVideoToolboxTonemapping &&
                !encodingOptions.EnableVppTonemapping)
                return false;

            // Check if codec is supported for hardware decoding
            return encodingOptions.HardwareDecodingCodecs
                .Any(c => string.Equals(c, video.VideoCodec.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        // MARK: ExtractSceneAsync
        public Task<string?> ExtractSceneAsync(
            string outputPath,
            EpisodeMetadata metadata,
            EncodingOptions encodingOptions,
            CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("HardwareFFmpegService does not execute FFmpeg directly. Use BuildFFmpegArgs + FFmpegService.RunFFmpegAsync.");
            return Task.FromResult<string?>(null);
        }
    }
}