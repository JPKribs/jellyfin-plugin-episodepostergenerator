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
    // MARK: - Public Interface
    public string? Generate(SKBitmap canvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, string? outputPath)
    {
        try
        {
            // MARK: Temp Output Path
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

            // MARK: Layer 1 - Base Bitmap
            using var basePaint = new SKPaint { IsAntialias = true };
            skCanvas.DrawBitmap(canvas, 0, 0, basePaint);

            // MARK: Layer 2 - Overlay
            DrawOverlayLayer(skCanvas, width, height, config);

            // MARK: Layer 3 - Roman Numeral
            DrawRomanNumeral(skCanvas, episodeMetadata, config, width, height);

            // MARK: Layer 4 - Title
            if (config.ShowTitle && !string.IsNullOrEmpty(episodeMetadata.EpisodeName))
            {
                TextUtils.DrawTitle(skCanvas, episodeMetadata.EpisodeName, Position.Center, Alignment.Center, config, width, height);
            }

            // MARK: Encode and Save
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

    // MARK: - Overlay
    private void DrawOverlayLayer(SKCanvas canvas, int width, int height, PluginConfiguration config)
    {
        var overlayColor = ColorUtils.ParseHexColor(config.OverlayColor);
        if (overlayColor.Alpha > 0)
        {
            using var paint = new SKPaint { Color = overlayColor, Style = SKPaintStyle.Fill };
            canvas.DrawRect(SKRect.Create(width, height), paint);
        }
    }

    // MARK: - Roman Numeral
    private void DrawRomanNumeral(SKCanvas canvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int width, int height)
    {
        ApplySafeAreaConstraints(width, height, config, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);

        var numeralText = NumberUtils.NumberToRomanNumeral(episodeMetadata.EpisodeNumberStart ?? 0);
        var typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle));

        float fontSize = FontUtils.CalculateOptimalFontSize(numeralText, typeface, safeWidth, safeHeight);

        using var numeralPaint = new SKPaint
        {
            Color = ColorUtils.ParseHexColor(config.EpisodeFontColor),
            IsAntialias = true,
            TextSize = fontSize,
            Typeface = typeface,
            TextAlign = SKTextAlign.Center
        };

        // Center text in safe area
        float centerX = safeLeft + safeWidth / 2f;
        var bounds = FontUtils.MeasureTextDimensions(numeralText, typeface, fontSize);
        float centerY = safeTop + safeHeight / 2f + (bounds.Height / 2f);

        canvas.DrawText(numeralText, centerX, centerY, numeralPaint);
    }
}