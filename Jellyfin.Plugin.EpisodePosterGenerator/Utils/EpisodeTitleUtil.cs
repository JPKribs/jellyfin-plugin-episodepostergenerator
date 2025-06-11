using System;
using System.Collections.Generic;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

/// <summary>
/// Positions where the episode title can be drawn on the poster.
/// </summary>
public enum TitlePosition
{
    Top,
    Middle,
    Bottom
}

/// <summary>
/// Utility class for drawing episode titles onto posters.
/// </summary>
public static class EpisodeTitleUtil
{
    private const float SafeAreaMargin = 0.05f;
    private const float ShadowOffset = 2f;
    private const byte ShadowAlpha = 180;
    private const float LineSpacingMultiplier = 1.2f;
    private const float HorizontalPadding = 0.9f;

    /// <summary>
    /// Draws the episode title on the given canvas with shadow and wrapping according to config.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="title">Title text.</param>
    /// <param name="position">Vertical position for the title.</param>
    /// <param name="config">Plugin configuration with font settings.</param>
    /// <param name="canvasWidth">Canvas width in pixels.</param>
    /// <param name="canvasHeight">Canvas height in pixels.</param>
    public static void DrawTitle(
        SKCanvas canvas,
        string title,
        TitlePosition position,
        PluginConfiguration config,
        float canvasWidth,
        float canvasHeight)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, canvasHeight, (100f * SafeAreaMargin));

        using var titlePaint = CreateTextPaint(config.TitleFontColor, fontSize, config.TitleFontFamily, config.TitleFontStyle);
        using var shadowPaint = CreateShadowPaint(fontSize, config.TitleFontFamily, config.TitleFontStyle);

        var safeArea = CalculateSafeArea(canvasWidth, canvasHeight);
        var maxTextWidth = safeArea.Width * HorizontalPadding;
        
        var lines = WrapText(title, titlePaint, maxTextWidth);
        var textBounds = CalculateTextBounds(lines, titlePaint, fontSize);
        
        var centerX = canvasWidth / 2f;
        var baseY = CalculateBaseY(position, safeArea, textBounds.Height, fontSize, titlePaint);
        
        DrawTextLines(canvas, lines, centerX, baseY, fontSize, titlePaint, shadowPaint);
    }

    // MARK: CreateTextPaint
    // Creates paint for the main title text.
    private static SKPaint CreateTextPaint(string hexColor, int fontSize, string fontFamily, string fontStyle)
    {
        return new SKPaint
        {
            Color = ColorUtils.ParseHexColor(hexColor),
            TextSize = fontSize,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(fontFamily, ParseFontStyle(fontStyle)),
            TextAlign = SKTextAlign.Center
        };
    }

    // MARK: CreateShadowPaint
    // Creates paint for the text shadow.
    private static SKPaint CreateShadowPaint(int fontSize, string fontFamily, string fontStyle)
    {
        return new SKPaint
        {
            Color = SKColors.Black.WithAlpha(ShadowAlpha),
            TextSize = fontSize,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(fontFamily, ParseFontStyle(fontStyle)),
            TextAlign = SKTextAlign.Center
        };
    }

    // MARK: ParseFontStyle
    // Parses string font style to SKFontStyle.
    private static SKFontStyle ParseFontStyle(string fontStyle)
    {
        return fontStyle?.ToLowerInvariant() switch
        {
            "normal" => SKFontStyle.Normal,
            "bold" => SKFontStyle.Bold,
            "italic" => SKFontStyle.Italic,
            "bolditalic" => new SKFontStyle(SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Italic),
            _ => SKFontStyle.Bold
        };
    }

    // MARK: CalculateSafeArea
    // Calculates a margin-inset area inside the canvas for safe drawing.
    private static SKRect CalculateSafeArea(float canvasWidth, float canvasHeight)
    {
        var marginX = canvasWidth * SafeAreaMargin;
        var marginY = canvasHeight * SafeAreaMargin;
        
        return new SKRect(
            marginX,
            marginY,
            canvasWidth - marginX,
            canvasHeight - marginY
        );
    }

    // MARK: WrapText
    // Wraps and truncates text to fit within maxWidth, splitting into up to two lines.
    private static List<string> WrapText(string text, SKPaint paint, float maxWidth)
    {
        var lines = new List<string>();
        
        if (paint.MeasureText(text) <= maxWidth)
        {
            lines.Add(text);
            return lines;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (words.Length == 1)
        {
            lines.Add(TruncateWithEllipsis(text, paint, maxWidth));
            return lines;
        }

        string line1 = "";
        string line2 = "";
        
        int splitPoint = FindOptimalSplitPoint(words, paint, maxWidth);
        
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

        if (paint.MeasureText(line1) > maxWidth)
        {
            line1 = TruncateWithEllipsis(line1, paint, maxWidth);
        }
        
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

    // MARK: FindOptimalSplitPoint
    // Finds the best word index to split text into two lines of similar width under maxWidth.
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

    // MARK: TruncateWithEllipsis
    // Truncates text and appends ellipsis to fit within maxWidth.
    private static string TruncateWithEllipsis(string text, SKPaint paint, float maxWidth)
    {
        const string ellipsis = "...";
        
        if (paint.MeasureText(text) <= maxWidth)
            return text;

        var ellipsisWidth = paint.MeasureText(ellipsis);
        var availableWidth = maxWidth - ellipsisWidth;

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

    // MARK: CalculateTextBounds
    // Calculates bounding rectangle of all text lines combined.
    private static SKRect CalculateTextBounds(List<string> lines, SKPaint paint, int fontSize)
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

    // MARK: CalculateBaseY
    // Calculates the vertical start position for the text block based on position enum.
    private static float CalculateBaseY(TitlePosition position, SKRect safeArea, float textHeight, int fontSize, SKPaint paint)
    {
        var fontMetrics = paint.FontMetrics;
        float bottomPadding = fontSize * 0.5f;
        
        return position switch
        {
            TitlePosition.Top => safeArea.Top - fontMetrics.Ascent,
            TitlePosition.Middle => safeArea.MidY - (textHeight / 2f) - fontMetrics.Ascent,
            TitlePosition.Bottom => safeArea.Bottom - textHeight - bottomPadding - fontMetrics.Ascent,
            _ => safeArea.Bottom - textHeight - bottomPadding - fontMetrics.Ascent
        };
    }

    // MARK: DrawTextLines
    // Draws each text line with shadow offset then main text centered horizontally.
    private static void DrawTextLines(
        SKCanvas canvas,
        List<string> lines,
        float centerX,
        float baseY,
        int fontSize,
        SKPaint titlePaint,
        SKPaint shadowPaint)
    {
        float lineHeight = fontSize * LineSpacingMultiplier;
        float currentY = baseY;

        foreach (var line in lines)
        {
            canvas.DrawText(line, centerX + ShadowOffset, currentY + ShadowOffset, shadowPaint);
            canvas.DrawText(line, centerX, currentY, titlePaint);
            currentY += lineHeight;
        }
    }
}