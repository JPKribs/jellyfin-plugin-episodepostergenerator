using System;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using SkiaSharp;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    /// <summary>
    /// Generates cutout-style posters with transparent text revealing the canvas beneath.
    /// Uses 4-layer rendering: Canvas → Overlay (with cutouts) → Graphics → Typography
    /// </summary>
    public class CutoutPosterGenerator : BasePosterGenerator
    {
        private readonly ILogger<CutoutPosterGenerator> _logger;
        private static readonly char[] WordSeparators = { ' ', '-' };

        public CutoutPosterGenerator(ILogger<CutoutPosterGenerator> logger)
        {
            _logger = logger;
        }

        // MARK: RenderOverlay
        protected override void RenderOverlay(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            if (string.IsNullOrEmpty(settings.OverlayColor))
                return;

            var overlayColor = ColorUtils.ParseHexColor(settings.OverlayColor);
            if (overlayColor.Alpha == 0)
                return;

            // Create a bitmap for the overlay with cutouts
            using var overlayBitmap = new SKBitmap(width, height);
            using var overlayCanvas = new SKCanvas(overlayBitmap);
            
            // Fill with overlay color
            using var overlayPaint = new SKPaint
            {
                Color = overlayColor,
                Style = SKPaintStyle.Fill
            };
            overlayCanvas.DrawRect(SKRect.Create(width, height), overlayPaint);

            // Create cutout text (this will remove parts of the overlay)
            DrawCutoutText(overlayCanvas, episodeMetadata, settings, width, height, overlayColor);

            // Draw the overlay with cutouts onto the main canvas
            using var finalPaint = new SKPaint { IsAntialias = true };
            skCanvas.DrawBitmap(overlayBitmap, 0, 0, finalPaint);
        }

        // MARK: RenderTypography
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            // Cutout style can optionally show title text if enabled
            if (!settings.ShowTitle || string.IsNullOrEmpty(episodeMetadata.EpisodeName))
                return;

            var safeArea = GetSafeAreaBounds(width, height, settings);
            
            // Position title at bottom of safe area
            var titleY = safeArea.Bottom - (safeArea.Height * 0.1f);
            
            using var titlePaint = new SKPaint
            {
                Color = ColorUtils.ParseHexColor(settings.TitleFontColor),
                TextSize = FontUtils.CalculateFontSizeFromPercentage(settings.TitleFontSize, height),
                IsAntialias = true,
                Typeface = FontUtils.CreateTypeface(settings.TitleFontFamily, FontUtils.GetFontStyle(settings.TitleFontStyle)),
                TextAlign = SKTextAlign.Center
            };

            using var shadowPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(180),
                TextSize = titlePaint.TextSize,
                IsAntialias = true,
                Typeface = titlePaint.Typeface,
                TextAlign = SKTextAlign.Center
            };

            var centerX = safeArea.MidX;
            skCanvas.DrawText(episodeMetadata.EpisodeName, centerX + 2, titleY + 2, shadowPaint);
            skCanvas.DrawText(episodeMetadata.EpisodeName, centerX, titleY, titlePaint);
        }

        // MARK: LogError
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate cutout poster for {EpisodeName}", episodeName);
        }

        // MARK: DrawCutoutText
        private void DrawCutoutText(SKCanvas canvas, EpisodeMetadata episodeMetadata, PosterSettings config, int canvasWidth, int canvasHeight, SKColor overlayColor)
        {
            var safeArea = GetSafeAreaBounds(canvasWidth, canvasHeight, config);

            string episodeText = EpisodeCodeUtil.FormatEpisodeText(config.CutoutType,
                episodeMetadata.SeasonNumber ?? 0,
                episodeMetadata.EpisodeNumberStart ?? 0);
            var words = episodeText.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);

            var cutoutArea = CalculateCutoutArea(safeArea, config.ShowTitle, config, canvasHeight);
            var fontStyle = FontUtils.GetFontStyle(config.EpisodeFontStyle);
            using var typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, fontStyle);
            float fontSize = CalculateOptimalCutoutFontSize(words, typeface, cutoutArea);

            // Draw border if enabled
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

            // Draw cutout text (transparent)
            using var cutoutPaint = new SKPaint
            {
                BlendMode = SKBlendMode.DstOut,
                IsAntialias = true,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center,
                TextSize = fontSize
            };
            DrawCutoutTextCentered(canvas, words, cutoutPaint, cutoutArea);
        }

        // MARK: GetContrastingBorderColor
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

        // MARK: CalculateCutoutArea
        private SKRect CalculateCutoutArea(SKRect safeArea, bool hasTitle, PosterSettings config, float canvasHeight)
        {
            if (!hasTitle)
                return safeArea;

            // Reserve space for title at bottom
            float titleFontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, canvasHeight);
            float titleSpace = titleFontSize * 2f; // Space for title + padding
            float cutoutBuffer = canvasHeight * 0.05f;

            float availableHeight = Math.Max(safeArea.Height - titleSpace - cutoutBuffer, safeArea.Height * 0.6f);
            
            return new SKRect(safeArea.Left, safeArea.Top, safeArea.Right, safeArea.Top + availableHeight);
        }

        // MARK: CalculateOptimalCutoutFontSize
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

        // MARK: DoAllWordsFit
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

        // MARK: DrawCutoutTextCentered
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
}