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
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int width, int height)
        {
            var safeArea = GetSafeAreaBounds(width, height, config);

            if (config.ShowTitle && !string.IsNullOrEmpty(episodeMetadata.EpisodeName))
            {
                // Calculate space needed for title at bottom
                var titleHeight = CalculateTitleHeight(episodeMetadata.EpisodeName, config, height, safeArea);
                var titleSpacing = height * 0.05f;
                
                // Adjust safe area for Roman numeral to leave space for title
                var numeralArea = new SKRect(
                    safeArea.Left, 
                    safeArea.Top, 
                    safeArea.Right, 
                    safeArea.Bottom - titleHeight - titleSpacing
                );

                // Draw Roman numeral in upper area
                DrawRomanNumeral(skCanvas, episodeMetadata, config, numeralArea);

                // Draw title at bottom
                DrawEpisodeTitle(skCanvas, episodeMetadata.EpisodeName, config, width, height, safeArea);
            }
            else
            {
                // No title - Roman numeral uses full safe area
                DrawRomanNumeral(skCanvas, episodeMetadata, config, safeArea);
            }
        }

        // MARK: LogError
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate numeral poster for {EpisodeName}", episodeName);
        }

        // MARK: DrawRomanNumeral
        private void DrawRomanNumeral(SKCanvas canvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, SKRect area)
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
        private void DrawEpisodeTitle(SKCanvas canvas, string title, PluginConfiguration config, int width, int height, SKRect safeArea)
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
            var startY = safeArea.Bottom - totalHeight + fontSize;

            // Draw each line with shadow
            for (int i = 0; i < lines.Count; i++)
            {
                var lineY = startY + (i * lineHeight);
                canvas.DrawText(lines[i], centerX + 2, lineY + 2, shadowPaint);
                canvas.DrawText(lines[i], centerX, lineY, titlePaint);
            }
        }

        // MARK: CalculateTitleHeight
        private float CalculateTitleHeight(string title, PluginConfiguration config, int height, SKRect safeArea)
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