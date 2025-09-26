using System;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

/// <summary>
/// Font management and typography utility for poster generation.
/// </summary>
public static class FontUtils
{
    // MARK: CreateTypeface
    public static SKTypeface CreateTypeface(string fontFamilyName, SKFontStyle style)
    {
        var fontManager = SKFontManager.Default;
        return fontManager.MatchFamily(fontFamilyName, style);
    }

    // MARK: CreateTypeface
    public static SKTypeface CreateTypeface(SKFontStyle style)
    {
        return SKFontManager.Default.MatchFamily(null, style);
    }

    // MARK: MeasureTextDimensions
    public static SKRect MeasureTextDimensions(string text, SKTypeface typeface, float fontSize)
    {
        using var paint = new SKPaint
        {
            Typeface = typeface,
            TextSize = fontSize,
            IsAntialias = true
        };

        var bounds = new SKRect();
        paint.MeasureText(text, ref bounds);
        return bounds;
    }

    // MARK: CalculateOptimalFontSize
    public static float CalculateOptimalFontSize(string text, SKTypeface typeface, float maxWidth, float maxHeight, float minFontSize = 10f, float tolerance = 0.5f)
    {
        float maxFontSize = maxHeight;
        float optimalSize = minFontSize;
        float low = minFontSize;
        float high = maxFontSize;

        while (low <= high)
        {
            float mid = low + (high - low) / 2;
            
            if (mid <= 0) break;

            var bounds = MeasureTextDimensions(text, typeface, mid);

            if (bounds.Width <= maxWidth && bounds.Height <= maxHeight)
            {
                optimalSize = mid;
                low = mid + tolerance;
            }
            else
            {
                high = mid - tolerance;
            }
        }

        return optimalSize;
    }

    // MARK: CalculateFontSizeFromPercentage
    public static int CalculateFontSizeFromPercentage(float percentage, float posterHeight, float posterMargin = 0)
    {
        if (percentage <= 0f || posterHeight <= 0f)
            return 0;

        return (int)(posterHeight * (percentage / (100f - (posterMargin * 2))));
    }

    // MARK: GetFontStyle
    public static SKFontStyle GetFontStyle(string fontStyle)
    {
        return fontStyle.ToLowerInvariant() switch
        {
            "bold" => SKFontStyle.Bold,
            "italic" => SKFontStyle.Italic,
            "bold italic" => SKFontStyle.BoldItalic,
            _ => SKFontStyle.Normal,
        };
    }
}