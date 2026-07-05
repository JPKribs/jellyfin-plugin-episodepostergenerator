using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utilities;

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
        var typeface = FontUtils.ResolveTypeface(settings.EffectiveTitleFontPath, settings.TitleFontFamily, FontUtils.GetFontStyle(settings.TitleFontStyle));

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

    // Divider between title segments: a spaced dash of any kind, or a colon.
    private static readonly Regex SegmentSeparator = new Regex(@"(\s+[-–—]\s+|:\s*)", RegexOptions.Compiled);

    // FitTitleLines
    // Applies the configured long title handling and returns the lines to draw.
    // Returns an empty list when the handling drops a title that does not fit.
    public static IReadOnlyList<string> FitTitleLines(string title, SKPaint paint, float maxWidth, LongTitleHandling handling)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Array.Empty<string>();

        if (handling == LongTitleHandling.Ellipsis)
            return FitTextToWidth(title, paint, maxWidth);

        // Abbreviate and DropName only engage when the title would otherwise be cut:
        // a title that fits on one line, or wraps to two whole lines, renders as normal.
        if (TryFitWhole(title, paint, maxWidth, out var lines))
            return lines;

        if (handling == LongTitleHandling.DropName)
            return Array.Empty<string>();

        foreach (var candidate in ShorterCandidates(title))
        {
            if (TryFitWhole(candidate, paint, maxWidth, out var candidateLines))
                return candidateLines;
        }

        return new[] { FitAbbreviation(AbbreviateTitle(title), paint, maxWidth) };
    }

    // FitTitleLine
    // Single line variant of FitTitleLines for styles that cannot wrap.
    // Returns null when the handling drops a title that does not fit.
    public static string? FitTitleLine(string title, SKPaint paint, float maxWidth, LongTitleHandling handling)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        if (paint.MeasureText(title) <= maxWidth)
            return title;

        if (handling == LongTitleHandling.DropName)
            return null;

        if (handling == LongTitleHandling.Abbreviate)
        {
            foreach (var candidate in ShorterCandidates(title))
            {
                if (paint.MeasureText(candidate) <= maxWidth)
                    return candidate;
            }

            return FitAbbreviation(AbbreviateTitle(title), paint, maxWidth);
        }

        return TruncateWithEllipsis(title, paint, maxWidth);
    }

    // TryFitWhole
    // Returns true when the text fits untouched, either on one line or split
    // across two whole lines, and outputs those lines.
    private static bool TryFitWhole(string text, SKPaint paint, float maxWidth, out IReadOnlyList<string> lines)
    {
        if (paint.MeasureText(text) <= maxWidth)
        {
            lines = new[] { text };
            return true;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            int split = FindOptimalSplitPoint(words, paint, maxWidth);
            var line1 = string.Join(" ", words[..split]);
            var line2 = string.Join(" ", words[split..]);
            if (paint.MeasureText(line1) <= maxWidth && paint.MeasureText(line2) <= maxWidth)
            {
                lines = new[] { line1, line2 };
                return true;
            }
        }

        lines = Array.Empty<string>();
        return false;
    }

    // ShorterCandidates
    // Natural shorter forms tried before abbreviating: the text before a
    // divider, then the first sentence.
    private static IEnumerable<string> ShorterCandidates(string title)
    {
        var left = LeftOfSeparator(title);
        if (left != null)
            yield return left;

        var sentence = FirstSentence(title);
        if (sentence != null)
            yield return sentence;
    }

    // LeftOfSeparator
    // Returns the text before the first divider (a spaced dash or a colon),
    // or null when the title has no divider. Hyphenated words do not count.
    public static string? LeftOfSeparator(string title)
    {
        var match = SegmentSeparator.Match(title);
        if (!match.Success || match.Index == 0)
            return null;

        var left = title[..match.Index].Trim();
        return left.Length > 0 ? left : null;
    }

    // FirstSentence
    // Returns the first sentence including its punctuation, or null when the
    // title is a single sentence. Very short fragments such as "Mr." are not
    // treated as sentences.
    public static string? FirstSentence(string title)
    {
        for (int i = 0; i < title.Length - 1; i++)
        {
            var c = title[i];
            if ((c == '!' || c == '?' || c == '.') && char.IsWhiteSpace(title[i + 1]))
            {
                var sentence = title[..(i + 1)].Trim();
                if (sentence.Length > 3 && sentence.Length < title.Trim().Length)
                    return sentence;
            }
        }

        return null;
    }

    // AbbreviateTitle
    // Reduces a title to the first letter of every word with a period after
    // each, so "Lord of the Ring" becomes "L.O.T.R.". Dividers are kept
    // between segments, so "The Power that Burns Fire - Akainu's Final Move"
    // becomes "T.P.T.B.F. - A.F.M.". Every word contributes regardless of
    // case, so all lowercase titles abbreviate too.
    public static string AbbreviateTitle(string title)
    {
        var parts = SegmentSeparator.Split(title);
        var pieces = new List<string>();
        string? pendingSeparator = null;

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
                continue;

            var trimmed = part.Trim();
            if (trimmed == "-" || trimmed == "–" || trimmed == "—")
            {
                pendingSeparator = " - ";
                continue;
            }

            if (trimmed == ":")
            {
                pendingSeparator = ": ";
                continue;
            }

            var abbreviated = AbbreviateWords(part);
            if (abbreviated.Length == 0)
                continue;

            if (pieces.Count > 0 && pendingSeparator != null)
                pieces.Add(pendingSeparator);

            pendingSeparator = null;
            pieces.Add(abbreviated);
        }

        return string.Concat(pieces);
    }

    // AbbreviateWords
    // The first letter of every word, uppercased, each followed by a period.
    private static string AbbreviateWords(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder(words.Length * 2);

        foreach (var word in words)
        {
            var letter = word.FirstOrDefault(char.IsLetterOrDigit);
            if (letter == default(char))
                continue;

            sb.Append(char.ToUpperInvariant(letter));
            sb.Append('.');
        }

        return sb.ToString();
    }

    // FitAbbreviation
    // Shrinks an abbreviation that is still too wide: dividers collapse away
    // and middle initials drop one at a time until it fits, always keeping
    // the first and the last.
    public static string FitAbbreviation(string abbreviation, SKPaint paint, float maxWidth)
    {
        if (paint.MeasureText(abbreviation) <= maxWidth)
            return abbreviation;

        var units = new List<string>();
        foreach (var c in abbreviation)
        {
            if (char.IsLetterOrDigit(c))
                units.Add(string.Concat(c, "."));
        }

        while (units.Count > 2 && paint.MeasureText(string.Concat(units)) > maxWidth)
        {
            units.RemoveAt(units.Count / 2);
        }

        return string.Concat(units);
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

        int splitPoint = FindOptimalSplitPoint(words, paint, maxWidth);

        var line1 = string.Join(" ", words.Take(splitPoint));
        var line2 = string.Join(" ", words.Skip(splitPoint));

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
        // Same pixel margin on all sides, derived from the poster height.
        var margin = canvasHeight * (settings.PosterSafeArea / 100f);

        return new SKRect(
            margin,
            margin,
            canvasWidth - margin,
            canvasHeight - margin
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
        float maxWidth = lines.Count > 0 ? lines.Max(line => paint.MeasureText(line)) : 0;

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
