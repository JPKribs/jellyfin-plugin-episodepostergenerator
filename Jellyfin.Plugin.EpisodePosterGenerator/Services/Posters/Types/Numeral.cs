using System;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

/// <summary>
/// Generates episode posters featuring Roman numerals for episode numbers with optional overlay and title display.
/// </summary>
public class NumeralPosterGenerator : BasePosterGenerator, IPosterGenerator
{
    public string? Generate(SKBitmap canvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, string? outputPath)
    {
        try
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(
                    Path.GetTempPath(),
                    $"{Guid.NewGuid()}.{config.PosterFileType.ToString().ToLowerInvariant()}");
            }

            int width = canvas.Width;
            int height = canvas.Height;

            var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var skCanvas = surface.Canvas;
            skCanvas.Clear(SKColors.Transparent);

            // Draw the input bitmap as base layer
            using var basePaint = new SKPaint { IsAntialias = true };
            skCanvas.DrawBitmap(canvas, 0, 0, basePaint);

            // Apply overlay if configured
            var overlayColor = ColorUtils.ParseHexColor(config.OverlayColor);
            if (overlayColor.Alpha > 0)
            {
                using var overlayPaint = new SKPaint { Color = overlayColor, Style = SKPaintStyle.Fill };
                skCanvas.DrawRect(SKRect.Create(width, height), overlayPaint);
            }

            // Calculate safe area
            ApplySafeAreaConstraints(width, height, config, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);

            // Determine numeral area (full safe area)
            var numeralArea = new SKRect(safeLeft, safeTop, safeLeft + safeWidth, safeTop + safeHeight);

            // Convert episode number to Roman numeral
            var numeralText = NumberUtils.NumberToRomanNumeral(episodeMetadata.EpisodeNumberStart ?? 0);

            // Create typeface
            var typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle));

            // Calculate optimal font size
            float fontSize = FontUtils.CalculateOptimalFontSize(numeralText, typeface, numeralArea.Width, numeralArea.Height);

            using var numeralPaint = new SKPaint
            {
                Color = ColorUtils.ParseHexColor(config.EpisodeFontColor),
                IsAntialias = true,
                TextSize = fontSize,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center
            };

            // Center Roman numeral in numeral area
            float centerX = numeralArea.MidX;
            var bounds = FontUtils.MeasureTextDimensions(numeralText, typeface, fontSize);
            float numeralY = numeralArea.MidY + (bounds.Height / 2f);
            skCanvas.DrawText(numeralText, centerX, numeralY, numeralPaint);

            // Draw episode title if enabled
            if (config.ShowTitle && !string.IsNullOrEmpty(episodeMetadata.EpisodeName))
            {
                TextUtils.DrawTitle(skCanvas, episodeMetadata.EpisodeName, Position.Center, Alignment.Center, config, width, height);
            }

            // Encode and save final image
            using var finalImage = surface.Snapshot();
            using var data = finalImage.Encode(config.PosterFileType switch
            {
                PosterFileType.JPEG => SKEncodedImageFormat.Jpeg,
                PosterFileType.PNG => SKEncodedImageFormat.Png,
                PosterFileType.WEBP => SKEncodedImageFormat.Webp,
                PosterFileType.GIF => SKEncodedImageFormat.Gif,
                _ => SKEncodedImageFormat.Jpeg
            }, 95);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

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