using System;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

public class CutoutPosterGenerator
{
    public string? Generate(string inputImagePath, string outputPath, string episodeCode, string episodeTitle, PluginConfiguration config)
    {
        try
        {
            using var original = SKBitmap.Decode(inputImagePath);
            if (original == null)
                return null;

            var width = original.Width;
            var height = original.Height;
            var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            // Clear to transparent
            canvas.Clear(SKColors.Transparent);

            // Create and draw the overlay rectangle
            var overlayColor = ColorUtils.ParseHexColor(config.OverlayColor);
            using var overlayPaint = new SKPaint
            {
                Color = overlayColor,
                Style = SKPaintStyle.Fill,
                BlendMode = SKBlendMode.Src
            };
            canvas.DrawRect(SKRect.Create(width, height), overlayPaint);

            // Cut out transparent episode code using BlendMode.Clear
            using var cutoutPaint = new SKPaint
            {
                Color = SKColors.Transparent,
                BlendMode = SKBlendMode.Clear,
                IsAntialias = true,
                TextSize = width * 0.25f,
                Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold),
                TextAlign = SKTextAlign.Center
            };

            float centerX = width / 2f;
            float centerY = height / 2f + cutoutPaint.TextSize / 3;
            canvas.DrawText(episodeCode, centerX, centerY, cutoutPaint);

            // Draw original image behind the cutout
            using var originalPaint = new SKPaint
            {
                BlendMode = SKBlendMode.DstOver
            };
            canvas.DrawBitmap(original, 0, 0, originalPaint);

            // Draw episode title at bottom
            using var titlePaint = new SKPaint
            {
                Color = ColorUtils.ParseHexColor(config.TextColor),
                TextSize = config.TitleFontSize,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Normal),
                TextAlign = SKTextAlign.Center
            };

            float titleY = height - config.TitleFontSize - 40;
            canvas.DrawText(episodeTitle, centerX, titleY, titlePaint);

            // Save to file
            using var finalImage = surface.Snapshot();
            using var data = finalImage.Encode(SKEncodedImageFormat.Jpeg, 95);
            using var outputStream = File.OpenWrite(outputPath);
            data.SaveTo(outputStream);

            return outputPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Poster generation failed: {ex}");
            return null;
        }
    }
}