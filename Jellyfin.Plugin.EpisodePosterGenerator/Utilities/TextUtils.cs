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

        var abbreviation = FitAbbreviation(AbbreviateTitle(title), paint, maxWidth);
        return abbreviation != null ? new[] { abbreviation } : Array.Empty<string>();
    }

    // FitTitleLines
    // Height-aware variant: fits the title to the width, then checks the resulting block against
    // the vertical space it has been given and re-fits if it would overflow.
    //
    // The width-only overload can return two lines for a slot that only has room for one, which is
    // why styles used to reserve a fixed two lines whether or not two were used — a guess that left
    // a hole when the title was short and still overflowed when it was not. Passing the real height
    // lets long title handling engage on vertical overflow the same way it does on horizontal.
    public static IReadOnlyList<string> FitTitleLines(
        string title,
        SKPaint paint,
        float maxWidth,
        float maxHeight,
        float lineHeight,
        LongTitleHandling handling)
    {
        var lines = FitTitleLines(title, paint, maxWidth, handling);
        if (lines.Count == 0 || lineHeight <= 0f || maxHeight <= 0f)
        {
            return lines;
        }

        // A run of n lines occupies fontSize + (n-1) * lineHeight, not n * lineHeight: the first
        // line contributes only its own height, and each line after it adds the leading. Dividing
        // the block height by lineHeight undercounts and would refuse the last line that fits.
        var fontSize = paint.TextSize;
        var slack = lineHeight * 0.01f;
        var maxLines = maxHeight + slack < fontSize
            ? 1
            : 1 + (int)Math.Floor((maxHeight - fontSize + slack) / lineHeight);

        if (lines.Count <= maxLines)
        {
            return lines;
        }

        // Too tall. Ellipsis keeps as many lines as fit and trims the last; the other modes are
        // asking for a shorter title, so re-run them against the width a single line really has.
        if (handling == LongTitleHandling.Ellipsis)
        {
            var kept = lines.Take(maxLines).ToList();
            kept[^1] = TruncateWithEllipsis(kept[^1] + "…", paint, maxWidth);
            return kept;
        }

        var single = FitTitleLine(title, paint, maxWidth * maxLines, handling);
        if (single == null)
        {
            return Array.Empty<string>();
        }

        var refit = FitTitleLines(single, paint, maxWidth, handling);
        return refit.Count <= maxLines ? refit : refit.Take(maxLines).ToList();
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
    // the first and the last. Returns null when even the shortest form does
    // not fit, so the caller drops the title entirely.
    public static string? FitAbbreviation(string abbreviation, SKPaint paint, float maxWidth)
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

        var reduced = string.Concat(units);
        return reduced.Length > 0 && paint.MeasureText(reduced) <= maxWidth ? reduced : null;
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
}
