using System;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using SkiaSharp;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    /// <summary>
    /// Generates a cutout-style poster from a pre-rendered canvas and episode metadata.
    /// Handles safe areas and overlay options defined in PluginConfiguration.
    /// </summary>
    public class CutoutPosterGenerator : BasePosterGenerator, IPosterGenerator
    {
        private readonly ILogger<CutoutPosterGenerator> _logger;

        private static readonly char[] WordSeparators = { ' ', '-' };

        public CutoutPosterGenerator(ILogger<CutoutPosterGenerator> logger)
        {
            _logger = logger;
        }

        public string? Generate(SKBitmap canvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, string? outputPath)
        {
            try
            {
                return GenerateBase(canvas, episodeMetadata, config, bmp =>
                {
                    int width = bmp.Width;
                    int height = bmp.Height;

                    using var surface = SKSurface.Create(new SKImageInfo(width, height));
                    var skCanvas = surface.Canvas;
                    skCanvas.Clear(SKColors.Transparent);

                    // Draw overlay
                    using var overlayPaint = new SKPaint
                    {
                        Color = ColorUtils.ParseHexColor(config.OverlayColor),
                        Style = SKPaintStyle.Fill,
                        BlendMode = SKBlendMode.Src
                    };
                    skCanvas.DrawRect(SKRect.Create(width, height), overlayPaint);

                    // Draw cutout text
                    DrawCutoutText(skCanvas, episodeMetadata, config, width, height, overlayPaint.Color);

                    // Draw original bitmap underneath
                    using var originalPaint = new SKPaint { BlendMode = SKBlendMode.DstOver };
                    skCanvas.DrawBitmap(bmp, 0, 0, originalPaint);

                    // Snapshot and return
                    using var finalImage = surface.Snapshot();
                    return SKBitmap.FromImage(finalImage);
                }, outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate cutout poster for {EpisodeName}", episodeMetadata.EpisodeName);
                return null;
            }
        }

        private void DrawCutoutText(SKCanvas canvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int canvasWidth, int canvasHeight, SKColor overlayColor)
        {
            ApplySafeAreaConstraints(canvasWidth, canvasHeight, config, out float safeWidth, out float safeHeight, out float safeLeft, out float safeTop);

            string episodeText = EpisodeCodeUtil.FormatEpisodeText(config.CutoutType,
                episodeMetadata.SeasonNumber ?? 0,
                episodeMetadata.EpisodeNumberStart ?? 0);
            var words = episodeText.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);

            var cutoutArea = CalculateCutoutArea(canvasWidth, canvasHeight, config.ShowTitle, config, safeLeft, safeTop, safeWidth, safeHeight);

            var fontStyle = FontUtils.GetFontStyle(config.EpisodeFontStyle);
            using var typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, fontStyle);

            float fontSize = CalculateOptimalCutoutFontSize(words, typeface, cutoutArea);

            if (config.CutoutBorder)
            {
                using var borderPaint = new SKPaint
                {
                    Color = GetContrastingBorderColor(overlayColor),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = Math.Max(1f, fontSize * 0.015f),
                    IsAntialias = true,
                    Typeface = typeface,
                    TextAlign = SKTextAlign.Center,
                    TextSize = fontSize
                };
                DrawCutoutTextCentered(canvas, words, borderPaint, cutoutArea);
            }

            using var cutoutPaint = new SKPaint
            {
                Color = SKColors.Transparent,
                BlendMode = SKBlendMode.Clear,
                IsAntialias = true,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center,
                TextSize = fontSize
            };
            DrawCutoutTextCentered(canvas, words, cutoutPaint, cutoutArea);
        }

        private SKColor GetContrastingBorderColor(SKColor overlayColor)
        {
            float r = overlayColor.Red / 255f;
            float g = overlayColor.Green / 255f;
            float b = overlayColor.Blue / 255f;

            r = r <= 0.03928f ? r / 12.92f : (float)Math.Pow((r + 0.055f) / 1.055f, 2.4f);
            g = g <= 0.03928f ? g / 12.92f : (float)Math.Pow((g + 0.055f) / 1.055f, 2.4f);
            b = b <= 0.03928f ? b / 12.92f : (float)Math.Pow((b + 0.055f) / 1.055f, 2.4f);

            float luminance = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            return luminance > 0.5f ? SKColors.Black : luminance < 0.2f ? SKColors.White : new SKColor(64, 64, 64);
        }

        private SKRect CalculateCutoutArea(float canvasWidth, float canvasHeight, bool hasTitle, PluginConfiguration config,
            float safeLeft, float safeTop, float safeWidth, float safeHeight)
        {
            if (!hasTitle)
                return new SKRect(safeLeft, safeTop, safeLeft + safeWidth, safeTop + safeHeight);

            float titleFontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, canvasHeight, 100f * GetSafeAreaMargin(config));
            float titleLineHeight = titleFontSize * 1.2f;
            float titleTotalHeight = titleLineHeight * 2;
            float titleSpaceFromBottom = (canvasHeight * GetSafeAreaMargin(config)) + titleTotalHeight;
            float cutoutBuffer = canvasHeight * 0.05f;

            float availableHeight = Math.Max(canvasHeight - safeTop - titleSpaceFromBottom - cutoutBuffer, canvasHeight * 0.3f);
            return new SKRect(safeLeft, safeTop, safeLeft + safeWidth, safeTop + availableHeight);
        }

        private float CalculateOptimalCutoutFontSize(string[] words, SKTypeface typeface, SKRect availableArea)
        {
            float maxWidth = availableArea.Width;
            float maxHeight = availableArea.Height;

            if (words.Length == 1)
                return FontUtils.CalculateOptimalFontSize(words[0], typeface, maxWidth, maxHeight, 50f);

            float lineSpacing = 1.1f;
            float maxFont = maxHeight / (words.Length * lineSpacing);
            float minFont = 30f;
            float low = minFont;
            float high = maxFont;
            float optimal = minFont;

            while (high - low > 1f)
            {
                float test = (low + high) / 2f;
                if (DoAllWordsFit(words, typeface, test, maxWidth, maxHeight, lineSpacing))
                {
                    optimal = test;
                    low = test;
                }
                else
                {
                    high = test;
                }
            }
            return optimal;
        }

        private bool DoAllWordsFit(string[] words, SKTypeface typeface, float fontSize, float maxWidth, float maxHeight, float lineSpacing)
        {
            float maxWordWidth = 0;
            foreach (var word in words)
            {
                var bounds = FontUtils.MeasureTextDimensions(word, typeface, fontSize);
                if (bounds.Width > maxWordWidth)
                    maxWordWidth = bounds.Width;
            }

            float totalHeight = words.Length * fontSize * lineSpacing;
            return maxWordWidth <= maxWidth && totalHeight <= maxHeight;
        }

        private void DrawCutoutTextCentered(SKCanvas canvas, string[] words, SKPaint paint, SKRect area)
        {
            float centerX = area.MidX;
            float centerY = area.MidY;

            if (words.Length == 1)
            {
                var bounds = FontUtils.MeasureTextDimensions(words[0], paint.Typeface!, paint.TextSize);
                float y = centerY + (bounds.Height / 2f);
                canvas.DrawText(words[0], centerX, y, paint);
            }
            else
            {
                float lineSpacing = 1.1f;
                float lineHeight = paint.TextSize * lineSpacing;
                float totalHeight = words.Length * lineHeight - (lineHeight - paint.TextSize);
                float startY = centerY - (totalHeight / 2f) + paint.TextSize;

                foreach (var word in words)
                {
                    canvas.DrawText(word, centerX, startY, paint);
                    startY += lineHeight;
                }
            }
        }
    }

    internal static class SKImageExtensions
    {
        public static SKBitmap ToBitmap(this SKImage image)
        {
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return SKBitmap.Decode(data);
        }
    }
}