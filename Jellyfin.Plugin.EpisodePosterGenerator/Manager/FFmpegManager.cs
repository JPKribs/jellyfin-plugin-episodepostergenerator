using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.FFmpeg;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Managers
{
    /// <summary>
    /// Manages FFmpeg operations by coordinating hardware and software services
    /// </summary>
    public class FFmpegManager : IDisposable
    {
        private readonly ILogger<FFmpegManager> _logger;
        private readonly FFmpegHardwareService _hardwareService;
        private readonly FFmpegSoftwareService _softwareService;
        private bool _disposed;

        // MARK: Constructor
        public FFmpegManager(
            ILogger<FFmpegManager> logger,
            ILogger<Services.FFmpegService> ffmpegServiceLogger,
            IMediaEncoder mediaEncoder,
            IServerConfigurationManager configurationManager)
        {
            _logger = logger;
            
            // Create hardware and software services
            _hardwareService = new FFmpegHardwareService(
                LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<FFmpegHardwareService>(),
                mediaEncoder,
                configurationManager);
                
            _softwareService = new FFmpegSoftwareService(
                LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<FFmpegSoftwareService>(),
                mediaEncoder);
        }

        // MARK: ExtractFrameAsync
        public async Task<string?> ExtractFrameAsync(
            string videoPath,
            TimeSpan timestamp,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            {
                _logger.LogWarning("Video file not found: {VideoPath}", videoPath);
                return null;
            }

            try
            {
                var outputPath = Path.GetTempFileName() + ".jpg";

                // Try hardware acceleration first
                var shouldTryHardware = await _hardwareService.ShouldUseHardwareAccelerationAsync(videoPath, cancellationToken).ConfigureAwait(false);

                if (shouldTryHardware)
                {
                    _logger.LogDebug("Attempting hardware-accelerated frame extraction for: {VideoPath}", videoPath);
                    
                    var hardwareResult = await _hardwareService.ExtractFrameAsync(videoPath, timestamp, outputPath, cancellationToken).ConfigureAwait(false);
                    
                    if (!string.IsNullOrEmpty(hardwareResult) && File.Exists(hardwareResult))
                    {
                        _logger.LogDebug("Hardware extraction succeeded for: {VideoPath}", videoPath);
                        return hardwareResult;
                    }
                    
                    _logger.LogWarning("Hardware extraction failed, falling back to software for: {VideoPath}", videoPath);
                }

                // Fallback to software extraction
                _logger.LogDebug("Using software frame extraction for: {VideoPath}", videoPath);
                return await _softwareService.ExtractFrameAsync(videoPath, timestamp, outputPath, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract frame from video: {VideoPath}", videoPath);
                return null;
            }
        }

        // MARK: GetVideoDurationAsync
        public async Task<TimeSpan?> GetVideoDurationAsync(string videoPath, CancellationToken cancellationToken = default)
        {
            return await _softwareService.GetVideoDurationAsync(videoPath, cancellationToken).ConfigureAwait(false);
        }

        // MARK: DetectBlackScenesAsync
        public async Task<Models.BlackInterval[]> DetectBlackScenesAsync(string videoPath, CancellationToken cancellationToken = default)
        {
            try
            {
                var duration = await GetVideoDurationAsync(videoPath, cancellationToken).ConfigureAwait(false);
                if (!duration.HasValue)
                {
                    return Array.Empty<Models.BlackInterval>();
                }

                var intervals = await _softwareService.DetectBlackScenesAsync(videoPath, duration.Value, cancellationToken: cancellationToken).ConfigureAwait(false);
                return intervals.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to detect black scenes: {VideoPath}", videoPath);
                return Array.Empty<Models.BlackInterval>();
            }
        }

        // MARK: Dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // MARK: Dispose
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _hardwareService?.Dispose();
                _softwareService?.Dispose();
                _disposed = true;
            }
        }
    }
}