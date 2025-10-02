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

            var primaryColor = ColorUtils.ParseHexColor(config.OverlayColor);
            if (primaryColor.Alpha == 0)
                return;

            var rect = SKRect.Create(width, height);

            if (config.OverlayGradient == OverlayGradient.None)
            {
                // Solid color overlay
                using var overlayPaint = new SKPaint
                {
                    Color = primaryColor,
                    Style = SKPaintStyle.Fill
                };
                skCanvas.DrawRect(rect, overlayPaint);
            }
            else
            {
                // Gradient overlay
                var secondaryColor = ColorUtils.ParseHexColor(config.OverlaySecondaryColor);
                if (secondaryColor.Alpha == 0) secondaryColor = primaryColor;

                var gradient = CreateOverlayGradient(config.OverlayGradient, rect, primaryColor, secondaryColor);
                if (gradient != null)
                {
                    using var overlayPaint = new SKPaint
                    {
                        Shader = gradient,
                        Style = SKPaintStyle.Fill
                    };
                    skCanvas.DrawRect(rect, overlayPaint);
                }
            }
        }

        // MARK: CreateOverlayGradient
        protected virtual SKShader? CreateOverlayGradient(OverlayGradient gradientType, SKRect rect, SKColor primaryColor, SKColor secondaryColor)
        {
            var colors = new[] { primaryColor, secondaryColor };
            
            return gradientType switch
            {
                OverlayGradient.LeftToRight => SKShader.CreateLinearGradient(
                    new SKPoint(rect.Left, rect.MidY),
                    new SKPoint(rect.Right, rect.MidY),
                    colors, null, SKShaderTileMode.Clamp),
                    
                OverlayGradient.BottomToTop => SKShader.CreateLinearGradient(
                    new SKPoint(rect.MidX, rect.Bottom),
                    new SKPoint(rect.MidX, rect.Top),
                    colors, null, SKShaderTileMode.Clamp),
                    
                OverlayGradient.TopLeftCornerToBottomRightCorner => SKShader.CreateLinearGradient(
                    new SKPoint(rect.Left, rect.Top),
                    new SKPoint(rect.Right, rect.Bottom),
                    colors, null, SKShaderTileMode.Clamp),
                    
                OverlayGradient.TopRightCornerToBottomLeftCorner => SKShader.CreateLinearGradient(
                    new SKPoint(rect.Right, rect.Top),
                    new SKPoint(rect.Left, rect.Bottom),
                    colors, null, SKShaderTileMode.Clamp),
                    
                _ => null
            };
        }

        // MARK: RenderGraphics
        protected virtual void RenderGraphics(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int width, int height)
        {
            if (string.IsNullOrEmpty(config.GraphicPath))
                return;

            if (!File.Exists(config.GraphicPath))
            {
                LogError(new FileNotFoundException("Graphic file not found"), config.GraphicPath);
                return;
            }

            try
            {
                using var stream = File.OpenRead(config.GraphicPath);
                using var graphicBitmap = SKBitmap.Decode(stream);
                if (graphicBitmap == null)
                    return;

                // Graphics layer respects safe area constraints
                ApplySafeAreaConstraints(width, height, config, out float safeWidth, out float safeHeight, out float safeLeft, out float safeTop);
                var graphicRect = CalculateGraphicRect(graphicBitmap, safeLeft, safeTop, safeWidth, safeHeight, config);

                using var graphicPaint = new SKPaint
                {
                    IsAntialias = true,
                    FilterQuality = SKFilterQuality.High
                };

                skCanvas.DrawBitmap(graphicBitmap, graphicRect, graphicPaint);
            }
            catch
            {
                LogError(new InvalidDataException("Failed to load or render graphic"), config.GraphicPath);
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
        protected virtual SKRect CalculateGraphicRect(SKBitmap graphicBitmap, float safeLeft, float safeTop, float safeWidth, float safeHeight, PluginConfiguration config)
        {
            // Calculate maximum allowed dimensions from percentages
            var posterWidth = safeWidth / (1 - 2 * GetSafeAreaMargin(config));
            var posterHeight = safeHeight / (1 - 2 * GetSafeAreaMargin(config));
            
            var maxWidth = posterWidth * (config.GraphicWidth / 100f);
            var maxHeight = posterHeight * (config.GraphicHeight / 100f);
            
            // Calculate aspect ratio preserving dimensions
            var originalAspect = (float)graphicBitmap.Width / graphicBitmap.Height;
            var constraintAspect = maxWidth / maxHeight;
            
            float finalWidth, finalHeight;
            
            if (originalAspect > constraintAspect)
            {
                // Image is wider than constraint - fit to width
                finalWidth = maxWidth;
                finalHeight = maxWidth / originalAspect;
            }
            else
            {
                // Image is taller than constraint - fit to height  
                finalHeight = maxHeight;
                finalWidth = maxHeight * originalAspect;
            }
            
            // Calculate position based on alignment and position
            var x = CalculateGraphicX(config.GraphicAlignment, safeLeft, safeWidth, finalWidth);
            var y = CalculateGraphicY(config.GraphicPosition, safeTop, safeHeight, finalHeight);
            
            return new SKRect(x, y, x + finalWidth, y + finalHeight);
        }

        // MARK: CalculateGraphicX
        private float CalculateGraphicX(Alignment alignment, float safeLeft, float safeWidth, float graphicWidth)
        {
            return alignment switch
            {
                Alignment.Left => safeLeft,
                Alignment.Center => safeLeft + (safeWidth - graphicWidth) / 2f,
                Alignment.Right => safeLeft + safeWidth - graphicWidth,
                _ => safeLeft + (safeWidth - graphicWidth) / 2f
            };
        }

        // MARK: CalculateGraphicY
        private float CalculateGraphicY(Position position, float safeTop, float safeHeight, float graphicHeight)
        {
            return position switch
            {
                Position.Top => safeTop,
                Position.Center => safeTop + (safeHeight - graphicHeight) / 2f,
                Position.Bottom => safeTop + safeHeight - graphicHeight,
                _ => safeTop + (safeHeight - graphicHeight) / 2f
            };
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