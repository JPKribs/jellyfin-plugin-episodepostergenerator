using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using MediaBrowser.Controller.Entities.TV;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

public class NumeralPosterGenerator : BasePosterGenerator, IPosterGenerator
{
    // MARK: Generate
    public string? Generate(string inputImagePath, string outputPath, Episode episode, PluginConfiguration config)
    {
        try
        {
            var episodeNumber = episode.IndexNumber ?? 0;
            var episodeTitle = episode.Name ?? "-";
            using var original = SKBitmap.Decode(inputImagePath);
            if (original == null)
                return null;

            var width = original.Width;
            var height = original.Height;
            var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            canvas.Clear(SKColors.Transparent);

            using var originalPaint = new SKPaint();
            canvas.DrawBitmap(original, 0, 0, originalPaint);

            var overlayColor = ColorUtils.ParseHexColor(config.BackgroundColor);
            using var overlayPaint = new SKPaint
            {
                Color = overlayColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(SKRect.Create(width, height), overlayPaint);

            ApplySafeAreaConstraints(width, height, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);

            var numeralText = ConvertToRomanNumeral(episodeNumber);
            float numeralArea = config.ShowTitle ? safeHeight * 0.7f : safeHeight * 0.9f;
            float fontSize = CalculateOptimalFontSize(numeralText, safeWidth * 0.9f, numeralArea);

            using var numeralPaint = new SKPaint
            {
                Color = ColorUtils.ParseHexColor(config.EpisodeFontColor),
                IsAntialias = true,
                TextSize = fontSize,
                Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold),
                TextAlign = SKTextAlign.Center
            };

            float centerX = safeLeft + (safeWidth / 2f);
            float numeralCenterY = config.ShowTitle ? safeTop + (numeralArea / 2f) : safeTop + (safeHeight / 2f);
            float numeralY = numeralCenterY + (fontSize * 0.35f);

            canvas.DrawText(numeralText, centerX, numeralY, numeralPaint);

            if (config.ShowTitle)
            {
                DrawEpisodeTitle(canvas, episodeTitle, config, centerX, safeTop + safeHeight, safeWidth);
            }

            using var finalImage = surface.Snapshot();
            using var data = finalImage.Encode(SKEncodedImageFormat.Jpeg, 95);
            using var outputStream = File.OpenWrite(outputPath);
            data.SaveTo(outputStream);

            return outputPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Numeral poster generation failed: {ex}");
            return null;
        }
    }

    // MARK: ConvertToRomanNumeral
    private string ConvertToRomanNumeral(int number)
    {
        if (number <= 0 || number > 3999)
            return number.ToString(CultureInfo.InvariantCulture);

        string[] thousands = { "", "M", "MM", "MMM" };
        string[] hundreds = { "", "C", "CC", "CCC", "CD", "D", "DC", "DCC", "DCCC", "CM" };
        string[] tens = { "", "X", "XX", "XXX", "XL", "L", "LX", "LXX", "LXXX", "XC" };
        string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };

        return thousands[number / 1000] +
               hundreds[(number % 1000) / 100] +
               tens[(number % 100) / 10] +
               ones[number % 10];
    }

    // MARK: CalculateOptimalFontSize
    private float CalculateOptimalFontSize(string text, float maxWidth, float maxHeight)
    {
        float fontSize = Math.Min(maxWidth, maxHeight) * 0.6f;
        const float minFontSize = 100f;

        using var measurePaint = new SKPaint
        {
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold),
        };

        while (fontSize > minFontSize)
        {
            measurePaint.TextSize = fontSize;
            var textWidth = measurePaint.MeasureText(text);
            var textHeight = fontSize;

            if (textWidth <= maxWidth && textHeight <= maxHeight)
                break;

            fontSize -= 10f;
        }

        return fontSize;
    }

    // MARK: DrawEpisodeTitle
    private void DrawEpisodeTitle(SKCanvas canvas, string episodeTitle, PluginConfiguration config, float centerX, float bottomY, float safeWidth)
    {
        using var titlePaint = new SKPaint
        {
            Color = ColorUtils.ParseHexColor(config.TitleFontColor),
            TextSize = config.TitleFontSize,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };

        using var shadowPaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(180),
            TextSize = config.TitleFontSize,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };

        float maxWidth = safeWidth * 0.8f;
        var lines = WrapTextForOverlay(episodeTitle, titlePaint, maxWidth);
        
        float lineHeight = config.TitleFontSize * 1.2f;
        float textOffset = config.TitleFontSize * 0.35f;
        float titleY = bottomY - (config.TitleFontSize * 1.5f);
        
        if (lines.Count == 1)
        {
            float y = titleY + textOffset;
            canvas.DrawText(lines[0], centerX + 2, y + 2, shadowPaint);
            canvas.DrawText(lines[0], centerX, y, titlePaint);
        }
        else if (lines.Count == 2)
        {
            float line1Y = titleY - (lineHeight / 2f) + textOffset;
            float line2Y = titleY + (lineHeight / 2f) + textOffset;
            
            canvas.DrawText(lines[0], centerX + 2, line1Y + 2, shadowPaint);
            canvas.DrawText(lines[0], centerX, line1Y, titlePaint);
            
            canvas.DrawText(lines[1], centerX + 2, line2Y + 2, shadowPaint);
            canvas.DrawText(lines[1], centerX, line2Y, titlePaint);
        }
    }

    // MARK: WrapTextForOverlay
    private List<string> WrapTextForOverlay(string text, SKPaint paint, float maxWidth)
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
        
        int splitPoint = words.Length / 2;
        
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

        while (paint.MeasureText(line1) > maxWidth && line1.Contains(' ', StringComparison.Ordinal))
        {
            var lastSpace = line1.LastIndexOf(' ');
            var movedWord = line1.Substring(lastSpace + 1);
            line1 = line1.Substring(0, lastSpace);
            line2 = movedWord + " " + line2;
        }

        while (paint.MeasureText(line2) > maxWidth && line2.Contains(' ', StringComparison.Ordinal))
        {
            var firstSpace = line2.IndexOf(' ', StringComparison.Ordinal);
            var movedWord = line2.Substring(0, firstSpace);
            line2 = line2.Substring(firstSpace + 1);
            line1 += " " + movedWord;
        }

        if (paint.MeasureText(line1) > maxWidth)
        {
            line1 = TruncateWithEllipsis(line1, paint, maxWidth);
        }
        
        if (paint.MeasureText(line2) > maxWidth)
        {
            line2 = TruncateWithEllipsis(line2, paint, maxWidth);
        }

        lines.Add(line1);
        if (!string.IsNullOrWhiteSpace(line2))
        {
            lines.Add(line2);
        }

        return lines;
    }

    // MARK: TruncateWithEllipsis
    private string TruncateWithEllipsis(string text, SKPaint paint, float maxWidth)
    {
        if (paint.MeasureText(text) <= maxWidth)
            return text;

        var ellipsis = "...";
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