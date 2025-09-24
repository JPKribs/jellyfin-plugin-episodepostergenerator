using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Orchestrates episode poster generation for arrays of episodes.
    /// </summary>
    public class EpisodePosterOrchestrator
    {
        private readonly ILogger<EpisodePosterOrchestrator> _logger;
        private readonly FFmpegService _ffmpegService;
        private readonly PosterGeneratorService _posterGeneratorService;

        // MARK: Constructor
        public EpisodePosterOrchestrator(
            ILogger<EpisodePosterOrchestrator> logger,
            FFmpegService ffmpegService,
            PosterGeneratorService posterGeneratorService)
        {
            _logger = logger;
            _ffmpegService = ffmpegService;
            _posterGeneratorService = posterGeneratorService;
        }

        // MARK: GeneratePoster
        public async Task<string[]> GeneratePoster(
            Episode[] episodes,
            PluginConfiguration config,
            CancellationToken cancellationToken = default)
        {
            var results = new List<string>();
            var tempDir = Path.Combine(Path.GetTempPath(), "episodeposter");
            Directory.CreateDirectory(tempDir);

            foreach (var episode in episodes)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var posterPath = await GenerateSinglePoster(episode, config, tempDir, cancellationToken).ConfigureAwait(false);
                    if (posterPath != null)
                    {
                        results.Add(posterPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate poster for episode: {EpisodeName}", episode.Name);
                }
            }

            return results.ToArray();
        }

        // MARK: GenerateSinglePoster
        private async Task<string?> GenerateSinglePoster(
            Episode episode,
            PluginConfiguration config,
            string tempDir,
            CancellationToken cancellationToken)
        {
            var tempFramePath = Path.Combine(tempDir, $"frame_{episode.Id}_{DateTime.UtcNow.Ticks}.jpg");
            var tempPosterPath = Path.Combine(tempDir, $"poster_{episode.Id}_{DateTime.UtcNow.Ticks}.jpg");

            try
            {
                // Pull media video metadata early using static utility
                var mediaDetails = BaseItemVideoDetails.GetMediaDetails(episode);
                var video = mediaDetails.VideoDetails;

                if (video == null)
                {
                    _logger.LogWarning("No video stream found for episode: {EpisodeName}", episode.Name);
                    return null;
                }

                _logger.LogInformation(
                    "Video metadata for {EpisodeName}: {Codec} {Width}x{Height} {ColorSpace} HDR={HDR}",
                    episode.Name,
                    video.Codec,
                    video.Width,
                    video.Height,
                    video.ColorSpace,
                    video.VideoRangeType
                );

                // Extract frame or create blank background
                string? sourceImagePath = null;

                if (!config.ExtractPoster)
                {
                    sourceImagePath = CreateTransparentBackground(tempFramePath);
                }
                else
                {
                    sourceImagePath = await ExtractVideoFrame(episode, tempFramePath, cancellationToken).ConfigureAwait(false);
                }

                if (sourceImagePath == null)
                {
                    _logger.LogWarning("Failed to create source image for episode: {EpisodeName}", episode.Name);
                    return null;
                }

                // Generate poster with video-aware metadata available
                var posterPath = _posterGeneratorService.ProcessImageWithText(
                    sourceImagePath,
                    tempPosterPath,
                    episode,
                    config
                );

                if (posterPath == null || !File.Exists(posterPath))
                {
                    _logger.LogWarning("Failed to generate poster for episode: {EpisodeName}", episode.Name);
                    return null;
                }

                return posterPath;
            }
            finally
            {
                CleanupTempFile(tempFramePath);
            }
        }

        // MARK: ExtractVideoFrame
        private async Task<string?> ExtractVideoFrame(Episode episode, string outputPath, CancellationToken cancellationToken)
        {
            try
            {
                var duration = GetEpisodeDuration(episode);
                if (!duration.HasValue)
                {
                    duration = await _ffmpegService.GetVideoDurationAsync(episode.Path, cancellationToken).ConfigureAwait(false);
                }

                if (!duration.HasValue)
                {
                    _logger.LogWarning("Could not determine video duration for: {Path}", episode.Path);
                    return null;
                }

                var blackIntervals = await _ffmpegService.DetectBlackScenesParallelAsync(
                    episode.Path,
                    duration.Value,
                    0.1,
                    0.1,
                    cancellationToken).ConfigureAwait(false);

                var selectedTimestamp = _ffmpegService.SelectRandomTimestamp(duration.Value, blackIntervals);

                return await _ffmpegService.ExtractFrameAsync(
                    episode.Path,
                    selectedTimestamp,
                    outputPath,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract video frame for episode: {EpisodeName}", episode.Name);
                return null;
            }
        }

        // MARK: CreateTransparentBackground
        private string? CreateTransparentBackground(string outputPath)
        {
            try
            {
                using var surface = SKSurface.Create(new SKImageInfo(1920, 1080, SKColorType.Rgba8888));
                using var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);

                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.OpenWrite(outputPath);
                data.SaveTo(stream);

                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create transparent background at {Path}", outputPath);
                return null;
            }
        }

        // MARK: GetEpisodeDuration
        private TimeSpan? GetEpisodeDuration(Episode episode)
        {
            return episode.RunTimeTicks.HasValue
                ? TimeSpan.FromTicks(episode.RunTimeTicks.Value)
                : null;
        }

        // MARK: CleanupTempFile
        private void CleanupTempFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup temp file: {Path}", filePath);
                }
            }
        }
    }
}