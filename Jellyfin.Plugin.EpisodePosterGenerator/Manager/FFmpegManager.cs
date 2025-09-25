using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Media
{
    /// <summary>
    /// Coordinates between hardware and software FFmpeg services for optimal frame extraction
    /// </summary>
    public class FFmpegManager
    {
        private readonly ILogger<FFmpegManager> _logger;
        private readonly FFmpegHardwareService _hardwareService;
        private readonly FFmpegSoftwareService _softwareService;

        // MARK: Constructor
        public FFmpegManager(
            ILogger<FFmpegManager> logger,
            FFmpegHardwareService hardwareService,
            FFmpegSoftwareService softwareService)
        {
            _logger = logger;
            _hardwareService = hardwareService;
            _softwareService = softwareService;
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
                // First, determine if hardware acceleration should be attempted
                var shouldTryHardware = await _hardwareService.ShouldUseHardwareAccelerationAsync(videoPath, cancellationToken).ConfigureAwait(false);

                if (shouldTryHardware)
                {
                    _logger.LogDebug("Attempting hardware-accelerated frame extraction for: {VideoPath}", videoPath);
                    
                    var hardwareResult = await _hardwareService.ExtractFrameAsync(videoPath, timestamp, cancellationToken).ConfigureAwait(false);
                    
                    if (!string.IsNullOrEmpty(hardwareResult) && File.Exists(hardwareResult))
                    {
                        _logger.LogDebug("Hardware extraction succeeded for: {VideoPath}", videoPath);
                        return hardwareResult;
                    }
                    
                    _logger.LogWarning("Hardware extraction failed, falling back to software for: {VideoPath}", videoPath);
                }

                // Fallback to software extraction
                _logger.LogDebug("Using software frame extraction for: {VideoPath}", videoPath);
                return await _softwareService.ExtractFrameAsync(videoPath, timestamp, cancellationToken).ConfigureAwait(false);
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
            // Use software service for metadata operations (both services should have this)
            return await _softwareService.GetVideoDurationAsync(videoPath, cancellationToken).ConfigureAwait(false);
        }

        // MARK: DetectBlackScenesAsync
        public async Task<BlackInterval[]> DetectBlackScenesAsync(string videoPath, CancellationToken cancellationToken = default)
        {
            // Use software service for analysis operations
            return await _softwareService.DetectBlackScenesAsync(videoPath, cancellationToken).ConfigureAwait(false);
        }
    }
}