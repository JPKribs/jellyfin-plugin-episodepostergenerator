using System;
using System.Globalization;
using System.IO;
using SkiaSharp;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    public class SplitPosterGenerator : BasePosterGenerator
    {
        private readonly ILogger<SplitPosterGenerator> _logger;

        // SplitPosterGenerator
        // Initializes a new instance of the split poster generator with logging support.
        public SplitPosterGenerator(ILogger<SplitPosterGenerator> logger)
        {
            _logger = logger;
        }

        // RenderCanvas
        // Draws the series poster on the left and the extracted frame on the right.
        protected override void RenderCanvas(SKCanvas skCanvas, SKBitmap canvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            var seriesPosterPath = episodeMetadata.VideoMetadata.SeriesPosterFilePath;
            var posterWidth = CalculatePosterWidth(height);

            using var canvasPaint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };

            // Draw extracted frame as base layer (full canvas)
            skCanvas.DrawBitmap(canvas, 0, 0, canvasPaint);

            // Draw series poster on the left side
            if (!string.IsNullOrEmpty(seriesPosterPath) && File.Exists(seriesPosterPath))
            {
                try
                {
                    using var posterStream = File.OpenRead(seriesPosterPath);
                    using var seriesPoster = SKBitmap.Decode(posterStream);

                    if (seriesPoster != null)
                    {
                        var posterRect = new SKRect(0, 0, posterWidth, height);
                        skCanvas.DrawBitmap(seriesPoster, posterRect, canvasPaint);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to decode series poster: {Path}", seriesPosterPath);
                        DrawFallbackPoster(skCanvas, posterWidth, height);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load series poster: {Path}", seriesPosterPath);
                    DrawFallbackPoster(skCanvas, posterWidth, height);
                }
            }
            else
            {
                _logger.LogDebug("No series poster available, using fallback");
                DrawFallbackPoster(skCanvas, posterWidth, height);
            }
        }

        // DrawFallbackPoster
        // Draws a solid dark rectangle when no series poster is available.
        private static void DrawFallbackPoster(SKCanvas skCanvas, int posterWidth, int height)
        {
            using var fallbackPaint = new SKPaint
            {
                Color = new SKColor(20, 20, 20),
                Style = SKPaintStyle.Fill
            };
            skCanvas.DrawRect(0, 0, posterWidth, height, fallbackPaint);
        }

        // RenderOverlay
        // Applies the overlay only to the right side (text area).
        protected override void RenderOverlay(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            if (string.IsNullOrEmpty(settings.OverlayColor))
                return;

            var primaryColor = ColorUtils.ParseHexColor(settings.OverlayColor);
            if (primaryColor.Alpha == 0)
                return;

            var posterWidth = CalculatePosterWidth(height);
            var rightRect = SKRect.Create(posterWidth, 0, width - posterWidth, height);

            // Solid color overlay
            if (settings.OverlayGradient == OverlayGradient.None)
            {
                using var overlayPaint = new SKPaint
                {
                    Color = primaryColor,
                    Style = SKPaintStyle.Fill
                };
                skCanvas.DrawRect(rightRect, overlayPaint);
            }
            // Gradient overlay
            else
            {
                var secondaryColor = ColorUtils.ParseHexColor(settings.OverlaySecondaryColor);
                if (secondaryColor.Alpha == 0) secondaryColor = primaryColor;

                var gradient = CreateOverlayGradient(settings.OverlayGradient, rightRect, primaryColor, secondaryColor);
                if (gradient != null)
                {
                    using var overlayPaint = new SKPaint
                    {
                        Shader = gradient,
                        Style = SKPaintStyle.Fill,
                        IsDither = true
                    };
                    skCanvas.DrawRect(rightRect, overlayPaint);
                }
            }
        }

        // RenderTypography
        // Renders episode title and info text within the right side safe area.
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            var seasonNumber = episodeMetadata.SeasonNumber ?? 0;
            var episodeNumber = episodeMetadata.EpisodeNumberStart ?? 0;
            var episodeTitle = episodeMetadata.EpisodeName ?? "-";

            var safeArea = GetRightSideSafeArea(width, height, settings);
            float spacingHeight = height * RenderConstants.DefaultSpacingRatio;
            float currentBottomY = safeArea.Bottom;

            // Both title and episode info enabled
            if (settings.ShowTitle && settings.ShowEpisode)
            {
                var titleHeight = DrawEpisodeTitle(skCanvas, episodeTitle, settings, width, height, currentBottomY, safeArea);
                currentBottomY -= titleHeight + spacingHeight;

                var lineHeight = DrawSeparatorLine(settings, skCanvas, currentBottomY, safeArea);
                currentBottomY -= lineHeight + spacingHeight;

                DrawEpisodeInfo(skCanvas, seasonNumber, episodeNumber, settings, height, currentBottomY, safeArea);
            }
            // Only title enabled
            else if (settings.ShowTitle)
            {
                DrawEpisodeTitle(skCanvas, episodeTitle, settings, width, height, currentBottomY, safeArea);
            }
            // Only episode info enabled
            else if (settings.ShowEpisode)
            {
                DrawEpisodeInfo(skCanvas, seasonNumber, episodeNumber, settings, height, currentBottomY, safeArea);
            }
        }

        // LogError
        // Logs an error that occurred during split poster generation.
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate split poster for {EpisodeName}", episodeName);
        }

        // CalculatePosterWidth
        // Calculates the width for a 2:3 aspect ratio poster that fills the full height.
        private static int CalculatePosterWidth(int height)
        {
            // 2:3 aspect ratio means width = height * (2/3)
            return (int)(height * (2.0 / 3.0));
        }

        // GetRightSideSafeArea
        // Calculates the safe area for the right side where text is rendered.
        private SKRect GetRightSideSafeArea(int width, int height, PosterSettings settings)
        {
            var posterWidth = CalculatePosterWidth(height);
            var rightSideWidth = width - posterWidth;

            var safeMargin = GetSafeAreaMargin(settings);
            var marginX = rightSideWidth * safeMargin;
            var marginY = height * safeMargin;

            var safeLeft = posterWidth + marginX;
            var safeTop = marginY;
            var safeWidth = rightSideWidth - (2 * marginX);
            var safeHeight = height - (2 * marginY);

            return new SKRect(safeLeft, safeTop, safeLeft + safeWidth, safeTop + safeHeight);
        }

        // DrawEpisodeTitle
        // Draws the episode title with shadow effect and returns the total height used.
        private static float DrawEpisodeTitle(SKCanvas canvas, string title, PosterSettings config, int canvasWidth, int canvasHeight, float bottomY, SKRect safeArea)
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
        private static float DrawSeparatorLine(PosterSettings config, SKCanvas canvas, float y, SKRect safeArea)
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
        private static void DrawEpisodeInfo(SKCanvas canvas, int seasonNumber, int episodeNumber, PosterSettings config, int canvasHeight, float bottomY, SKRect safeArea)
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
