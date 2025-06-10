using System;
using System.Globalization;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using MediaBrowser.Controller.Entities.TV;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

/// <summary>
/// Generates a poster with a "cutout" style overlay for a given TV episode.
/// Draws episode information (e.g., title or code) as transparent text cut out from a background overlay.
/// </summary>
public class CutoutPosterGenerator : BasePosterGenerator, IPosterGenerator
{
    /// <summary>
    /// Word separators used to split episode text into multiple lines.
    /// </summary>
    private static readonly char[] WordSeparators = { ' ', '-' };

    // MARK: Generate

    /// <summary>
    /// Generates a poster image using a cutout text overlay and saves it to the specified output path.
    /// </summary>
    /// <param name="inputImagePath">Path to the base input image.</param>
    /// <param name="outputPath">Path to save the generated poster.</param>
    /// <param name="episode">The episode metadata object.</param>
    /// <param name="config">Plugin configuration used to control visual settings.</param>
    /// <returns>Path to the saved poster image, or null if generation fails.</returns>
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

            DrawCutoutText(canvas, episodeWords, width, height, config.ShowTitle);

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

    /// <summary>
    /// Returns the episode text to be used in the cutout, formatted according to the selected CutoutType.
    /// </summary>
    private string GetCutoutText(CutoutType cutoutType, int seasonNumber, int episodeNumber)
    {
        return cutoutType switch
        {
            CutoutType.Text => episodeNumber >= 100 ? episodeNumber.ToString(CultureInfo.InvariantCulture) : NumberUtils.NumberToWords(episodeNumber),
            CutoutType.Code => episodeNumber >= 100 ? $"S{seasonNumber:D2}E{episodeNumber}" : $"S{seasonNumber:D2}E{episodeNumber:D2}",
            _ => episodeNumber >= 100 ? episodeNumber.ToString(CultureInfo.InvariantCulture) : NumberUtils.NumberToWords(episodeNumber)
        };
    }

    // MARK: DrawCutoutText

    /// <summary>
    /// Draws episode text (cutout style) on the canvas. Supports single or multi-line formatting.
    /// </summary>
    private void DrawCutoutText(SKCanvas canvas, string[] episodeWords, float canvasWidth, float canvasHeight, bool hasTitle)
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

        float maxTextHeight = safeHeight * 0.7f;

        if (episodeWords.Length == 1)
        {
            DrawSingleWord(canvas, episodeWords[0], cutoutPaint, canvasWidth, canvasHeight, safeWidth, maxTextHeight, safeLeft, hasTitle);
        }
        else
        {
            DrawMultipleWords(canvas, episodeWords, cutoutPaint, canvasWidth, canvasHeight, safeWidth, maxTextHeight, safeLeft, hasTitle);
        }
    }

    // MARK: DrawSingleWord

    /// <summary>
    /// Renders a single word centered on the canvas using a clear blend mode (cutout effect).
    /// </summary>
    private void DrawSingleWord(SKCanvas canvas, string word, SKPaint cutoutPaint, float canvasWidth, float canvasHeight, float maxWidth, float maxHeight, float safeLeft, bool hasTitle)
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
        float centerY = hasTitle
            ? (canvasHeight * 0.4f) + (bounds.Height / 2f)
            : (canvasHeight / 2f) + (bounds.Height / 2f);

        canvas.DrawText(word, centerX, centerY, cutoutPaint);
    }

    // MARK: DrawMultipleWords

    /// <summary>
    /// Renders multiple words centered and stacked vertically with spacing on the canvas.
    /// </summary>
    private void DrawMultipleWords(SKCanvas canvas, string[] words, SKPaint cutoutPaint, float canvasWidth, float canvasHeight, float maxWidth, float maxHeight, float safeLeft, bool hasTitle)
    {
        float lineSpacing = 1.1f;

        float usableHeight = hasTitle ? maxHeight : maxHeight * 0.95f;
        float targetHeight = usableHeight / (words.Length * lineSpacing);

        float fontSize = Math.Min(targetHeight, maxWidth * 0.25f);
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

            if (maxWordWidth <= maxWidth && totalHeight <= usableHeight)
                break;

            fontSize -= 3f;
        }

        cutoutPaint.TextSize = fontSize;
        float lineHeight = fontSize * lineSpacing;
        float totalTextHeight = words.Length * lineHeight - (lineHeight - fontSize);

        float blockCenterY = hasTitle ? canvasHeight * 0.4f : canvasHeight / 2f;
        float startY = blockCenterY - (totalTextHeight / 2f) + fontSize;
        float centerX = canvasWidth / 2f;

        foreach (var word in words)
        {
            canvas.DrawText(word, centerX, startY, cutoutPaint);
            startY += lineHeight;
        }
    }
}