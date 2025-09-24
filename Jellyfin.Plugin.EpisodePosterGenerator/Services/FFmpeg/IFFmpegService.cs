using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.FFmpeg
{
    public interface IFFmpegService : IDisposable
    {
        Task<string?> ExtractFrameAsync(string videoPath, TimeSpan timestamp, string outputPath, CancellationToken cancellationToken = default);
        Task<List<BlackInterval>> DetectBlackScenesAsync(
            string videoPath,
            TimeSpan totalDuration,
            double pixelThreshold = 0.1,
            double durationThreshold = 0.1,
            CancellationToken cancellationToken = default);
        Task<TimeSpan?> GetVideoDurationAsync(string videoPath, CancellationToken cancellationToken = default);
    }
}