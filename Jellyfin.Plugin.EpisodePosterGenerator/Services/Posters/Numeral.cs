using System;
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
            {
                Console.WriteLine("Failed to decode input image.");
                return null;
            }

            var width = original.Width;
            var height = original.Height;
            var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            canvas.Clear(SKColors.Transparent);

            // Draw original image as the base
            using var originalPaint = new SKPaint();
            canvas.DrawBitmap(original, 0, 0, originalPaint);

            // Draw color overlay
            var overlayColor = ColorUtils.ParseHexColor(config.BackgroundColor);
            using var overlayPaint = new SKPaint
            {
                Color = overlayColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(SKRect.Create(width, height), overlayPaint);

            ApplySafeAreaConstraints(width, height, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);

            var numeralText = NumberUtils.NumberToRomanNumeral(episodeNumber);
            
            var fontStyle = FontUtils.GetFontStyle(config.EpisodeFontStyle);
            
            using var typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, fontStyle);
            
            float fontSize = FontUtils.CalculateOptimalFontSize(numeralText, typeface, safeWidth, safeHeight);


            using var numeralPaint = new SKPaint
            {
                Color = ColorUtils.ParseHexColor(config.EpisodeFontColor),
                IsAntialias = true,
                TextSize = fontSize,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center
            };
            
            float centerX = width / 2f;
            // Vertically center the numeral text
            float numeralY = (height / 2f) + (fontSize * 0.35f);
            canvas.DrawText(numeralText, centerX, numeralY, numeralPaint);

            if (config.ShowTitle)
            {
                // Assuming EpisodeTitleUtil is also updated or doesn't need this specific typeface
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
            // It's better to log the full exception for debugging.
            Console.WriteLine($"Numeral poster generation failed: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }
}
