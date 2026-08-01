using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Controller.Entities.TV;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Orchestrates poster canvas creation by delegating to frame extraction,
    /// cropping, and brightness services, or producing a blank canvas.
    /// </summary>
    public class CanvasService
    {
        private readonly ILogger<CanvasService> _logger;
        private readonly FrameExtractionService _frameExtractionService;
        private readonly CroppingService _croppingService;
        private readonly BrightnessService _brightnessService;

        public CanvasService(
            ILogger<CanvasService> logger,
            FrameExtractionService frameExtractionService,
            CroppingService croppingService,
            BrightnessService brightnessService)
        {
            _logger = logger;
            _frameExtractionService = frameExtractionService;
            _croppingService = croppingService;
            _brightnessService = brightnessService;
        }

        /// <summary>
        /// Generates up to <paramref name="count"/> poster canvases. Only the extracted-frame
        /// source can produce more than one; the others yield a single canvas. The caller owns
        /// every returned bitmap.
        /// </summary>
        public async Task<IReadOnlyList<SKBitmap>> GenerateCanvasesAsync(
            Episode episode,
            EpisodeMetadata metadata,
            PosterSettings config,
            int count,
            CancellationToken cancellationToken = default)
        {
            if (metadata?.VideoMetadata == null)
            {
                _logger.LogError("Invalid metadata provided to CanvasService");
                return Array.Empty<SKBitmap>();
            }

            var videoMeta = metadata.VideoMetadata;

            try
            {
                switch (config.CanvasSource)
                {
                    case CanvasSource.Extract:
                        return await BuildExtractedCanvasesAsync(episode, metadata, config, count, cancellationToken)
                            .ConfigureAwait(false);

                    case CanvasSource.SeriesBackdrop:
                        var backdropCanvas = LoadSeriesBackdropCanvas(metadata.VideoMetadata, config);
                        if (backdropCanvas == null)
                        {
                            _logger.LogInformation("Series backdrop unavailable for {SeriesName}, using transparent canvas",
                                metadata.SeriesName);
                            backdropCanvas = CreateFallbackCanvas(videoMeta.VideoWidth, videoMeta.VideoHeight);
                        }

                        return new[] { backdropCanvas };

                    case CanvasSource.None:
                    default:
                        return new[] { CreateFallbackCanvas(videoMeta.VideoWidth, videoMeta.VideoHeight) };
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating poster canvas for {SeriesName} - {EpisodeName}",
                    metadata.SeriesName, metadata.EpisodeName);
                return Array.Empty<SKBitmap>();
            }
        }

        // BuildExtractedCanvasesAsync
        // Extracts frames and turns each into a cropped, brightened canvas. Extracted frame
        // files are always removed, including when a later step throws.
        private async Task<IReadOnlyList<SKBitmap>> BuildExtractedCanvasesAsync(
            Episode episode,
            EpisodeMetadata metadata,
            PosterSettings config,
            int count,
            CancellationToken cancellationToken)
        {
            var framePaths = await _frameExtractionService
                .ExtractFrameCandidatesAsync(episode, config, count, cancellationToken)
                .ConfigureAwait(false);

            if (framePaths.Count == 0)
            {
                _logger.LogWarning("Frame extraction did not produce a valid output file");
                return Array.Empty<SKBitmap>();
            }

            var canvases = new List<SKBitmap>(framePaths.Count);

            try
            {
                foreach (var framePath in framePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!File.Exists(framePath))
                    {
                        continue;
                    }

                    using var bitmap = SKBitmap.Decode(framePath);
                    if (bitmap == null)
                    {
                        _logger.LogWarning("Failed to decode extracted frame");
                        continue;
                    }

                    var canvas = _croppingService.CropPoster(bitmap, config);

                    // CropPoster hands back the source untouched when nothing needed cropping;
                    // the source is disposed with the using above, so take a copy in that case.
                    if (ReferenceEquals(canvas, bitmap))
                    {
                        canvas = bitmap.Copy();
                    }

                    if (config.BrightenHDR > 0)
                    {
                        _logger.LogDebug("Applying HDR brightening: +{Brightness}%", config.BrightenHDR);
                        _brightnessService.BrightenBitmap(canvas, config.BrightenHDR);
                    }

                    canvases.Add(canvas);
                }
            }
            catch
            {
                foreach (var canvas in canvases)
                {
                    canvas.Dispose();
                }

                throw;
            }
            finally
            {
                foreach (var framePath in framePaths)
                {
                    TryDeleteFrame(framePath);
                }
            }

            // Every canvas comes from the same video, so recording the dimensions once is enough.
            if (canvases.Count > 0)
            {
                metadata.VideoMetadata.VideoWidth = canvases[0].Width;
                metadata.VideoMetadata.VideoHeight = canvases[0].Height;
            }

            return canvases;
        }

        // TryDeleteFrame
        // Removes a temporary extracted frame, logging but not propagating cleanup failures.
        private void TryDeleteFrame(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temporary file: {FilePath}", path);
            }
        }

        // LoadSeriesBackdropCanvas
        // Loads the series backdrop image and crops it to the configured poster dimensions.
        // Returns null when no backdrop is available or it cannot be decoded.
        private SKBitmap? LoadSeriesBackdropCanvas(VideoMetadata videoMeta, PosterSettings config)
        {
            var backdropPath = videoMeta.SeriesBackdropFilePath;
            if (string.IsNullOrEmpty(backdropPath) || !File.Exists(backdropPath))
            {
                return null;
            }

            using var bitmap = SKBitmap.Decode(backdropPath);
            if (bitmap == null)
            {
                _logger.LogWarning("Failed to decode series backdrop: {BackdropPath}", backdropPath);
                return null;
            }

            var cropped = _croppingService.CropPoster(bitmap, config);
            var canvas = ReferenceEquals(cropped, bitmap) ? bitmap.Copy() : cropped;

            videoMeta.VideoWidth = canvas.Width;
            videoMeta.VideoHeight = canvas.Height;

            return canvas;
        }

        // CreateFallbackCanvas
        // Creates an empty bitmap canvas with the specified dimensions.
        private SKBitmap CreateFallbackCanvas(int width, int height)
        {
            _logger.LogDebug("Creating fallback canvas {Width}x{Height}", width, height);
            var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            return bitmap;
        }
    }
}
