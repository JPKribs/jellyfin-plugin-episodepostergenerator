using System;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils
{
    // PaintFactory
    // Factory for creating commonly used SKPaint configurations with consistent settings.
    public static class PaintFactory
    {
        // Cached blur mask filter — same sigma used for all shadow paints, lives for process lifetime
        private static readonly Lazy<SKMaskFilter> ShadowBlurFilter = new Lazy<SKMaskFilter>(
            () => SKMaskFilter.CreateBlur(SKBlurStyle.Normal, RenderConstants.ShadowBlurSigma));

        // CreateTextPaint
        // Creates a text paint with standard rendering settings.
        public static SKPaint CreateTextPaint(
            SKColor color,
            float fontSize,
            SKTypeface typeface,
            SKTextAlign textAlign = SKTextAlign.Center)
        {
            return new SKPaint
            {
                Color = color,
                TextSize = fontSize,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                Typeface = typeface,
                TextAlign = textAlign,
                TextEncoding = SKTextEncoding.Utf8
            };
        }

        // CreateShadowTextPaint
        // Creates a text paint with shadow blur effect.
        public static SKPaint CreateShadowTextPaint(
            float fontSize,
            SKTypeface typeface,
            SKTextAlign textAlign = SKTextAlign.Center)
        {
            return new SKPaint
            {
                Color = RenderConstants.ShadowColor,
                TextSize = fontSize,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                Typeface = typeface,
                TextAlign = textAlign,
                MaskFilter = ShadowBlurFilter.Value,
                TextEncoding = SKTextEncoding.Utf8
            };
        }

        // CreateBitmapPaint
        // Creates a paint for high-quality bitmap rendering.
        public static SKPaint CreateBitmapPaint()
        {
            return new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };
        }

        // CreateLinePaint
        // Creates a paint for drawing lines with standard stroke settings.
        public static SKPaint CreateLinePaint(SKColor color, float strokeWidth = RenderConstants.SeparatorStrokeWidth)
        {
            return new SKPaint
            {
                Color = color,
                StrokeWidth = strokeWidth,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Square
            };
        }

        // CreateShadowLinePaint
        // Creates a paint for drawing shadow lines.
        public static SKPaint CreateShadowLinePaint(float strokeWidth = RenderConstants.SeparatorStrokeWidth)
        {
            return new SKPaint
            {
                Color = RenderConstants.ShadowColor,
                StrokeWidth = strokeWidth,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Square
            };
        }

        // CreateFillPaint
        // Creates a paint for solid color fills.
        public static SKPaint CreateFillPaint(SKColor color)
        {
            return new SKPaint
            {
                Color = color,
                Style = SKPaintStyle.Fill
            };
        }

        // DrawTextWithShadow
        // Draws text with a shadow effect at the specified position.
        public static void DrawTextWithShadow(
            SKCanvas canvas,
            string text,
            float x,
            float y,
            SKPaint textPaint,
            SKPaint shadowPaint)
        {
            canvas.DrawText(text, x + RenderConstants.ShadowOffset, y + RenderConstants.ShadowOffset, shadowPaint);
            canvas.DrawText(text, x, y, textPaint);
        }

        // DrawLineWithShadow
        // Draws a line with a shadow effect.
        public static void DrawLineWithShadow(
            SKCanvas canvas,
            float startX,
            float startY,
            float endX,
            float endY,
            SKPaint linePaint,
            SKPaint shadowPaint)
        {
            canvas.DrawLine(
                startX + RenderConstants.ShadowOffset,
                startY + RenderConstants.ShadowOffset,
                endX + RenderConstants.ShadowOffset,
                endY + RenderConstants.ShadowOffset,
                shadowPaint);
            canvas.DrawLine(startX, startY, endX, endY, linePaint);
        }
    }
}
