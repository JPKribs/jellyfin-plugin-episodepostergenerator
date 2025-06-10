using System;
using System.Globalization;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using MediaBrowser.Controller.Entities.TV;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

/// <summary>
/// Generates episode posters featuring Roman numerals for episode numbers.
/// </summary>
public class NumeralPosterGenerator : BasePosterGenerator, IPosterGenerator
{
    // MARK: Generate
    /// <summary>
    /// Generates a poster with a Roman numeral episode number and optional title.
    /// </summary>
    /// <param name="inputImagePath">Input image file path.</param>
    /// <param name="outputPath">Output image file path.</param>
    /// <param name="episode">Episode metadata.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <returns>Output path if successful; otherwise null.</returns>
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

            var numeralText = NumberUtils.NumberToRomanNumeral(episodeNumber);
            float centerX = width / 2f;

            float fontSize = CalculateOptimalFontSize(numeralText, safeWidth, safeHeight);

            using var numeralPaint = new SKPaint
            {
                Color = ColorUtils.ParseHexColor(config.EpisodeFontColor),
                IsAntialias = true,
                TextSize = fontSize,
                Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold),
                TextAlign = SKTextAlign.Center
            };

            float numeralY = (height / 2f) + (fontSize * 0.35f);
            canvas.DrawText(numeralText, centerX, numeralY, numeralPaint);

            if (config.ShowTitle)
            {
                EpisodeTitleUtil.DrawTitle(canvas, episodeTitle, TitlePosition.Middle, config, width, height);
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

    // MARK: CalculateOptimalFontSize
    /// <summary>
    /// Calculates the largest font size that fits the text within maxWidth and maxHeight.
    /// </summary>
    private float CalculateOptimalFontSize(string text, float maxWidth, float maxHeight)
    {
        float fontSize = Math.Min(maxWidth, maxHeight) * 0.8f;
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
}