using System;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

public static class FontUtils
{
    private static readonly string[] CondensedFonts = {
        "Impact",
        "Arial Black", 
        "Bebas Neue",
        "Oswald",
        "Roboto Condensed",
        "Arial Narrow",
        "Franklin Gothic Heavy",
        "Helvetica Neue Condensed Bold"
    };

    private static readonly string[] BoldFonts = {
        "Arial Black",
        "Helvetica Bold",
        "Segoe UI Black",
        "Calibri Bold",
        "Verdana Bold"
    };

    // MARK: GetBestDisplayTypeface
    public static SKTypeface GetBestDisplayTypeface()
    {
        foreach (var fontName in CondensedFonts)
        {
            var typeface = SKTypeface.FromFamilyName(fontName, SKFontStyle.Bold);
            if (typeface != null && !typeface.FamilyName.Equals("serif", StringComparison.OrdinalIgnoreCase))
            {
                return typeface;
            }
            typeface?.Dispose();
        }

        foreach (var fontName in BoldFonts)
        {
            var typeface = SKTypeface.FromFamilyName(fontName, SKFontStyle.Bold);
            if (typeface != null && !typeface.FamilyName.Equals("serif", StringComparison.OrdinalIgnoreCase))
            {
                return typeface;
            }
            typeface?.Dispose();
        }

        return SKTypeface.FromFamilyName(null, SKFontStyle.Bold);
    }

    // MARK: GetCondensedTypeface
    public static SKTypeface GetCondensedTypeface()
    {
        foreach (var fontName in CondensedFonts)
        {
            var typeface = SKTypeface.FromFamilyName(fontName, SKFontStyle.Bold);
            if (typeface != null && !typeface.FamilyName.Equals("serif", StringComparison.OrdinalIgnoreCase))
            {
                return typeface;
            }
            typeface?.Dispose();
        }

        return SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
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
    public static float CalculateOptimalFontSize(string text, SKTypeface typeface, float maxWidth, float maxHeight, float minFontSize = 20f)
    {
        float fontSize = Math.Min(maxWidth, maxHeight) * 0.8f;
        
        while (fontSize > minFontSize)
        {
            var bounds = MeasureTextDimensions(text, typeface, fontSize);
            
            if (bounds.Width <= maxWidth && bounds.Height <= maxHeight)
                return fontSize;
                
            fontSize -= 2f;
        }
        
        return minFontSize;
    }
}