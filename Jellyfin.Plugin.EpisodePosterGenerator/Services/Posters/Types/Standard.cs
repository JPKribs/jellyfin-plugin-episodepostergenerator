using System;
using System.Globalization;
using SkiaSharp;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    public class StandardPosterGenerator : BasePosterGenerator
    {
        private readonly ILogger<StandardPosterGenerator> _logger;

        // StandardPosterGenerator
        // Initializes a new instance of the standard poster generator with logging support.
        public StandardPosterGenerator(ILogger<StandardPosterGenerator> logger)
        {
            _logger = logger;
        }

        // RenderTypography
        // Renders episode title and info text at the bottom of the poster.
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            var seasonNumber = episodeMetadata.SeasonNumber ?? 0;
            var episodeNumber = episodeMetadata.EpisodeNumberStart ?? 0;
            var episodeTitle = episodeMetadata.EpisodeName ?? "-";

            var safeArea = GetSafeAreaBounds(width, height, settings);
            float spacingHeight = height * RenderConstants.DefaultSpacingRatio;
            float currentBottomY = safeArea.Bottom;

            // Both title and episode info enabled
            if (settings.ShowTitle && settings.ShowEpisode)
            {
                var titleHeight = DrawEpisodeTitle(skCanvas, episodeTitle, settings, width, height, currentBottomY, safeArea);
                currentBottomY -= titleHeight + spacingHeight;

                var lineHeight = DrawSeparatorLine(settings, skCanvas, width, currentBottomY, safeArea);
                currentBottomY -= lineHeight + spacingHeight;

                DrawEpisodeInfo(skCanvas, seasonNumber, episodeNumber, settings, width, height, currentBottomY, safeArea);
            }
            // Only title enabled
            else if (settings.ShowTitle)
            {
                DrawEpisodeTitle(skCanvas, episodeTitle, settings, width, height, currentBottomY, safeArea);
            }
            // Only episode info enabled
            else if (settings.ShowEpisode)
            {
                DrawEpisodeInfo(skCanvas, seasonNumber, episodeNumber, settings, width, height, currentBottomY, safeArea);
            }
        }

        // LogError
        // Logs an error that occurred during standard poster generation.
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate standard poster for {EpisodeName}", episodeName);
        }

        // DrawEpisodeTitle
        // Draws the episode title with shadow effect and returns the total height used.
        private float DrawEpisodeTitle(SKCanvas canvas, string title, PosterSettings config, int canvasWidth, int canvasHeight, float bottomY, SKRect safeArea)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, canvasHeight);
            var typeface = FontUtils.CreateTypeface(config.TitleFontFamily, FontUtils.GetFontStyle(config.TitleFontStyle));
            var titleColor = ColorUtils.ParseHexColor(config.TitleFontColor);

            using var titlePaint = PaintFactory.CreateTextPaint(titleColor, fontSize, typeface);
            using var shadowPaint = PaintFactory.CreateShadowTextPaint(fontSize, typeface);

            var safeWidth = safeArea.Width * RenderConstants.TextWidthMultiplier;
            var lines = TextUtils.FitTextToWidth(title, titlePaint, safeWidth);

            var lineHeight = fontSize * RenderConstants.LineHeightMultiplier;
            var totalHeight = (lines.Count - 1) * lineHeight + fontSize;

            var centerX = safeArea.MidX;
            var startY = bottomY - totalHeight + fontSize;

            for (int i = 0; i < lines.Count; i++)
            {
                var lineY = startY + (i * lineHeight);
                PaintFactory.DrawTextWithShadow(canvas, lines[i], centerX, lineY, titlePaint, shadowPaint);
            }

            return totalHeight;
        }

        // DrawSeparatorLine
        // Draws a horizontal separator line with shadow effect.
        private static float DrawSeparatorLine(PosterSettings config, SKCanvas canvas, int canvasWidth, float y, SKRect safeArea)
        {
            var startX = safeArea.Left;
            var endX = safeArea.Right;

            using var shadowPaint = PaintFactory.CreateShadowLinePaint();
            using var linePaint = PaintFactory.CreateLinePaint(SKColors.White);

            PaintFactory.DrawLineWithShadow(canvas, startX, y, endX, y, linePaint, shadowPaint);

            return RenderConstants.SeparatorLineHeight;
        }

        // DrawEpisodeInfo
        // Draws the season and episode numbers with a bullet separator.
        private static void DrawEpisodeInfo(SKCanvas canvas, int seasonNumber, int episodeNumber, PosterSettings config, int canvasWidth, int canvasHeight, float bottomY, SKRect safeArea)
        {
            var episodeFontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize, canvasHeight);
            var episodeColor = ColorUtils.ParseHexColor(config.EpisodeFontColor ?? "#FFFFFF");
            var episodeTypeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle));
            var bulletTypeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, SKFontStyle.Normal);

            using var episodePaint = PaintFactory.CreateTextPaint(episodeColor, episodeFontSize, episodeTypeface);
            using var shadowPaint = PaintFactory.CreateShadowTextPaint(episodeFontSize, episodeTypeface);
            using var bulletPaint = PaintFactory.CreateTextPaint(episodeColor, episodeFontSize, bulletTypeface);
            using var bulletShadowPaint = PaintFactory.CreateShadowTextPaint(episodeFontSize, bulletTypeface);

            var seasonText = seasonNumber.ToString(CultureInfo.InvariantCulture);
            var episodeText = episodeNumber.ToString(CultureInfo.InvariantCulture);
            var bulletText = " • ";

            var fontMetrics = episodePaint.FontMetrics;
            var baselineY = bottomY - Math.Abs(fontMetrics.Descent);

            var seasonWidth = episodePaint.MeasureText(seasonText);
            var episodeWidth = episodePaint.MeasureText(episodeText);
            var bulletWidth = bulletPaint.MeasureText(bulletText);

            var centerX = safeArea.MidX;
            var bulletX = centerX;
            var seasonX = bulletX - (bulletWidth / 2f) - (seasonWidth / 2f);
            var episodeX = bulletX + (bulletWidth / 2f) + (episodeWidth / 2f);

            PaintFactory.DrawTextWithShadow(canvas, seasonText, seasonX, baselineY, episodePaint, shadowPaint);
            PaintFactory.DrawTextWithShadow(canvas, bulletText, bulletX, baselineY, bulletPaint, bulletShadowPaint);
            PaintFactory.DrawTextWithShadow(canvas, episodeText, episodeX, baselineY, episodePaint, shadowPaint);
        }
    }
}
