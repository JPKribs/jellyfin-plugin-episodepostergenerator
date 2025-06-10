using System;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

/// <summary>
/// Utility class for handling font selection, measurement, and sizing logic.
/// This version uses SKFontManager for robust, dynamic font matching and a more efficient
/// binary search for calculating optimal font sizes.
/// </summary>
public static class FontUtils
{
    /// <summary>
    /// Creates an SKTypeface by matching a font family name and a desired style.
    /// This is more reliable than FromFamilyName as it uses the system's font manager
    /// to find the best possible match if an exact one isn't available.
    /// </summary>
    /// <param name="fontFamilyName">The name of the font family (e.g., "Arial", "Roboto", "Impact").</param>
    /// <param name="style">The desired font style (e.g., Bold, Italic, Condensed).</param>
    /// <returns>A matching SKTypeface. If the requested family is not found, it returns a system default based on the style.</returns>
    /// <example>
    /// To get a bold, condensed font:
    /// <code>
    /// var style = new SKFontStyle(SKFontStyleWeight.Bold, SKFontStyleWidth.Condensed, SKFontStyleSlant.Upright);
    /// var typeface = FontUtils.CreateTypeface("Impact", style);
    /// </code>
    /// </example>
    public static SKTypeface CreateTypeface(string fontFamilyName, SKFontStyle style)
    {
        // SKFontManager.Default provides access to the system's installed fonts.
        var fontManager = SKFontManager.Default;

        // MatchFamily will find the best match for the requested family and style.
        // This is the modern, preferred way to select fonts.
        return fontManager.MatchFamily(fontFamilyName, style);
    }

    /// <summary>
    /// Creates a default typeface for a given style, without specifying a family.
    /// The system's font manager will select a suitable default font.
    /// </summary>
    /// <param name="style">The desired font style.</param>
    /// <returns>A default system SKTypeface matching the style.</returns>
    public static SKTypeface CreateTypeface(SKFontStyle style)
    {
        // Passing null or empty for the family name asks the font manager for a default.
        return SKFontManager.Default.MatchFamily(null, style);
    }

    /// <summary>
    /// Measures the bounding rectangle of a string of text using the given typeface and font size.
    /// </summary>
    /// <param name="text">Text to measure.</param>
    /// <param name="typeface">Typeface used to render the text.</param>
    /// <param name="fontSize">Font size in points.</param>
    /// <returns>A SKRect representing the bounds of the rendered text.</returns>
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
    /// Determines the largest font size that fits text within given dimensions using an efficient binary search.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <param name="typeface">The typeface to use.</param>
    /// <param name="maxWidth">The maximum allowed width.</param>
    /// <param name="maxHeight">The maximum allowed height.</param>
    /// <param name="minFontSize">The minimum allowed font size.</param>
    /// <param name="tolerance">The precision for the binary search. A smaller value is more accurate but may take more iterations.</param>
    /// <returns>The optimal font size that fits within the bounds.</returns>
    public static float CalculateOptimalFontSize(string text, SKTypeface typeface, float maxWidth, float maxHeight, float minFontSize = 10f, float tolerance = 0.5f)
    {
        // The upper bound can be set to the max height as text rarely exceeds this.
        float maxFontSize = maxHeight;
        float optimalSize = minFontSize;

        // Perform a binary search for the optimal font size.
        float low = minFontSize;
        float high = maxFontSize;

        while (low <= high)
        {
            float mid = low + (high - low) / 2;
            if (mid <= 0) break; // Safety check

            var bounds = MeasureTextDimensions(text, typeface, mid);

            if (bounds.Width <= maxWidth && bounds.Height <= maxHeight)
            {
                // This size fits. We can try for a larger size.
                optimalSize = mid; // Store this as a potential answer
                low = mid + tolerance;
            }
            else
            {
                // This size is too big. We must try a smaller size.
                high = mid - tolerance;
            }
        }

        return optimalSize;
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

    /// <summary>
    /// Parses a font style string from the configuration into an SKFontStyle object.
    /// </summary>
    /// <param name="fontStyle">The font style string (e.g., "Bold", "Italic").</param>
    /// <returns>An SKFontStyle object.</returns>
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
