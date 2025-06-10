using System;
using System.Globalization;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using MediaBrowser.Controller.Entities.TV;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

public class CutoutPosterGenerator : BasePosterGenerator, IPosterGenerator
{
    private static readonly char[] WordSeparators = { ' ', '-' };

    // MARK: Generate
    public string? Generate(string inputImagePath, string outputPath, Episode episode, PluginConfiguration config)
    {
        try
        {
            var seasonNumber = episode.ParentIndexNumber ?? 0;
            var episodeNumber = episode.IndexNumber ?? 0;
            var episodeTitle = episode.Name ?? "-";
            
            var episodeText = GetCutoutText(config.CutoutType, seasonNumber, episodeNumber);
            var episodeWords = episodeText.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);

            using var original = SKBitmap.Decode(inputImagePath);
            if (original == null)
                return null;

            var width = original.Width;
            var height = original.Height;
            var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            canvas.Clear(SKColors.Transparent);

            var overlayColor = ColorUtils.ParseHexColor(config.BackgroundColor);
            using var overlayPaint = new SKPaint
            {
                Color = overlayColor,
                Style = SKPaintStyle.Fill,
                BlendMode = SKBlendMode.Src
            };
            canvas.DrawRect(SKRect.Create(width, height), overlayPaint);

            DrawCutoutText(canvas, episodeWords, width, height);

            using var originalPaint = new SKPaint
            {
                BlendMode = SKBlendMode.DstOver
            };
            canvas.DrawBitmap(original, 0, 0, originalPaint);

            if (config.ShowTitle)
            {
                EpisodeTitleUtil.DrawTitle(canvas, episodeTitle, TitlePosition.Bottom, config, width, height);
            }

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
    private void DrawCutoutText(SKCanvas canvas, string[] episodeWords, float canvasWidth, float canvasHeight)
    {
        if (episodeWords.Length == 0)
            return;

        ApplySafeAreaConstraints((int)canvasWidth, (int)canvasHeight, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);

        using var typeface = FontUtils.GetCondensedTypeface();
        using var cutoutPaint = new SKPaint
        {
            Color = SKColors.Transparent,
            BlendMode = SKBlendMode.Clear,
            IsAntialias = true,
            Typeface = typeface,
            TextAlign = SKTextAlign.Center
        };

        // Use more vertical space for the cutout text
        float maxTextHeight = safeHeight * 0.7f; // Increased from 0.6f

        if (episodeWords.Length == 1)
        {
            DrawSingleWord(canvas, episodeWords[0], cutoutPaint, canvasWidth, canvasHeight, safeWidth, maxTextHeight, safeLeft);
        }
        else
        {
            DrawMultipleWords(canvas, episodeWords, cutoutPaint, canvasWidth, canvasHeight, safeWidth, maxTextHeight, safeLeft);
        }
    }

    // MARK: DrawSingleWordOptimized
    private void DrawSingleWord(SKCanvas canvas, string word, SKPaint cutoutPaint, float canvasWidth, float canvasHeight, float maxWidth, float maxHeight, float safeLeft)
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
        float centerY = (canvasHeight * 0.4f) + (bounds.Height / 2f);

        canvas.DrawText(word, centerX, centerY, cutoutPaint);
    }

    // MARK: DrawMultipleWordsOptimized
    private void DrawMultipleWords(SKCanvas canvas, string[] words, SKPaint cutoutPaint, float canvasWidth, float canvasHeight, float maxWidth, float maxHeight, float safeLeft)
    {
        float lineSpacing = 1.1f;
        float targetHeight = maxHeight / (words.Length * lineSpacing);
        
        float fontSize = Math.Min(targetHeight, maxWidth * 0.2f);
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
        float totalTextHeight = words.Length * lineHeight - (lineHeight - fontSize); // Subtract extra spacing from last line
        
        // Center the text block vertically
        float blockCenterY = canvasHeight * 0.4f;
        float startY = blockCenterY - (totalTextHeight / 2f) + fontSize;
        float centerX = canvasWidth / 2f;

        foreach (var word in words)
        {
            canvas.DrawText(word, centerX, startY, cutoutPaint);
            startY += lineHeight;
        }
    }
}