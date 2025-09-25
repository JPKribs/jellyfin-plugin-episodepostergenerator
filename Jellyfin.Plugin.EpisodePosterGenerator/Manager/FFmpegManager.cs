using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.FFmpeg;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Models.Enums;
using Jellyfin.Plugin.EpisodePosterGenerator.Managers.FFmpeg;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Managers
{
    public sealed class FFmpegManager : IDisposable
    {
        private readonly ILogger<FFmpegManager> _logger;
        private readonly HardwareDecodeManager _hardwareDecodeManager;
        private readonly SoftwareDecodeManager _softwareDecodeManager;
        private readonly SWFFmpegService _swService;
        private readonly HWAFFmpegService _hwaService;
        private bool _disposed;

        // MARK: Constructor
        public FFmpegManager(
            ILogger<FFmpegManager> logger,
            HardwareDecodeManager hardwareDecodeManager,
            SoftwareDecodeManager softwareDecodeManager)
        {
            _logger = logger;
            _hardwareDecodeManager = hardwareDecodeManager;
            _softwareDecodeManager = softwareDecodeManager;
            _swService = new SWFFmpegService();
            _hwaService = new HWAFFmpegService();
        }

        // MARK: ExtractFrameAsync (New method signature)
        public async Task<(byte[] frameImage, DecodeType decodeType)> ExtractFrameAsync(
            Episode episode,
            TimeSpan? timecode = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Try hardware first
                return await _hardwareDecodeManager.ExtractFrameAsync(episode, timecode, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hardware decode failed, falling back to software for {Path}", episode.Path);
                
                // Fall back to software
                return await _softwareDecodeManager.ExtractFrameAsync(episode, timecode, cancellationToken).ConfigureAwait(false);
            }
        }

        // MARK: ExtractFrameAsync (Legacy method - keep for backward compatibility)
        public async Task<string?> ExtractFrameAsync(bool useHardware, string videoPath, TimeSpan timestamp, string outputPath, CancellationToken cancellationToken = default)
        {
            var service = SelectService(useHardware);
            return await service.ExtractFrameAsync(videoPath, timestamp, outputPath, cancellationToken).ConfigureAwait(false);
        }

        // MARK: DetectBlackScenesAsync
        public async Task<List<BlackInterval>> DetectBlackScenesAsync(bool useHardware, string videoPath, TimeSpan totalDuration, double pixelThreshold = 0.1, double durationThreshold = 0.1, CancellationToken cancellationToken = default)
        {
            var service = SelectService(useHardware);
            return await service.DetectBlackScenesAsync(videoPath, totalDuration, pixelThreshold, durationThreshold, cancellationToken).ConfigureAwait(false);
        }

        // MARK: GetVideoDurationAsync
        public async Task<TimeSpan?> GetVideoDurationAsync(string videoPath, CancellationToken cancellationToken = default)
        {
            return await _swService.GetVideoDurationAsync(videoPath, cancellationToken).ConfigureAwait(false);
        }

        // MARK: SelectService (Legacy - private)
        private IFFmpegService SelectService(bool useHardware)
        {
            return useHardware ? _hwaService : _swService;
        }

        // MARK: Dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // MARK: Dispose
        private void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _swService?.Dispose();
                _hwaService?.Dispose();
            }

            _disposed = true;
        }
    }
}