using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Model.Configuration;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Interface for FFmpeg-based scene extraction services
    /// </summary>
    public interface IFFmpegService
    {
        /// <summary>
        /// Extract a scene frame from video at specified timestamp
        /// </summary>
        Task<string?> ExtractSceneAsync(
            string outputPath,
            EpisodeMetadata metadata,
            EncodingOptions encodingOptions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if this service can handle the given video metadata
        /// </summary>
        bool CanProcess(EpisodeMetadata metadata, EncodingOptions encodingOptions);

        /// <summary>
        /// Optionally build FFmpeg command arguments without executing.
        /// Useful for hardware services that want the controller to run FFmpeg.
        /// </summary>
        string? BuildFFmpegArgs(string outputPath, EpisodeMetadata metadata, EncodingOptions encodingOptions);
    }
}