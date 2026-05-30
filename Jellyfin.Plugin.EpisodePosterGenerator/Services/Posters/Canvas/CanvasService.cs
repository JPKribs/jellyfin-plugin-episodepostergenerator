using System;
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
        /// Generates a poster canvas by extracting a frame, cropping, and brightening it,
        /// or creating a transparent fallback canvas if extraction is disabled.
        /// </summary>
        public async Task<SKBitmap?> GenerateCanvasAsync(Episode episode, EpisodeMetadata metadata, PosterSettings config, CancellationToken cancellationToken = default)
        {
            if (metadata?.VideoMetadata == null)
            {
                _logger.LogError("Invalid metadata provided to CanvasService");
                return null;
            }

            var videoMeta = metadata.VideoMetadata;
            SKBitmap? canvasBitmap = null;
            string? extractedFramePath = null;

            try
            {
                // Branch on the configured canvas background source.
                switch (config.CanvasSource)
                {
                    case CanvasSource.Extract:
                        extractedFramePath = await _frameExtractionService.ExtractFrameAsync(
                            episode,
                            config,
                            cancellationToken).ConfigureAwait(false);

                        if (string.IsNullOrEmpty(extractedFramePath) || !File.Exists(extractedFramePath))
                        {
                            _logger.LogWarning("Frame extraction did not produce a valid output file");
                            return null;
                        }

                        using (var bitmap = SKBitmap.Decode(extractedFramePath))
                        {
                            if (bitmap == null)
                            {
                                _logger.LogWarning("Failed to decode extracted frame");
                                return null;
                            }

                            canvasBitmap = bitmap.Copy();
                        }

                        var croppedBitmap = _croppingService.CropPoster(canvasBitmap, metadata.VideoMetadata, config);
                        if (croppedBitmap != canvasBitmap)
                        {
                            using (canvasBitmap) { }
                            canvasBitmap = croppedBitmap;
                        }

                        if (config.BrightenHDR > 0)
                        {
                            _logger.LogDebug("Applying HDR brightening: +{Brightness}%", config.BrightenHDR);
                            _brightnessService.BrightenBitmap(canvasBitmap, config.BrightenHDR);
                        }
                        break;

                    case CanvasSource.SeriesBackdrop:
                        canvasBitmap = LoadSeriesBackdropCanvas(metadata.VideoMetadata, config);
                        if (canvasBitmap == null)
                        {
                            _logger.LogInformation("Series backdrop unavailable for {SeriesName}, using transparent canvas",
                                metadata.SeriesName);
                            canvasBitmap = CreateFallbackCanvas(videoMeta.VideoWidth, videoMeta.VideoHeight);
                        }
                        break;

                    case CanvasSource.None:
                    default:
                        // Branch: Create transparent canvas when no background is requested
                        canvasBitmap = CreateFallbackCanvas(videoMeta.VideoWidth, videoMeta.VideoHeight);
                        break;
                }

                // Return ownership of bitmap to caller (caller is responsible for disposing)
                var result = canvasBitmap;
                canvasBitmap = null;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating poster for {SeriesName} - {EpisodeName}",
                    metadata.SeriesName, metadata.EpisodeName);
                return null;
            }
            finally
            {
                canvasBitmap?.Dispose();

                if (!string.IsNullOrEmpty(extractedFramePath) && File.Exists(extractedFramePath))
                {
                    try
                    {
                        File.Delete(extractedFramePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cleanup temporary file: {FilePath}", extractedFramePath);
                    }
                }
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

            SKBitmap? canvasBitmap;
            using (var bitmap = SKBitmap.Decode(backdropPath))
            {
                if (bitmap == null)
                {
                    _logger.LogWarning("Failed to decode series backdrop: {BackdropPath}", backdropPath);
                    return null;
                }

                canvasBitmap = bitmap.Copy();
            }

            var cropped = _croppingService.CropPoster(canvasBitmap, videoMeta, config);
            if (cropped != canvasBitmap)
            {
                using (canvasBitmap) { }
                canvasBitmap = cropped;
            }

            return canvasBitmap;
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
