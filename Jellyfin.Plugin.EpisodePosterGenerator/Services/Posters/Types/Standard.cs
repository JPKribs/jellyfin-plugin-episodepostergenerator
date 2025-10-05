using System;
using System.Globalization;
using SkiaSharp;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    /// <summary>
    /// Generates standard-style episode posters with bottom-aligned text elements.
    /// Uses 4-layer rendering: Canvas → Overlay → Graphics → Typography (bottom text stack)
    /// </summary>
    public class StandardPosterGenerator : BasePosterGenerator
    {
        private readonly ILogger<StandardPosterGenerator> _logger;

        public StandardPosterGenerator(ILogger<StandardPosterGenerator> logger)
        {
            _logger = logger;
        }

        // MARK: RenderTypography
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            var seasonNumber = episodeMetadata.SeasonNumber ?? 0;
            var episodeNumber = episodeMetadata.EpisodeNumberStart ?? 0;
            var episodeTitle = episodeMetadata.EpisodeName ?? "-";

            var safeArea = GetSafeAreaBounds(width, height, settings);
            float spacingHeight = height * 0.02f;
            float currentBottomY = safeArea.Bottom;

            // Build text stack from bottom to top: episode info, separator line, episode title
            if (settings.ShowTitle && settings.ShowEpisode)
            {
                var titleHeight = DrawEpisodeTitle(skCanvas, episodeTitle, settings, width, height, currentBottomY, safeArea);
                currentBottomY -= titleHeight + spacingHeight;

                var lineHeight = DrawSeparatorLine(settings, skCanvas, width, currentBottomY, safeArea);
                currentBottomY -= lineHeight + spacingHeight;

                DrawEpisodeInfo(skCanvas, seasonNumber, episodeNumber, settings, width, height, currentBottomY, safeArea);
            }
            else if (settings.ShowTitle)
            {
                DrawEpisodeTitle(skCanvas, episodeTitle, settings, width, height, currentBottomY, safeArea);
            }
            else if (settings.ShowEpisode)
            {
                DrawEpisodeInfo(skCanvas, seasonNumber, episodeNumber, settings, width, height, currentBottomY, safeArea);
            }
        }

        // MARK: LogError
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate standard poster for {EpisodeName}", episodeName);
        }

        // MARK: DrawEpisodeTitle
        private float DrawEpisodeTitle(SKCanvas canvas, string title, PosterSettings config, int canvasWidth, int canvasHeight, float bottomY, SKRect safeArea)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, canvasHeight);
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

            var safeWidth = safeArea.Width * 0.9f;
            var lines = TextUtils.FitTextToWidth(title, titlePaint, safeWidth);

            var lineHeight = fontSize * 1.2f;
            var totalHeight = (lines.Count - 1) * lineHeight + fontSize;

            var centerX = safeArea.MidX;
            var startY = bottomY - totalHeight + fontSize;

            for (int i = 0; i < lines.Count; i++)
            {
                var lineY = startY + (i * lineHeight);
                canvas.DrawText(lines[i], centerX + 2, lineY + 2, shadowPaint);
                canvas.DrawText(lines[i], centerX, lineY, titlePaint);
            }

            return totalHeight;
        }

        // MARK: DrawSeparatorLine
        private float DrawSeparatorLine(PosterSettings config, SKCanvas canvas, int canvasWidth, float y, SKRect safeArea)
        {
            var startX = safeArea.Left;
            var endX = safeArea.Right;

            using var shadowPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(180),
                StrokeWidth = 2f,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            canvas.DrawLine(startX + 2, y + 2, endX + 2, y + 2, shadowPaint);

            using var linePaint = new SKPaint
            {
                Color = SKColors.White,
                StrokeWidth = 2f,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            canvas.DrawLine(startX, y, endX, y, linePaint);

            return 4f;
        }

        // MARK: DrawEpisodeInfo
        private void DrawEpisodeInfo(SKCanvas canvas, int seasonNumber, int episodeNumber, PosterSettings config, int canvasWidth, int canvasHeight, float bottomY, SKRect safeArea)
        {
            var episodeFontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize, canvasHeight);
            var episodeColor = ColorUtils.ParseHexColor(config.EpisodeFontColor ?? "#FFFFFF");
            var shadowColor = SKColors.Black.WithAlpha(180);

            using var episodePaint = new SKPaint
            {
                Color = episodeColor,
                TextSize = episodeFontSize,
                IsAntialias = true,
                Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
                TextAlign = SKTextAlign.Center
            };

            using var shadowPaint = new SKPaint
            {
                Color = shadowColor,
                TextSize = episodeFontSize,
                IsAntialias = true,
                Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
                TextAlign = SKTextAlign.Center
            };

            using var bulletPaint = new SKPaint
            {
                Color = episodeColor,
                TextSize = episodeFontSize,
                IsAntialias = true,
                Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, SKFontStyle.Normal),
                TextAlign = SKTextAlign.Center
            };

            using var bulletShadowPaint = new SKPaint
            {
                Color = shadowColor,
                TextSize = episodeFontSize,
                IsAntialias = true,
                Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, SKFontStyle.Normal),
                TextAlign = SKTextAlign.Center
            };

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

            canvas.DrawText(seasonText, seasonX + 2, baselineY + 2, shadowPaint);
            canvas.DrawText(seasonText, seasonX, baselineY, episodePaint);

            canvas.DrawText(bulletText, bulletX + 2, baselineY + 2, bulletShadowPaint);
            canvas.DrawText(bulletText, bulletX, baselineY, bulletPaint);

            canvas.DrawText(episodeText, episodeX + 2, baselineY + 2, shadowPaint);
            canvas.DrawText(episodeText, episodeX, baselineY, episodePaint);
        }
    }
}