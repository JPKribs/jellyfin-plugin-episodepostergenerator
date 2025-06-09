using System;
using System.Globalization;
using System.IO;
using J2N.Numerics;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

public class CutoutPosterGenerator
{
    public string? Generate(string inputImagePath, string outputPath, int episodeNumber, string episodeTitle, PluginConfiguration config)
    {
        try
        {
            var episodeWords = NumberUtils.NumberToWords(episodeNumber).Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);

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

            // Draw overlay
            var overlayColor = ColorUtils.ParseHexColor(config.OverlayColor);
            using var overlayPaint = new SKPaint
            {
                Color = overlayColor,
                Style = SKPaintStyle.Fill,
                BlendMode = SKBlendMode.Src
            };
            canvas.DrawRect(SKRect.Create(width, height), overlayPaint);

            // Determine dynamic cutout font size
            float horizontalPadding = width * 0.1f; // 10% padding on each side
            float maxAllowedWidth = width - 2 * horizontalPadding;
            float initialSize = width * 0.25f;
            float fontSize = initialSize;
            const float minFontSize = 12f;

            using var measurePaint = new SKPaint
            {
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold),
            };

            while (fontSize > minFontSize)
            {
                measurePaint.TextSize = fontSize;
                float maxWordWidth = 0;
                foreach (var word in episodeWords)
                {
                    var wordWidth = measurePaint.MeasureText(word);
                    if (wordWidth > maxWordWidth)
                        maxWordWidth = wordWidth;
                }

                if (maxWordWidth <= maxAllowedWidth)
                    break;

                fontSize -= 1f;
            }

            // Cut out each word centered
            using var cutoutPaint = new SKPaint
            {
                Color = SKColors.Transparent,
                BlendMode = SKBlendMode.Clear,
                IsAntialias = true,
                TextSize = fontSize,
                Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold),
                TextAlign = SKTextAlign.Center
            };

            float totalTextHeight = episodeWords.Length * fontSize * 1.2f;
            float startY = (height - totalTextHeight) / 2f + fontSize;

            float centerX = width / 2f;

            foreach (var word in episodeWords)
            {
                canvas.DrawText(word, centerX, startY, cutoutPaint);
                startY += fontSize * 1.2f;
            }

            // Draw original image behind
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