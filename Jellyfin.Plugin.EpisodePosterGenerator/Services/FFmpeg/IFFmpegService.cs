using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Model.Configuration;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    // IFFmpegService
    // Defines the contract for FFmpeg-based scene extraction services.
    public interface IFFmpegService
    {
        // ExtractSceneAsync
        // Extracts a scene frame from video at the specified timestamp.
        Task<string?> ExtractSceneAsync(
            string outputPath,
            EpisodeMetadata metadata,
            EncodingOptions encodingOptions,
            CancellationToken cancellationToken = default);

        // CanProcess
        // Checks if this service can handle the given video metadata.
        bool CanProcess(EpisodeMetadata metadata, EncodingOptions encodingOptions);

        // BuildFFmpegArgs
        // Builds FFmpeg command arguments without executing.
        string? BuildFFmpegArgs(
                string outputPath,
                EpisodeMetadata metadata,
                EncodingOptions encodingOptions,
                int? seekSeconds = null,
                bool skipToneMapping = false
            );
        }
}
