using System;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Controller.Entities.TV;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

/// <summary>
/// Generates episode posters featuring Roman numerals for episode numbers with optional overlay and title display.
/// Creates elegant posters with large, centered Roman numeral episode numbers over background images or solid colors.
/// Supports background color overlays and optional episode title positioning.
/// Uses inherited safe area calculations for consistent margins across all poster generators.
/// </summary>
public class NumeralPosterGenerator : BasePosterGenerator, IPosterGenerator
{
    /// <summary>
    /// Generates a poster with a Roman numeral episode number and optional title.
    /// Creates a layered composition with the original image, color overlay, centered Roman numeral, and optional title.
    /// Roman numerals are automatically calculated and sized to fit optimally within the inherited safe area.
    /// Uses BasePosterGenerator safe area constraints for consistent positioning with other poster styles.
    /// </summary>
    /// <param name="inputImagePath">Path to the source image file to use as background.</param>
    /// <param name="outputPath">Path where the generated poster will be saved.</param>
    /// <param name="episode">Episode metadata containing episode number and title information.</param>
    /// <param name="config">Plugin configuration with styling, font, and color settings.</param>
    /// <returns>Path to the generated poster file, or null if generation fails.</returns>
    // MARK: Generate
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

            // Draw the original image as the base layer
            using var originalPaint = new SKPaint();
            canvas.DrawBitmap(original, 0, 0, originalPaint);

            // Apply background color overlay for better text visibility
            var overlayColor = ColorUtils.ParseHexColor(config.OverlayColor);
            using var overlayPaint = new SKPaint
            {
                Color = overlayColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(SKRect.Create(width, height), overlayPaint);

            // Calculate safe area using same logic as other generators
            var safeAreaMargin = config.PosterSafeArea / 100f;
            var marginX = width * safeAreaMargin;
            var marginY = height * safeAreaMargin;
            var safeLeft = marginX;
            var safeTop = marginY;
            var safeWidth = width - (2 * marginX);
            var safeHeight = height - (2 * marginY);

            // For numeral style, title overlays the numeral so use full safe area
            var numeralArea = new SKRect(safeLeft, safeTop, safeLeft + safeWidth, safeTop + safeHeight);

            // Convert episode number to Roman numeral text
            var numeralText = NumberUtils.NumberToRomanNumeral(episodeNumber);

            // Create typeface with configured font family and style
            var fontStyle = FontUtils.GetFontStyle(config.EpisodeFontStyle);
            using var typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, fontStyle);

            // Calculate optimal font size to fit within the numeral area
            float fontSize = FontUtils.CalculateOptimalFontSize(numeralText, typeface, numeralArea.Width, numeralArea.Height);

            // Create paint for rendering the Roman numeral
            using var numeralPaint = new SKPaint
            {
                Color = ColorUtils.ParseHexColor(config.EpisodeFontColor),
                IsAntialias = true,
                TextSize = fontSize,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center
            };

            // Position Roman numeral in the center of the numeral area
            float centerX = numeralArea.MidX;
            var bounds = FontUtils.MeasureTextDimensions(numeralText, typeface, fontSize);
            float numeralY = numeralArea.MidY + (bounds.Height / 2f);
            canvas.DrawText(numeralText, centerX, numeralY, numeralPaint);

            // Draw episode title if enabled - CENTERED OVER THE NUMERAL
            if (config.ShowTitle)
            {
                TextUtils.DrawTitle(canvas, episodeTitle, Position.Center, Alignment.Center, config, width, height);
            }

            // Encode and save the final image
            using var finalImage = surface.Snapshot();
            using var data = finalImage.Encode(SKEncodedImageFormat.Jpeg, 95);
            using var outputStream = File.OpenWrite(outputPath);
            data.SaveTo(outputStream);

            return outputPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Numeral poster generation failed: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }
}