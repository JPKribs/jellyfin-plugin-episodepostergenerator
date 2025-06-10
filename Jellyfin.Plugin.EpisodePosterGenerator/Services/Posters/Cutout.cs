using System;
using System.Globalization;
using System.IO;
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

            DrawCutoutTextOptimized(canvas, episodeWords, width, height);

            using var originalPaint = new SKPaint
            {
                BlendMode = SKBlendMode.DstOver
            };
            canvas.DrawBitmap(original, 0, 0, originalPaint);

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
            CutoutType.Text => episodeNumber >= 100 ? episodeNumber.ToString(CultureInfo.InvariantCulture) : NumberUtils.NumberToWords(episodeNumber),
            CutoutType.Code => episodeNumber >= 100 ? $"S{seasonNumber:D2}E{episodeNumber}" : $"S{seasonNumber:D2}E{episodeNumber:D2}",
            _ => episodeNumber >= 100 ? episodeNumber.ToString(CultureInfo.InvariantCulture) : NumberUtils.NumberToWords(episodeNumber)
        };
    }

    // MARK: DrawCutoutTextOptimized
    private void DrawCutoutTextOptimized(SKCanvas canvas, string[] episodeWords, float canvasWidth, float canvasHeight)
    {
        if (episodeWords.Length == 0)
            return;

        float horizontalPadding = canvasWidth * 0.05f;
        float maxWidth = canvasWidth - 2 * horizontalPadding;
        float maxHeight = canvasHeight * 0.9f;

        using var typeface = FontUtils.GetCondensedTypeface();
        using var cutoutPaint = new SKPaint
        {
            Color = SKColors.Transparent,
            BlendMode = SKBlendMode.Clear,
            IsAntialias = true,
            Typeface = typeface,
            TextAlign = SKTextAlign.Center
        };

        if (episodeWords.Length == 1)
        {
            DrawSingleWordOptimized(canvas, episodeWords[0], cutoutPaint, canvasWidth, canvasHeight, maxWidth, maxHeight);
        }
        else
        {
            DrawMultipleWordsOptimized(canvas, episodeWords, cutoutPaint, canvasWidth, canvasHeight, maxWidth, maxHeight);
        }
    }

    // MARK: DrawSingleWordOptimized
    private void DrawSingleWordOptimized(SKCanvas canvas, string word, SKPaint cutoutPaint, float canvasWidth, float canvasHeight, float maxWidth, float maxHeight)
    {
        float fontSize = FontUtils.CalculateOptimalFontSize(word, cutoutPaint.Typeface!, maxWidth, maxHeight, 50f);
        
        float aspectRatio = maxWidth / maxHeight;
        if (aspectRatio < 1.5f)
        {
            fontSize = Math.Min(fontSize, maxHeight * 0.7f);
        }

        cutoutPaint.TextSize = fontSize;
        
        var bounds = FontUtils.MeasureTextDimensions(word, cutoutPaint.Typeface!, fontSize);
        float centerX = canvasWidth / 2f;
        float centerY = (canvasHeight / 2f) + (bounds.Height / 2f);

        canvas.DrawText(word, centerX, centerY, cutoutPaint);
    }

    // MARK: DrawMultipleWordsOptimized
    private void DrawMultipleWordsOptimized(SKCanvas canvas, string[] words, SKPaint cutoutPaint, float canvasWidth, float canvasHeight, float maxWidth, float maxHeight)
    {
        float lineSpacing = 1.1f;
        float targetHeight = maxHeight / words.Length;
        
        float fontSize = Math.Min(targetHeight / lineSpacing, maxWidth * 0.2f);
        const float minFontSize = 30f;

        while (fontSize > minFontSize)
        {
            float maxWordWidth = 0;
            foreach (var word in words)
            {
                var bounds = FontUtils.MeasureTextDimensions(word, cutoutPaint.Typeface!, fontSize);
                if (bounds.Width > maxWordWidth)
                    maxWordWidth = bounds.Width;
            }

            float totalHeight = words.Length * fontSize * lineSpacing;
            
            if (maxWordWidth <= maxWidth && totalHeight <= maxHeight)
                break;

            fontSize -= 3f;
        }

        cutoutPaint.TextSize = fontSize;
        float lineHeight = fontSize * lineSpacing;
        float totalTextHeight = words.Length * lineHeight;
        float startY = (canvasHeight - totalTextHeight) / 2f + fontSize;
        float centerX = canvasWidth / 2f;

        foreach (var word in words)
        {
            canvas.DrawText(word, centerX, startY, cutoutPaint);
            startY += lineHeight;
        }
    }
}