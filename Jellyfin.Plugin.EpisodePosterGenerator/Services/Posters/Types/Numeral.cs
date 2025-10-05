using System;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using SkiaSharp;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    /// <summary>
    /// Generates episode posters featuring Roman numerals for episode numbers.
    /// Uses 4-layer rendering: Canvas → Overlay → Graphics → Typography (Roman numerals + optional title)
    /// </summary>
    public class NumeralPosterGenerator : BasePosterGenerator
    {
        private readonly ILogger<NumeralPosterGenerator> _logger;

        public NumeralPosterGenerator(ILogger<NumeralPosterGenerator> logger)
        {
            _logger = logger;
        }

        // MARK: RenderTypography
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            var safeArea = GetSafeAreaBounds(width, height, settings);

            // Always draw Roman numeral centered in full safe area
            DrawRomanNumeral(skCanvas, episodeMetadata, settings, safeArea);

            // Draw title overlapping on top of the Roman numeral if enabled
            if (settings.ShowTitle && !string.IsNullOrEmpty(episodeMetadata.EpisodeName))
            {
                DrawEpisodeTitle(skCanvas, episodeMetadata.EpisodeName, settings, width, height, safeArea);
            }
        }

        // MARK: LogError
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate numeral poster for {EpisodeName}", episodeName);
        }

        // MARK: DrawRomanNumeral
        private void DrawRomanNumeral(SKCanvas canvas, EpisodeMetadata episodeMetadata, PosterSettings config, SKRect area)
        {
            var numeralText = NumberUtils.NumberToRomanNumeral(episodeMetadata.EpisodeNumberStart ?? 0);
            var typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle));

            float fontSize = FontUtils.CalculateOptimalFontSize(numeralText, typeface, area.Width, area.Height);

            using var numeralPaint = new SKPaint
            {
                Color = ColorUtils.ParseHexColor(config.EpisodeFontColor),
                IsAntialias = true,
                TextSize = fontSize,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center
            };

            using var shadowPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(180),
                IsAntialias = true,
                TextSize = fontSize,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center
            };

            // Center text in available area
            float centerX = area.MidX;
            var bounds = FontUtils.MeasureTextDimensions(numeralText, typeface, fontSize);
            float centerY = area.MidY + (bounds.Height / 2f);

            // Draw shadow
            canvas.DrawText(numeralText, centerX + 2, centerY + 2, shadowPaint);
            // Draw main text
            canvas.DrawText(numeralText, centerX, centerY, numeralPaint);
        }

        // MARK: DrawEpisodeTitle
        private void DrawEpisodeTitle(SKCanvas canvas, string title, PosterSettings config, int width, int height, SKRect safeArea)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height);
            var typeface = FontUtils.CreateTypeface(config.TitleFontFamily, FontUtils.GetFontStyle(config.TitleFontStyle));

            using var titlePaint = new SKPaint
            {
                Color = ColorUtils.ParseHexColor(config.TitleFontColor),
                TextSize = fontSize,
                IsAntialias = true,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center
            };

            using var shadowPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(180),
                TextSize = fontSize,
                IsAntialias = true,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center
            };

            // Fit text to safe area width
            var availableWidth = safeArea.Width * 0.9f;
            var lines = TextUtils.FitTextToWidth(title, titlePaint, availableWidth);

            var lineHeight = fontSize * 1.2f;
            var totalHeight = (lines.Count - 1) * lineHeight + fontSize;
            var centerX = safeArea.MidX;
            
            // Position title at center of safe area, overlapping the Roman numeral
            var startY = safeArea.MidY - (totalHeight / 2f) + fontSize;

            // Draw each line with shadow, overlapping the Roman numeral at center
            for (int i = 0; i < lines.Count; i++)
            {
                var lineY = startY + (i * lineHeight);
                canvas.DrawText(lines[i], centerX + 2, lineY + 2, shadowPaint);
                canvas.DrawText(lines[i], centerX, lineY, titlePaint);
            }
        }

        // MARK: CalculateTitleHeight
        private float CalculateTitleHeight(string title, PosterSettings config, int height, SKRect safeArea)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height);
            var typeface = FontUtils.CreateTypeface(config.TitleFontFamily, FontUtils.GetFontStyle(config.TitleFontStyle));

            using var paint = new SKPaint
            {
                TextSize = fontSize,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center
            };

            var availableWidth = safeArea.Width * 0.9f;
            var lines = TextUtils.FitTextToWidth(title, paint, availableWidth);
            var lineHeight = fontSize * 1.2f;
            
            return (lines.Count - 1) * lineHeight + fontSize;
        }
    }
}