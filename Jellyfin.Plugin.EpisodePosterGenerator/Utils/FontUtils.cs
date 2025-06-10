using System;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

/// <summary>
/// Utility class for handling font selection, measurement, and sizing logic.
/// </summary>
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

    /// <summary>
    /// Attempts to return a bold, condensed typeface that is suitable for display purposes.
    /// </summary>
    /// <returns>An <see cref="SKTypeface"/> for bold display; falls back to a generic bold typeface if no match is found.</returns>
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

    /// <summary>
    /// Attempts to return a condensed bold typeface from a predefined list.
    /// </summary>
    /// <returns>An <see cref="SKTypeface"/> that is condensed and bold; defaults to Arial Bold if none found.</returns>
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

    /// <summary>
    /// Measures the bounding rectangle of a string of text using the given typeface and font size.
    /// </summary>
    /// <param name="text">Text to measure.</param>
    /// <param name="typeface">Typeface used to render the text.</param>
    /// <param name="fontSize">Font size in pixels.</param>
    /// <returns>A <see cref="SKRect"/> representing the bounds of the rendered text.</returns>
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

    /// <summary>
    /// Iteratively determines the largest possible font size that fits within the given width and height bounds.
    /// </summary>
    /// <param name="text">Text to measure and fit.</param>
    /// <param name="typeface">Typeface used for measurement.</param>
    /// <param name="maxWidth">Maximum allowed width in pixels.</param>
    /// <param name="maxHeight">Maximum allowed height in pixels.</param>
    /// <param name="minFontSize">Minimum acceptable font size to avoid infinite loop (default: 20).</param>
    /// <returns>The optimal font size in pixels that fits within the bounds.</returns>
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

    /// <summary>
    /// Calculates the font size in pixels based on a percentage of the poster's vertical height.
    /// </summary>
    /// <param name="percentage">Percentage of the poster height to use for font size (e.g., 5.0 means 5%).</param>
    /// <param name="posterHeight">Total height of the poster or canvas in pixels.</param>
    /// <param name="posterMargin">Percentage of the poster height reserved (e.g., 5.0 means 5%).</param>
    /// <returns>Calculated font size in pixels as an integer.</returns>
    public static int CalculateFontSizeFromPercentage(float percentage, float posterHeight, float posterMargin = 0)
    {
        if (percentage <= 0f || posterHeight <= 0f)
            return 0;

        return (int)(posterHeight * (percentage / (100f - (posterMargin * 2))));
    }
}