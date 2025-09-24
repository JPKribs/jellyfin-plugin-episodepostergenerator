using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.FFmpeg;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Managers
{
    public sealed class FFmpegManager : IDisposable
    {
        private readonly SWFFmpegService _swService;
        private readonly HWAFFmpegService _hwaService;
        private bool _disposed;

        public FFmpegManager(ILogger<FFmpegManager> logger)
        {
            _swService = new SWFFmpegService();
            _hwaService = new HWAFFmpegService();
        }

        private IFFmpegService SelectService(bool useHardware)
        {
            return useHardware ? _hwaService : _swService;
        }

        public async Task<string?> ExtractFrameAsync(bool useHardware, string videoPath, TimeSpan timestamp, string outputPath, CancellationToken cancellationToken = default)
        {
            var service = SelectService(useHardware);
            return await service.ExtractFrameAsync(videoPath, timestamp, outputPath, cancellationToken).ConfigureAwait(false);
        }

        public async Task<List<BlackInterval>> DetectBlackScenesAsync(bool useHardware, string videoPath, TimeSpan totalDuration, double pixelThreshold = 0.1, double durationThreshold = 0.1, CancellationToken cancellationToken = default)
        {
            var service = SelectService(useHardware);
            return await service.DetectBlackScenesAsync(videoPath, totalDuration, pixelThreshold, durationThreshold, cancellationToken).ConfigureAwait(false);
        }

        public async Task<TimeSpan?> GetVideoDurationAsync(string videoPath, CancellationToken cancellationToken = default)
        {
            return await _swService.GetVideoDurationAsync(videoPath, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _swService.Dispose();
                _hwaService.Dispose();
            }

            _disposed = true;
        }
    }
}