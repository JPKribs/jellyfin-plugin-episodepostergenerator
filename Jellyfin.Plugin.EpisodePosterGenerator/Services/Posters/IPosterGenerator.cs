using System;
using System.IO;
using SkiaSharp;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utilities;
using MediaBrowser.Controller.Entities.TV;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    public interface IPosterGenerator
    {
        // Generate
        // Generates a poster from a provided canvas and episode metadata using layered rendering,
        // returning the encoded JPEG bytes, or null when rendering failed.
        byte[]? Generate(
            SKBitmap canvas,
            EpisodeMetadata episodeMetadata,
            PosterSettings settings);

        // Style
        // The poster style this generator produces.
        PosterStyle Style { get; }

        // Description
        // A short, user facing description of this style shown in the configuration UI.
        string Description { get; }
    }

    public abstract class BasePosterGenerator : IPosterGenerator
    {
        // Style
        // The poster style this generator produces.
        public abstract PosterStyle Style { get; }

        // Description
        // A short, user facing description of this style shown in the configuration UI.
        public abstract string Description { get; }

        // GetSafeAreaMargin
        // Returns the safe area margin as a percentage of the poster dimensions.
        protected static float GetSafeAreaMargin(PosterSettings settings) => settings.PosterSafeArea / 100f;

        // GetElementSpacing
        // The configured gap between stacked elements, in pixels for this poster height.
        // Every style resolves spacing through here so one setting moves them all consistently.
        protected static float GetElementSpacing(PosterSettings settings, float posterHeight)
            => posterHeight * (Math.Max(0f, settings.ElementSpacing) / 100f);

        // CenteredBaseline
        // First baseline for a run of text lines centred vertically inside its slot.
        //
        // Title slots are reserved at a fixed two lines so the elements above them cannot shift
        // between episodes with short and long titles. A one line title therefore leaves a line
        // of slack, and centring splits it evenly above and below rather than pooling it all at
        // one end — which reads as the block floating high or hanging low. A full two line title
        // fills the slot, so this is a no-op for it.
        protected static float CenteredBaseline(SKRect slot, int lineCount, float fontSize, float lineHeight)
        {
            var blockHeight = fontSize + (Math.Max(1, lineCount) - 1) * lineHeight;
            return slot.Top + Math.Max(0f, (slot.Height - blockHeight) / 2f) + fontSize;
        }

        // ApplySafeAreaConstraints
        // Calculates the safe area dimensions and offsets for a given poster size.
        // The margin is the safe area percent of the poster HEIGHT, applied as the same
        // pixel amount on all four sides, so the border is visually even (10% of a
        // 1600x1000 poster is a 100 pixel margin both vertically and horizontally).
        protected static void ApplySafeAreaConstraints(
            int width, int height, PosterSettings settings,
            out float safeWidth, out float safeHeight, out float safeLeft, out float safeTop)
        {
            var marginPixels = height * GetSafeAreaMargin(settings);
            safeLeft = marginPixels;
            safeTop = marginPixels;
            safeWidth = width - (2 * marginPixels);
            safeHeight = height - (2 * marginPixels);
        }

        // Generate
        // Generates a poster using the 4-layer rendering pipeline and returns the encoded JPEG.
        public byte[]? Generate(SKBitmap canvas, EpisodeMetadata episodeMetadata, PosterSettings settings)
        {
            try
            {
                int width = canvas.Width;
                int height = canvas.Height;

                if (settings.PaletteDerivedColors)
                {
                    settings = ApplyDerivedPalette(settings, canvas);
                }

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
                using var data = finalImage.Encode(SKEncodedImageFormat.Jpeg, RenderConstants.JpegQuality);

                return data?.ToArray();
            }
            catch (Exception ex)
            {
                LogError(ex, episodeMetadata.EpisodeName);
                return null;
            }
        }

        // ApplyDerivedPalette
        // Returns a render-time copy of the settings whose overlay colors are replaced by the
        // dominant color sampled from the canvas (secondary gets a darkened variant for depth).
        // The configured alpha channels are preserved; a transparent canvas leaves settings unchanged.
        private static PosterSettings ApplyDerivedPalette(PosterSettings settings, SKBitmap canvas)
        {
            // An empty or zero-alpha overlay means "no overlay" — leave it alone. ParseHexColor
            // falls back to opaque white for empty/invalid strings, so deriving from it would
            // turn a deliberately disabled overlay into a fully opaque one.
            if (string.IsNullOrEmpty(settings.OverlayColor))
                return settings;

            var primaryAlpha = ColorUtils.ParseHexColor(settings.OverlayColor).Alpha;
            if (primaryAlpha == 0)
                return settings;

            var dominant = ColorUtils.GetDominantColor(canvas);
            if (dominant == SKColor.Empty)
                return settings;

            var derived = settings.Clone();
            derived.OverlayColor = ColorUtils.ToArgbHex(dominant.WithAlpha(primaryAlpha));

            // The secondary color only participates when it is itself enabled; a zero-alpha
            // secondary keeps its meaning ("fall back to primary") in RenderOverlay.
            var secondaryAlpha = string.IsNullOrEmpty(settings.OverlaySecondaryColor)
                ? (byte)0
                : ColorUtils.ParseHexColor(settings.OverlaySecondaryColor).Alpha;
            if (secondaryAlpha > 0)
            {
                derived.OverlaySecondaryColor = ColorUtils.ToArgbHex(ColorUtils.Darken(dominant, 0.45f).WithAlpha(secondaryAlpha));
            }

            return derived;
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

                // SKPaint does not own its shader, so the gradient is disposed here rather
                // than left to the finalizer — a full library run creates one per poster.
                using var gradient = CreateOverlayGradient(settings.OverlayGradient, rect, primaryColor, secondaryColor);
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
            // The pixel margin comes from the poster height on both axes, so the height
            // reverses proportionally and the width just adds the margins back.
            var posterHeight = safeHeight / (1 - 2 * GetSafeAreaMargin(settings));
            var posterWidth = safeWidth + (2 * GetSafeAreaMargin(settings) * posterHeight);

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

    }
}
