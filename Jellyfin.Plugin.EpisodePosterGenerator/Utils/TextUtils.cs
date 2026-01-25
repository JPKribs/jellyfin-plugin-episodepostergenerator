using System;
using System.Collections.Generic;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils;

public static class TextUtils
{
    private const float ShadowOffset = 2f;
    private const byte ShadowAlpha = 180;
    private const float LineSpacingMultiplier = 1.2f;

    // DrawTitle
    // Renders title text on a canvas with automatic wrapping, positioning, and shadow effects.
    public static void DrawTitle(
        SKCanvas canvas,
        string title,
        Position position,
        Alignment alignment,
        PosterSettings settings,
        float canvasWidth,
        float canvasHeight)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        var fontSize = FontUtils.CalculateFontSizeFromPercentage(settings.TitleFontSize, canvasHeight, settings.PosterSafeArea);
        var typeface = FontUtils.CreateTypeface(settings.TitleFontFamily, FontUtils.GetFontStyle(settings.TitleFontStyle));

        using var titlePaint = CreateTextPaint(settings.TitleFontColor, fontSize, typeface, alignment);
        using var shadowPaint = CreateShadowPaint(fontSize, typeface, alignment);

        var safeArea = CalculateSafeArea(canvasWidth, canvasHeight, settings);
        var safeAreaMargin = settings.PosterSafeArea / 100f;
        var horizontalPadding = 1.0f - (2 * safeAreaMargin);
        var maxTextWidth = safeArea.Width * horizontalPadding;
        var lines = FitTextToWidth(title, titlePaint, maxTextWidth);
        var textBounds = CalculateTextBounds(lines, titlePaint, fontSize);
        var alignmentX = CalculateAlignmentX(alignment, canvasWidth, safeArea);
        var baseY = CalculateBaseY(position, safeArea, textBounds.Height, fontSize, titlePaint);

        DrawTextLines(canvas, lines, alignmentX, baseY, fontSize, titlePaint, shadowPaint);
    }

    // FitTextToWidth
    // Fits text within width constraints using wrapping and ellipsis truncation.
    public static IReadOnlyList<string> FitTextToWidth(string text, SKPaint paint, float maxWidth)
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

    // CreateTextPaint
    // Creates a configured SKPaint object for main text rendering.
    private static SKPaint CreateTextPaint(string hexColor, int fontSize, SKTypeface typeface, Alignment alignment)
    {
        return new SKPaint
        {
            Color = ColorUtils.ParseHexColor(hexColor),
            TextSize = fontSize,
            IsAntialias = true,
            SubpixelText = true,
            LcdRenderText = true,
            Typeface = typeface,
            TextAlign = GetSKTextAlign(alignment)
        };
    }

    // CreateShadowPaint
    // Creates a configured SKPaint object for drop shadow text rendering.
    private static SKPaint CreateShadowPaint(int fontSize, SKTypeface typeface, Alignment alignment)
    {
        return new SKPaint
        {
            Color = SKColors.Black.WithAlpha(ShadowAlpha),
            TextSize = fontSize,
            IsAntialias = true,
            SubpixelText = true,
            LcdRenderText = true,
            Typeface = typeface,
            TextAlign = GetSKTextAlign(alignment),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 1.5f)
        };
    }

    // GetSKTextAlign
    // Converts plugin alignment enum to SkiaSharp text alignment.
    private static SKTextAlign GetSKTextAlign(Alignment alignment)
    {
        return alignment switch
        {
            Alignment.Left => SKTextAlign.Left,
            Alignment.Center => SKTextAlign.Center,
            Alignment.Right => SKTextAlign.Right,
            _ => SKTextAlign.Center
        };
    }

    // CalculateAlignmentX
    // Calculates horizontal pixel position for text based on alignment.
    private static float CalculateAlignmentX(Alignment alignment, float canvasWidth, SKRect safeArea)
    {
        return alignment switch
        {
            Alignment.Left => safeArea.Left,
            Alignment.Center => canvasWidth / 2f,
            Alignment.Right => safeArea.Right,
            _ => canvasWidth / 2f
        };
    }

    // CalculateBaseY
    // Calculates vertical baseline position using font metrics and positioning preferences.
    private static float CalculateBaseY(Position position, SKRect safeArea, float textHeight, int fontSize, SKPaint paint)
    {
        var fontMetrics = paint.FontMetrics;
        float bottomPadding = fontSize * 0.5f;

        return position switch
        {
            Position.Top => safeArea.Top - fontMetrics.Ascent,
            Position.Center => safeArea.MidY - (textHeight / 2f) - fontMetrics.Ascent,
            Position.Bottom => safeArea.Bottom - textHeight - bottomPadding - fontMetrics.Ascent,
            _ => safeArea.Bottom - textHeight - bottomPadding - fontMetrics.Ascent
        };
    }

    // CalculateSafeArea
    // Calculates the safe drawing area within canvas boundaries accounting for margins.
    private static SKRect CalculateSafeArea(float canvasWidth, float canvasHeight, PosterSettings settings)
    {
        var safeAreaMargin = settings.PosterSafeArea / 100f;
        var marginX = canvasWidth * safeAreaMargin;
        var marginY = canvasHeight * safeAreaMargin;

        return new SKRect(
            marginX,
            marginY,
            canvasWidth - marginX,
            canvasHeight - marginY
        );
    }

    // FindOptimalSplitPoint
    // Finds the optimal word split point for balanced two-line text layouts.
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

    // TruncateWithEllipsis
    // Truncates text and appends ellipsis to fit within width constraints.
    public static string TruncateWithEllipsis(string text, SKPaint paint, float maxWidth)
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

    // CalculateTextBounds
    // Calculates the bounding rectangle for a collection of text lines.
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

    // DrawTextLines
    // Renders multiple lines of text with drop shadow effects on the canvas.
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
            canvas.DrawText(line, alignmentX + ShadowOffset, currentY + ShadowOffset, shadowPaint);
            canvas.DrawText(line, alignmentX, currentY, titlePaint);
            currentY += lineHeight;
        }
    }
}
