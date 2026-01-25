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
    public interface IPosterGenerator
    {
        // Generate
        // Generates a poster from a provided canvas and episode metadata using layered rendering.
        string? Generate(
            SKBitmap canvas,
            EpisodeMetadata episodeMetadata,
            PosterSettings settings,
            string? outputPath = null);
    }

    public abstract class BasePosterGenerator : IPosterGenerator
    {
        // GetSafeAreaMargin
        // Returns the safe area margin as a percentage of the poster dimensions.
        protected static float GetSafeAreaMargin(PosterSettings settings) => settings.PosterSafeArea / 100f;

        // ApplySafeAreaConstraints
        // Calculates the safe area dimensions and offsets for a given poster size.
        protected static void ApplySafeAreaConstraints(
            int width, int height, PosterSettings settings,
            out float safeWidth, out float safeHeight, out float safeLeft, out float safeTop)
        {
            var safeAreaMargin = GetSafeAreaMargin(settings);
            safeLeft = width * safeAreaMargin;
            safeTop = height * safeAreaMargin;
            safeWidth = width * (1 - 2 * safeAreaMargin);
            safeHeight = height * (1 - 2 * safeAreaMargin);
        }

        // Generate
        // Generates a poster using the 4-layer rendering pipeline and saves it to disk.
        public string? Generate(SKBitmap canvas, EpisodeMetadata episodeMetadata, PosterSettings settings, string? outputPath = null)
        {
            try
            {
                int width = canvas.Width;
                int height = canvas.Height;

                var imageInfo = new SKImageInfo(
                    width,
                    height,
                    SKColorType.Rgba8888,
                    SKAlphaType.Premul,
                    SKColorSpace.CreateSrgb());

                using var surface = SKSurface.Create(imageInfo);
                var skCanvas = surface.Canvas;
                skCanvas.Clear(SKColors.Transparent);

                // Layer 1: Canvas (base layer)
                RenderCanvas(skCanvas, canvas, episodeMetadata, settings, width, height);

                // Layer 2: Overlay (color tinting)
                RenderOverlay(skCanvas, episodeMetadata, settings, width, height);

                // Layer 3: Graphics (static images/watermarks)
                RenderGraphics(skCanvas, episodeMetadata, settings, width, height);

                // Layer 4: Typography (text and logos)
                RenderTypography(skCanvas, episodeMetadata, settings, width, height);

                using var finalImage = surface.Snapshot();
                using var finalBitmap = SKBitmap.FromImage(finalImage);

                return SavePoster(finalBitmap, settings, outputPath);
            }
            catch (Exception ex)
            {
                LogError(ex, episodeMetadata.EpisodeName);
                return null;
            }
        }

        // RenderCanvas
        // Draws the base canvas bitmap onto the surface.
        protected virtual void RenderCanvas(SKCanvas skCanvas, SKBitmap canvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            using var canvasPaint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };
            skCanvas.DrawBitmap(canvas, 0, 0, canvasPaint);
        }

        // RenderOverlay
        // Applies a color overlay with optional gradient to the poster.
        protected virtual void RenderOverlay(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            if (string.IsNullOrEmpty(settings.OverlayColor))
                return;

            var primaryColor = ColorUtils.ParseHexColor(settings.OverlayColor);
            if (primaryColor.Alpha == 0)
                return;

            var rect = SKRect.Create(width, height);

            // Solid color overlay branch
            if (settings.OverlayGradient == OverlayGradient.None)
            {
                using var overlayPaint = new SKPaint
                {
                    Color = primaryColor,
                    Style = SKPaintStyle.Fill
                };
                skCanvas.DrawRect(rect, overlayPaint);
            }
            // Gradient overlay branch
            else
            {
                var secondaryColor = ColorUtils.ParseHexColor(settings.OverlaySecondaryColor);
                if (secondaryColor.Alpha == 0) secondaryColor = primaryColor;

                var gradient = CreateOverlayGradient(settings.OverlayGradient, rect, primaryColor, secondaryColor);
                if (gradient != null)
                {
                    using var overlayPaint = new SKPaint
                    {
                        Shader = gradient,
                        Style = SKPaintStyle.Fill,
                        IsDither = true
                    };
                    skCanvas.DrawRect(rect, overlayPaint);
                }
            }
        }

        // CreateOverlayGradient
        // Creates a shader for the specified gradient direction.
        protected virtual SKShader? CreateOverlayGradient(OverlayGradient gradientType, SKRect rect, SKColor primaryColor, SKColor secondaryColor)
        {
            var colors = new[] { primaryColor, secondaryColor };

            return gradientType switch
            {
                OverlayGradient.LeftToRight => SKShader.CreateLinearGradient(
                    new SKPoint(rect.Left, rect.MidY),
                    new SKPoint(rect.Right, rect.MidY),
                    colors, null, SKShaderTileMode.Clamp, SKMatrix.Identity),

                OverlayGradient.BottomToTop => SKShader.CreateLinearGradient(
                    new SKPoint(rect.MidX, rect.Bottom),
                    new SKPoint(rect.MidX, rect.Top),
                    colors, null, SKShaderTileMode.Clamp, SKMatrix.Identity),

                OverlayGradient.TopLeftCornerToBottomRightCorner => SKShader.CreateLinearGradient(
                    new SKPoint(rect.Left, rect.Top),
                    new SKPoint(rect.Right, rect.Bottom),
                    colors, null, SKShaderTileMode.Clamp, SKMatrix.Identity),

                OverlayGradient.TopRightCornerToBottomLeftCorner => SKShader.CreateLinearGradient(
                    new SKPoint(rect.Right, rect.Top),
                    new SKPoint(rect.Left, rect.Bottom),
                    colors, null, SKShaderTileMode.Clamp, SKMatrix.Identity),

                _ => null
            };
        }

        // RenderGraphics
        // Loads and draws a static graphic image within the safe area.
        protected virtual void RenderGraphics(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            if (string.IsNullOrEmpty(settings.GraphicPath))
                return;

            if (!File.Exists(settings.GraphicPath))
            {
                LogError(new FileNotFoundException("Graphic file not found"), settings.GraphicPath);
                return;
            }

            try
            {
                using var stream = File.OpenRead(settings.GraphicPath);
                using var graphicBitmap = SKBitmap.Decode(stream);
                if (graphicBitmap == null)
                    return;

                ApplySafeAreaConstraints(width, height, settings, out float safeWidth, out float safeHeight, out float safeLeft, out float safeTop);
                var graphicRect = CalculateGraphicRect(graphicBitmap, safeLeft, safeTop, safeWidth, safeHeight, settings);

                using var graphicPaint = new SKPaint
                {
                    IsAntialias = true,
                    FilterQuality = SKFilterQuality.High
                };

                skCanvas.DrawBitmap(graphicBitmap, graphicRect, graphicPaint);
            }
            catch
            {
                LogError(new InvalidDataException("Failed to load or render graphic"), settings.GraphicPath);
            }
        }

        // RenderTypography
        // Draws text elements on the poster.
        protected abstract void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height);

        // GetSafeAreaBounds
        // Returns the safe area as an SKRect for the given poster dimensions.
        protected static SKRect GetSafeAreaBounds(int width, int height, PosterSettings settings)
        {
            ApplySafeAreaConstraints(width, height, settings, out float safeWidth, out float safeHeight, out float safeLeft, out float safeTop);
            return new SKRect(safeLeft, safeTop, safeLeft + safeWidth, safeTop + safeHeight);
        }

        // LogError
        // Logs an error that occurred during poster generation.
        protected abstract void LogError(Exception ex, string? episodeName);

        // CalculateGraphicRect
        // Calculates the destination rectangle for a graphic while preserving aspect ratio.
        protected virtual SKRect CalculateGraphicRect(SKBitmap graphicBitmap, float safeLeft, float safeTop, float safeWidth, float safeHeight, PosterSettings settings)
        {
            var posterWidth = safeWidth / (1 - 2 * GetSafeAreaMargin(settings));
            var posterHeight = safeHeight / (1 - 2 * GetSafeAreaMargin(settings));

            var maxWidth = posterWidth * (settings.GraphicWidth / 100f);
            var maxHeight = posterHeight * (settings.GraphicHeight / 100f);

            var originalAspect = (float)graphicBitmap.Width / graphicBitmap.Height;
            var constraintAspect = maxWidth / maxHeight;

            float finalWidth, finalHeight;

            // Image is wider than constraint - fit to width
            if (originalAspect > constraintAspect)
            {
                finalWidth = maxWidth;
                finalHeight = maxWidth / originalAspect;
            }
            // Image is taller than constraint - fit to height
            else
            {
                finalHeight = maxHeight;
                finalWidth = maxHeight * originalAspect;
            }

            var x = CalculateGraphicX(settings.GraphicAlignment, safeLeft, safeWidth, finalWidth);
            var y = CalculateGraphicY(settings.GraphicPosition, safeTop, safeHeight, finalHeight);

            return new SKRect(x, y, x + finalWidth, y + finalHeight);
        }

        // CalculateGraphicX
        // Calculates the horizontal position for a graphic based on alignment.
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

        // CalculateGraphicY
        // Calculates the vertical position for a graphic based on position.
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

        // SavePoster
        // Encodes and saves the poster bitmap to the specified path.
        private string? SavePoster(SKBitmap bitmap, PosterSettings settings, string? outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                var extension = settings.PosterFileType.ToString().ToLowerInvariant();
                outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.{extension}");
            }

            SKEncodedImageFormat format = settings.PosterFileType switch
            {
                PosterFileType.JPEG => SKEncodedImageFormat.Jpeg,
                PosterFileType.PNG => SKEncodedImageFormat.Png,
                PosterFileType.WEBP => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Png
            };

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, 100);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using var outputStream = File.OpenWrite(outputPath);
            data.SaveTo(outputStream);

            return outputPath;
        }
    }
}
