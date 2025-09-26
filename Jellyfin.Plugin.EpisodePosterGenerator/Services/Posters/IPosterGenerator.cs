using System;
using System.IO;
using SkiaSharp;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using MediaBrowser.Controller.Entities.TV;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    /// <summary>
    /// Defines the contract for generating poster images using a layered rendering approach.
    /// Layers: Canvas (base) → Overlay (tinting) → Graphics (static images) → Typography (text/logos)
    /// </summary>
    public interface IPosterGenerator
    {
        /// <summary>
        /// Generates a poster from a provided canvas and episode metadata using layered rendering.
        /// </summary>
        /// <param name="canvas">The canvas layer - base bitmap from video extraction or transparent background.</param>
        /// <param name="episodeMetadata">Metadata for the episode.</param>
        /// <param name="config">Plugin configuration guiding the generation.</param>
        /// <param name="outputPath">
        /// Optional. The path to save the generated poster. 
        /// If null or empty, a temporary path will be generated using the configured PosterFileType.
        /// </param>
        /// <returns>The path to the generated poster, or <c>null</c> if generation failed.</returns>
        string? Generate(
            SKBitmap canvas,
            EpisodeMetadata episodeMetadata,
            PluginConfiguration config,
            string? outputPath = null);
    }

    /// <summary>
    /// Base class for all poster generators providing the standard 4-layer rendering pipeline.
    /// ALL posters use: Canvas → Overlay → Graphics → Typography
    /// </summary>
    public abstract class BasePosterGenerator : IPosterGenerator
    {
        protected static float GetSafeAreaMargin(PluginConfiguration config) => config.PosterSafeArea / 100f;

        protected static void ApplySafeAreaConstraints(
            int width, int height, PluginConfiguration config,
            out float safeWidth, out float safeHeight, out float safeLeft, out float safeTop)
        {
            var safeAreaMargin = GetSafeAreaMargin(config);
            safeLeft = width * safeAreaMargin;
            safeTop = height * safeAreaMargin;
            safeWidth = width * (1 - 2 * safeAreaMargin);
            safeHeight = height * (1 - 2 * safeAreaMargin);
        }

        // MARK: Generate
        public string? Generate(SKBitmap canvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, string? outputPath = null)
        {
            try
            {
                int width = canvas.Width;
                int height = canvas.Height;

                using var surface = SKSurface.Create(new SKImageInfo(width, height));
                var skCanvas = surface.Canvas;
                skCanvas.Clear(SKColors.Transparent);

                // Layer 1: Canvas (base layer)
                RenderCanvas(skCanvas, canvas, episodeMetadata, config, width, height);

                // Layer 2: Overlay (color tinting)
                RenderOverlay(skCanvas, episodeMetadata, config, width, height);

                // Layer 3: Graphics (static images/watermarks)
                RenderGraphics(skCanvas, episodeMetadata, config, width, height);

                // Layer 4: Typography (text and logos)
                RenderTypography(skCanvas, episodeMetadata, config, width, height);

                // Encode and save
                using var finalImage = surface.Snapshot();
                using var finalBitmap = SKBitmap.FromImage(finalImage);
                
                return SavePoster(finalBitmap, config, outputPath);
            }
            catch (Exception ex)
            {
                LogError(ex, episodeMetadata.EpisodeName);
                return null;
            }
        }

        // MARK: RenderCanvas
        protected virtual void RenderCanvas(SKCanvas skCanvas, SKBitmap canvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int width, int height)
        {
            using var canvasPaint = new SKPaint { IsAntialias = true };
            skCanvas.DrawBitmap(canvas, 0, 0, canvasPaint);
        }

        // MARK: RenderOverlay
        protected virtual void RenderOverlay(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int width, int height)
        {
            if (string.IsNullOrEmpty(config.OverlayColor))
                return;

            var overlayColor = ColorUtils.ParseHexColor(config.OverlayColor);
            if (overlayColor.Alpha == 0)
                return;

            using var overlayPaint = new SKPaint
            {
                Color = overlayColor,
                Style = SKPaintStyle.Fill
            };
            skCanvas.DrawRect(SKRect.Create(width, height), overlayPaint);
        }

        // MARK: RenderGraphics
        protected virtual void RenderGraphics(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int width, int height)
        {
            if (string.IsNullOrEmpty(config.GraphicPath) || !File.Exists(config.GraphicPath))
                return;

            try
            {
                using var stream = File.OpenRead(config.GraphicPath);
                using var graphicBitmap = SKBitmap.Decode(stream);
                if (graphicBitmap == null)
                    return;

                // Graphics layer respects safe area constraints
                ApplySafeAreaConstraints(width, height, config, out float safeWidth, out float safeHeight, out float safeLeft, out float safeTop);
                var graphicRect = CalculateGraphicRect(graphicBitmap, safeLeft, safeTop, safeWidth, safeHeight);

                using var graphicPaint = new SKPaint 
                { 
                    IsAntialias = true, 
                    FilterQuality = SKFilterQuality.High 
                };
                
                skCanvas.DrawBitmap(graphicBitmap, graphicRect, graphicPaint);
            }
            catch
            {
                // Silently skip if graphic can't be loaded
            }
        }

        // MARK: RenderTypography
        protected abstract void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int width, int height);

        // MARK: GetSafeAreaBounds
        protected static SKRect GetSafeAreaBounds(int width, int height, PluginConfiguration config)
        {
            ApplySafeAreaConstraints(width, height, config, out float safeWidth, out float safeHeight, out float safeLeft, out float safeTop);
            return new SKRect(safeLeft, safeTop, safeLeft + safeWidth, safeTop + safeHeight);
        }

        // MARK: LogError
        protected abstract void LogError(Exception ex, string? episodeName);

        // MARK: CalculateGraphicRect
        protected virtual SKRect CalculateGraphicRect(SKBitmap graphicBitmap, float safeLeft, float safeTop, float safeWidth, float safeHeight)
        {
            float maxSize = Math.Min(safeWidth, safeHeight) * 0.25f;
            float aspectRatio = (float)graphicBitmap.Width / graphicBitmap.Height;
            
            float graphicWidth, graphicHeight;
            if (aspectRatio > 1)
            {
                graphicWidth = Math.Min(maxSize, safeWidth * 0.3f);
                graphicHeight = graphicWidth / aspectRatio;
            }
            else
            {
                graphicHeight = Math.Min(maxSize, safeHeight * 0.3f);
                graphicWidth = graphicHeight * aspectRatio;
            }

            float x = safeLeft + (safeWidth - graphicWidth) / 2f;
            float y = safeTop + (safeHeight - graphicHeight) / 2f;

            return new SKRect(x, y, x + graphicWidth, y + graphicHeight);
        }

        // MARK: SavePoster
        private string? SavePoster(SKBitmap bitmap, PluginConfiguration config, string? outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                var extension = config.PosterFileType.ToString().ToLowerInvariant();
                outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.{extension}");
            }

            SKEncodedImageFormat format = config.PosterFileType switch
            {
                PosterFileType.JPEG => SKEncodedImageFormat.Jpeg,
                PosterFileType.PNG => SKEncodedImageFormat.Png,
                PosterFileType.WEBP => SKEncodedImageFormat.Webp,
                PosterFileType.GIF => SKEncodedImageFormat.Gif,
                _ => SKEncodedImageFormat.Png
            };

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, 95);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using var outputStream = File.OpenWrite(outputPath);
            data.SaveTo(outputStream);

            return outputPath;
        }
    }

    /// <summary>
    /// Canvas-focused poster generator that applies cropping to the canvas layer.
    /// Primarily handles Canvas layer processing with minimal additional layers.
    /// </summary>
    public class CanvasPosterGenerator : BasePosterGenerator
    {
        private readonly CroppingService _croppingService;
        private readonly ILogger<CanvasPosterGenerator> _logger;

        public CanvasPosterGenerator(CroppingService croppingService, ILogger<CanvasPosterGenerator> logger)
        {
            _croppingService = croppingService;
            _logger = logger;
        }

        // MARK: RenderCanvas
        protected override void RenderCanvas(SKCanvas skCanvas, SKBitmap canvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int width, int height)
        {
            var processedCanvas = _croppingService.CropPoster(canvas, episodeMetadata.VideoMetadata, config);
            using var canvasPaint = new SKPaint { IsAntialias = true };
            skCanvas.DrawBitmap(processedCanvas, 0, 0, canvasPaint);
            
            if (processedCanvas != canvas)
                processedCanvas.Dispose();
        }

        // MARK: RenderTypography
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int width, int height)
        {
            // Canvas generator has no typography by default
        }

        // MARK: LogError
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate canvas poster for episode {EpisodeName}", episodeName);
        }
    }
}