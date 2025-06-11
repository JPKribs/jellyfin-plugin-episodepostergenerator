using System;
using System.Collections.Generic;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

/// <summary>
/// Utility class for rendering text on poster canvases with support for positioning, alignment, wrapping, and shadow effects.
/// Provides text fitting algorithms to ensure text displays properly within specified boundaries.
/// </summary>
public static class TextUtils
{
    private const float ShadowOffset = 2f;
    private const byte ShadowAlpha = 180;
    private const float LineSpacingMultiplier = 1.2f;

    /// <summary>
    /// Draws title text on a canvas with automatic text wrapping, positioning, and shadow effects.
    /// Text is automatically wrapped to fit within safe area boundaries, with a maximum of two lines.
    /// </summary>
    /// <param name="canvas">Canvas to draw the text on.</param>
    /// <param name="title">Title text to render.</param>
    /// <param name="position">Vertical position of the text (Top, Center, Bottom).</param>
    /// <param name="alignment">Horizontal alignment of the text (Left, Center, Right).</param>
    /// <param name="config">Plugin configuration containing font settings.</param>
    /// <param name="canvasWidth">Width of the canvas in pixels.</param>
    /// <param name="canvasHeight">Height of the canvas in pixels.</param>
    // MARK: DrawTitle
    public static void DrawTitle(
        SKCanvas canvas,
        string title,
        TextPosition position,
        TextAlignment alignment,
        PluginConfiguration config,
        float canvasWidth,
        float canvasHeight)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, canvasHeight, config.PosterSafeArea);
        var typeface = FontUtils.CreateTypeface(config.TitleFontFamily, FontUtils.GetFontStyle(config.TitleFontStyle));

        using var titlePaint = CreateTextPaint(config.TitleFontColor, fontSize, typeface, alignment);
        using var shadowPaint = CreateShadowPaint(fontSize, typeface, alignment);

        var safeArea = CalculateSafeArea(canvasWidth, canvasHeight, config);
        
        // Calculate horizontal padding from config: 100% - (left margin + right margin)
        var safeAreaMargin = config.PosterSafeArea / 100f;
        var horizontalPadding = 1.0f - (2 * safeAreaMargin);
        var maxTextWidth = safeArea.Width * horizontalPadding;
        
        var lines = FitTextToWidth(title, titlePaint, maxTextWidth);
        var textBounds = CalculateTextBounds(lines, titlePaint, fontSize);
        
        var alignmentX = CalculateAlignmentX(alignment, canvasWidth, safeArea);
        var baseY = CalculateBaseY(position, safeArea, textBounds.Height, fontSize, titlePaint);
        
        DrawTextLines(canvas, lines, alignmentX, baseY, fontSize, titlePaint, shadowPaint);
    }

    /// <summary>
    /// Fits text within specified width constraints by wrapping to multiple lines or truncating with ellipsis.
    /// Attempts optimal word wrapping first, then truncates with ellipsis if text still exceeds boundaries.
    /// </summary>
    /// <param name="text">Text to fit within width constraints.</param>
    /// <param name="paint">Paint object used for measuring text dimensions.</param>
    /// <param name="maxWidth">Maximum allowed width for text.</param>
    /// <returns>Read-only list of text lines that fit within the specified width.</returns>
    // MARK: FitTextToWidth
    public static IReadOnlyList<string> FitTextToWidth(string text, SKPaint paint, float maxWidth)
    {
        var lines = new List<string>();
        
        // If text fits on one line, return as-is
        if (paint.MeasureText(text) <= maxWidth)
        {
            lines.Add(text);
            return lines;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // If single word is too long, truncate with ellipsis
        if (words.Length == 1)
        {
            lines.Add(TruncateWithEllipsis(text, paint, maxWidth));
            return lines;
        }

        string line1 = "";
        string line2 = "";
        
        int splitPoint = FindOptimalSplitPoint(words, paint, maxWidth);
        
        // Split words between two lines
        for (int i = 0; i < words.Length; i++)
        {
            if (i < splitPoint)
            {
                line1 += (i > 0 ? " " : "") + words[i];
            }
            else
            {
                line2 += (i > splitPoint ? " " : "") + words[i];
            }
        }

        line1 = line1.Trim();
        line2 = line2.Trim();

        // Truncate first line if still too wide
        if (paint.MeasureText(line1) > maxWidth)
        {
            line1 = TruncateWithEllipsis(line1, paint, maxWidth);
        }
        
        // Add second line if it exists and fits, otherwise truncate
        if (!string.IsNullOrWhiteSpace(line2))
        {
            if (paint.MeasureText(line2) > maxWidth)
            {
                line2 = TruncateWithEllipsis(line2, paint, maxWidth);
            }
            lines.Add(line1);
            lines.Add(line2);
        }
        else
        {
            lines.Add(line1);
        }

        return lines;
    }

    /// <summary>
    /// Creates a text paint object with specified color, font, and alignment settings.
    /// </summary>
    /// <param name="hexColor">Hex color code for the text.</param>
    /// <param name="fontSize">Font size in pixels.</param>
    /// <param name="typeface">Typeface for the text.</param>
    /// <param name="alignment">Text alignment setting.</param>
    /// <returns>Configured SKPaint object for text rendering.</returns>
    // MARK: CreateTextPaint
    private static SKPaint CreateTextPaint(string hexColor, int fontSize, SKTypeface typeface, TextAlignment alignment)
    {
        return new SKPaint
        {
            Color = ColorUtils.ParseHexColor(hexColor),
            TextSize = fontSize,
            IsAntialias = true,
            Typeface = typeface,
            TextAlign = GetSKTextAlign(alignment)
        };
    }

    /// <summary>
    /// Creates a shadow paint object for text drop shadow effects.
    /// Uses semi-transparent black with specified font settings.
    /// </summary>
    /// <param name="fontSize">Font size in pixels.</param>
    /// <param name="typeface">Typeface for the shadow text.</param>
    /// <param name="alignment">Text alignment setting.</param>
    /// <returns>Configured SKPaint object for shadow rendering.</returns>
    // MARK: CreateShadowPaint
    private static SKPaint CreateShadowPaint(int fontSize, SKTypeface typeface, TextAlignment alignment)
    {
        return new SKPaint
        {
            Color = SKColors.Black.WithAlpha(ShadowAlpha),
            TextSize = fontSize,
            IsAntialias = true,
            Typeface = typeface,
            TextAlign = GetSKTextAlign(alignment)
        };
    }

    /// <summary>
    /// Converts TextAlignment enum to corresponding SKTextAlign value.
    /// </summary>
    /// <param name="alignment">TextAlignment enum value.</param>
    /// <returns>Corresponding SKTextAlign value.</returns>
    // MARK: GetSKTextAlign
    private static SKTextAlign GetSKTextAlign(TextAlignment alignment)
    {
        return alignment switch
        {
            TextAlignment.Left => SKTextAlign.Left,
            TextAlignment.Center => SKTextAlign.Center,
            TextAlignment.Right => SKTextAlign.Right,
            _ => SKTextAlign.Center
        };
    }

    /// <summary>
    /// Calculates the horizontal position for text alignment within the canvas.
    /// </summary>
    /// <param name="alignment">Desired text alignment.</param>
    /// <param name="canvasWidth">Total canvas width.</param>
    /// <param name="safeArea">Safe drawing area boundaries.</param>
    /// <returns>X coordinate for text positioning.</returns>
    // MARK: CalculateAlignmentX
    private static float CalculateAlignmentX(TextAlignment alignment, float canvasWidth, SKRect safeArea)
    {
        return alignment switch
        {
            TextAlignment.Left => safeArea.Left,
            TextAlignment.Center => canvasWidth / 2f,
            TextAlignment.Right => safeArea.Right,
            _ => canvasWidth / 2f
        };
    }

    /// <summary>
    /// Calculates the vertical baseline position for text based on position and font metrics.
    /// </summary>
    /// <param name="position">Desired vertical position.</param>
    /// <param name="safeArea">Safe drawing area boundaries.</param>
    /// <param name="textHeight">Total height of the text block.</param>
    /// <param name="fontSize">Font size in pixels.</param>
    /// <param name="paint">Paint object containing font metrics.</param>
    /// <returns>Y coordinate for text baseline positioning.</returns>
    // MARK: CalculateBaseY
    private static float CalculateBaseY(TextPosition position, SKRect safeArea, float textHeight, int fontSize, SKPaint paint)
    {
        var fontMetrics = paint.FontMetrics;
        float bottomPadding = fontSize * 0.5f;
        
        return position switch
        {
            TextPosition.Top => safeArea.Top - fontMetrics.Ascent,
            TextPosition.Center => safeArea.MidY - (textHeight / 2f) - fontMetrics.Ascent,
            TextPosition.Bottom => safeArea.Bottom - textHeight - bottomPadding - fontMetrics.Ascent,
            _ => safeArea.Bottom - textHeight - bottomPadding - fontMetrics.Ascent
        };
    }

    /// <summary>
    /// Calculates the safe drawing area within the canvas, accounting for margin padding.
    /// Safe area prevents text from being drawn too close to canvas edges.
    /// </summary>
    /// <param name="canvasWidth">Total canvas width.</param>
    /// <param name="canvasHeight">Total canvas height.</param>
    /// <returns>Rectangle defining the safe drawing area.</returns>
    // MARK: CalculateSafeArea
    private static SKRect CalculateSafeArea(float canvasWidth, float canvasHeight, PluginConfiguration config)
    {
        var safeAreaMargin = config.PosterSafeArea / 100f;
        var marginX = canvasWidth * safeAreaMargin;
        var marginY = canvasHeight * safeAreaMargin;
        
        return new SKRect(
            marginX,
            marginY,
            canvasWidth - marginX,
            canvasHeight - marginY
        );
    }

    /// <summary>
    /// Finds the optimal word split point for dividing text into two balanced lines.
    /// Attempts to minimize width difference between lines while ensuring both fit within maxWidth.
    /// </summary>
    /// <param name="words">Array of words to split.</param>
    /// <param name="paint">Paint object for measuring text width.</param>
    /// <param name="maxWidth">Maximum allowed width per line.</param>
    /// <returns>Index where words should be split between lines.</returns>
    // MARK: FindOptimalSplitPoint
    private static int FindOptimalSplitPoint(string[] words, SKPaint paint, float maxWidth)
    {
        int bestSplit = words.Length / 2;
        float bestDifference = float.MaxValue;

        for (int i = 1; i < words.Length; i++)
        {
            string firstPart = string.Join(" ", words[..i]);
            string secondPart = string.Join(" ", words[i..]);
            
            float firstWidth = paint.MeasureText(firstPart);
            float secondWidth = paint.MeasureText(secondPart);
            
            // Only consider splits where both parts fit within maxWidth
            if (firstWidth <= maxWidth && secondWidth <= maxWidth)
            {
                float difference = Math.Abs(firstWidth - secondWidth);
                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    bestSplit = i;
                }
            }
        }

        return bestSplit;
    }

    /// <summary>
    /// Truncates text and appends ellipsis to fit within specified width.
    /// Progressively removes characters from the end until text plus ellipsis fits.
    /// </summary>
    /// <param name="text">Text to truncate.</param>
    /// <param name="paint">Paint object for measuring text width.</param>
    /// <param name="maxWidth">Maximum allowed width.</param>
    /// <returns>Truncated text with ellipsis, or just ellipsis if no characters fit.</returns>
    // MARK: TruncateWithEllipsis
    public static string TruncateWithEllipsis(string text, SKPaint paint, float maxWidth)
    {
        const string ellipsis = "...";
        
        if (paint.MeasureText(text) <= maxWidth)
            return text;

        var ellipsisWidth = paint.MeasureText(ellipsis);
        var availableWidth = maxWidth - ellipsisWidth;

        // Remove characters from end until text fits with ellipsis
        for (int i = text.Length - 1; i >= 0; i--)
        {
            var substring = text.Substring(0, i);
            if (paint.MeasureText(substring) <= availableWidth)
            {
                return substring + ellipsis;
            }
        }

        return ellipsis;
    }

    /// <summary>
    /// Calculates the bounding rectangle for a block of text lines.
    /// </summary>
    /// <param name="lines">Read-only list of text lines.</param>
    /// <param name="paint">Paint object for measuring text.</param>
    /// <param name="fontSize">Font size in pixels.</param>
    /// <returns>Rectangle encompassing all text lines.</returns>
    // MARK: CalculateTextBounds
    private static SKRect CalculateTextBounds(IReadOnlyList<string> lines, SKPaint paint, int fontSize)
    {
        float maxWidth = 0;
        foreach (var line in lines)
        {
            var width = paint.MeasureText(line);
            if (width > maxWidth)
                maxWidth = width;
        }

        float lineHeight = fontSize * LineSpacingMultiplier;
        float totalHeight = (lines.Count - 1) * lineHeight + fontSize;
        
        return new SKRect(0, 0, maxWidth, totalHeight);
    }

    /// <summary>
    /// Draws multiple lines of text with shadow effects on the canvas.
    /// Each line is drawn with a shadow offset before drawing the main text.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="lines">List of text lines to draw.</param>
    /// <param name="alignmentX">Horizontal position for text alignment.</param>
    /// <param name="baseY">Vertical baseline position for first line.</param>
    /// <param name="fontSize">Font size in pixels for line spacing calculation.</param>
    /// <param name="titlePaint">Paint object for main text.</param>
    /// <param name="shadowPaint">Paint object for shadow text.</param>
    // MARK: DrawTextLines
    private static void DrawTextLines(
        SKCanvas canvas,
        IReadOnlyList<string> lines,
        float alignmentX,
        float baseY,
        int fontSize,
        SKPaint titlePaint,
        SKPaint shadowPaint)
    {
        float lineHeight = fontSize * LineSpacingMultiplier;
        float currentY = baseY;

        foreach (var line in lines)
        {
            // Draw shadow first, then main text
            canvas.DrawText(line, alignmentX + ShadowOffset, currentY + ShadowOffset, shadowPaint);
            canvas.DrawText(line, alignmentX, currentY, titlePaint);
            currentY += lineHeight;
        }
    }
}