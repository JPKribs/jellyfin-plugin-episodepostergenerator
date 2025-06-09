using System;
using System.Globalization;
using System.IO;
using J2N.Numerics;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

public class CutoutPosterGenerator
{
    private static readonly char[] WordSeparators = { ' ', '-' };

    // MARK: Generate
    public string? Generate(string inputImagePath, string outputPath, int seasonNumber, int episodeNumber, string episodeTitle, PluginConfiguration config)
    {
        try
        {
            var cutoutText = GetCutoutText(config.CutoutType, seasonNumber, episodeNumber);
            var episodeWords = cutoutText.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);

            using var original = SKBitmap.Decode(inputImagePath);
            if (original == null)
                return null;

            var width = original.Width;
            var height = original.Height;
            var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            canvas.Clear(SKColors.Transparent);

            var overlayColor = ColorUtils.ParseHexColor(config.OverlayColor);
            using var overlayPaint = new SKPaint
            {
                Color = overlayColor,
                Style = SKPaintStyle.Fill,
                BlendMode = SKBlendMode.Src
            };
            canvas.DrawRect(SKRect.Create(width, height), overlayPaint);

            // Calculate fixed reserved space for title at bottom (using actual config font size)
            float titleReservedHeight = config.TitleFontSize + 60f; // Fixed padding around title
            float availableHeightForCutout = height - titleReservedHeight;

            // EPISODE NUMBER: Maximize width usage with dynamic sizing
            // (Episode title below will use fixed config.TitleFontSize)

            // Maximize episode number width with minimal horizontal padding
            float horizontalPadding = width * 0.05f; // Reduced padding for maximum width
            float maxAllowedWidth = width - 2 * horizontalPadding;
            
            // Start with large font size and work down to fit both width and available height
            float fontSize = width * 0.4f; // Start much larger to maximize width usage
            const float minFontSize = 20f; // Higher minimum for better visibility

            using var measurePaint = new SKPaint
            {
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold),
            };

            // Find largest font size that fits all words within width and height constraints
            while (fontSize > minFontSize)
            {
                measurePaint.TextSize = fontSize;
                float maxWordWidth = 0;
                
                // Check if ALL words fit at this size
                foreach (var word in episodeWords)
                {
                    var wordWidth = measurePaint.MeasureText(word);
                    if (wordWidth > maxWordWidth)
                        maxWordWidth = wordWidth;
                }

                float totalTextHeight = episodeWords.Length * fontSize * 1.2f;
                
                // Both width AND height must fit, prioritizing width usage
                if (maxWordWidth <= maxAllowedWidth && totalTextHeight <= availableHeightForCutout * 0.85f)
                    break;

                fontSize -= 2f; // Larger decrements for faster sizing
            }

            // Create transparent cutout text that shows the original image through
            using var cutoutPaint = new SKPaint
            {
                Color = SKColors.Transparent,
                BlendMode = SKBlendMode.Clear, // This creates transparent holes
                IsAntialias = true,
                TextSize = fontSize, // Same size for all words
                Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold),
                TextAlign = SKTextAlign.Center
            };

            float finalTextHeight = episodeWords.Length * fontSize * 1.2f;
            float startY = (availableHeightForCutout - finalTextHeight) / 2f + fontSize;
            float centerX = width / 2f;

            foreach (var word in episodeWords)
            {
                canvas.DrawText(word, centerX, startY, cutoutPaint);
                startY += fontSize * 1.2f;
            }

            // Draw original image behind the overlay (shows through transparent text holes)
            using var originalPaint = new SKPaint
            {
                BlendMode = SKBlendMode.DstOver
            };
            canvas.DrawBitmap(original, 0, 0, originalPaint);

            // Draw episode title in the reserved space at bottom (fixed size)
            DrawEpisodeTitle(canvas, episodeTitle, width, height, config);

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

    // MARK: GetCutoutText
    private string GetCutoutText(CutoutType cutoutType, int seasonNumber, int episodeNumber)
    {
        return cutoutType switch
        {
            CutoutType.Text => NumberUtils.NumberToWords(episodeNumber),
            CutoutType.Code => $"S{seasonNumber:D2}E{episodeNumber:D2}",
            _ => NumberUtils.NumberToWords(episodeNumber)
        };
    }

    // MARK: DrawEpisodeTitle
    private void DrawEpisodeTitle(SKCanvas canvas, string episodeTitle, int width, int height, PluginConfiguration config)
    {
        float horizontalPadding = width * 0.05f;
        float titleY = height - config.TitleFontSize - 30f; // Fixed position based on actual font size

        using var titlePaint = new SKPaint
        {
            Color = ColorUtils.ParseHexColor(config.TextColor),
            TextSize = config.TitleFontSize, // Always use exact config size
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Normal),
            TextAlign = SKTextAlign.Center
        };

        canvas.DrawText(episodeTitle, width / 2f, titleY, titlePaint);
    }
}