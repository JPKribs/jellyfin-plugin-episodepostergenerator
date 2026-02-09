using System;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using SkiaSharp;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    public class NumeralPosterGenerator : BasePosterGenerator
    {
        private readonly ILogger<NumeralPosterGenerator> _logger;

        // NumeralPosterGenerator
        // Initializes a new instance of the numeral poster generator with logging support.
        public NumeralPosterGenerator(ILogger<NumeralPosterGenerator> logger)
        {
            _logger = logger;
        }

        // RenderTypography
        // Renders the Roman numeral and optional episode title centered on the poster.
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            var safeArea = GetSafeAreaBounds(width, height, settings);

            DrawRomanNumeral(skCanvas, episodeMetadata, settings, safeArea);

            if (settings.ShowTitle && !string.IsNullOrEmpty(episodeMetadata.EpisodeName))
            {
                DrawEpisodeTitle(skCanvas, episodeMetadata.EpisodeName, settings, width, height, safeArea);
            }
        }

        // LogError
        // Logs an error that occurred during numeral poster generation.
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate numeral poster for {EpisodeName}", episodeName);
        }

        // DrawRomanNumeral
        // Draws the episode number as a Roman numeral centered in the safe area.
        private void DrawRomanNumeral(SKCanvas canvas, EpisodeMetadata episodeMetadata, PosterSettings config, SKRect area)
        {
            var numeralText = NumberUtils.NumberToRomanNumeral(episodeMetadata.EpisodeNumberStart ?? 0);
            var typeface = FontUtils.ResolveTypeface(config.EffectiveEpisodeFontPath, config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle));

            float fontSize = FontUtils.CalculateOptimalFontSize(numeralText, typeface, area.Width, area.Height);

            using var numeralPaint = new SKPaint
            {
                Color = ColorUtils.ParseHexColor(config.EpisodeFontColor),
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                TextSize = fontSize,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center
            };

            using var shadowPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(180),
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                TextSize = fontSize,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 1.5f)
            };

            float centerX = area.MidX;
            var bounds = FontUtils.MeasureTextDimensions(numeralText, typeface, fontSize);
            float centerY = area.MidY + (bounds.Height / 2f);

            canvas.DrawText(numeralText, centerX + 2, centerY + 2, shadowPaint);
            canvas.DrawText(numeralText, centerX, centerY, numeralPaint);
        }

        // DrawEpisodeTitle
        // Draws the episode title centered and overlapping the Roman numeral.
        private void DrawEpisodeTitle(SKCanvas canvas, string title, PosterSettings config, int width, int height, SKRect safeArea)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height);
            var typeface = FontUtils.ResolveTypeface(config.EffectiveTitleFontPath, config.TitleFontFamily, FontUtils.GetFontStyle(config.TitleFontStyle));

            using var titlePaint = new SKPaint
            {
                Color = ColorUtils.ParseHexColor(config.TitleFontColor),
                TextSize = fontSize,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center
            };

            using var shadowPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(180),
                TextSize = fontSize,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 1.5f)
            };

            var availableWidth = safeArea.Width * 0.9f;
            var lines = TextUtils.FitTextToWidth(title, titlePaint, availableWidth);

            var lineHeight = fontSize * 1.2f;
            var totalHeight = (lines.Count - 1) * lineHeight + fontSize;
            var centerX = safeArea.MidX;

            var startY = safeArea.MidY - (totalHeight / 2f) + fontSize;

            for (int i = 0; i < lines.Count; i++)
            {
                var lineY = startY + (i * lineHeight);
                canvas.DrawText(lines[i], centerX + 2, lineY + 2, shadowPaint);
                canvas.DrawText(lines[i], centerX, lineY, titlePaint);
            }
        }

        // CalculateTitleHeight
        // Calculates the total height needed for the episode title text.
        private float CalculateTitleHeight(string title, PosterSettings config, int height, SKRect safeArea)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height);
            var typeface = FontUtils.ResolveTypeface(config.EffectiveTitleFontPath, config.TitleFontFamily, FontUtils.GetFontStyle(config.TitleFontStyle));

            using var paint = new SKPaint
            {
                TextSize = fontSize,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center,
                SubpixelText = true,
                LcdRenderText = true
            };

            var availableWidth = safeArea.Width * 0.9f;
            var lines = TextUtils.FitTextToWidth(title, paint, availableWidth);
            var lineHeight = fontSize * 1.2f;

            return (lines.Count - 1) * lineHeight + fontSize;
        }
    }
}
