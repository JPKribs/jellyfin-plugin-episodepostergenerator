using System;
using System.Globalization;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utilities;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    public class TimelinePosterGenerator : BasePosterGenerator
    {
        // Style
        // The poster style this generator produces.
        public override PosterStyle Style => PosterStyle.Timeline;

        // Description
        // A short, user facing description of this style shown in the configuration UI.
        public override string Description => "Season progress bar with episode info and optional title. Clean and data driven.";

        private readonly ILogger<TimelinePosterGenerator> _logger;

        // TimelinePosterGenerator
        // Initializes a new instance of the timeline poster generator with logging support.
        public TimelinePosterGenerator(ILogger<TimelinePosterGenerator> logger)
        {
            _logger = logger;
        }

        // RenderTypography
        // Renders, bottom-up: the season progress bar, the episode code and position labels
        // above it, and the optional episode title above those.
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            var safeArea = GetSafeAreaBounds(width, height, settings);
            float spacing = height * RenderConstants.DefaultSpacingRatio;

            var episodeNumber = episodeMetadata.EpisodeNumberStart ?? 0;
            // When the season size is unknown the bar renders full rather than guessing.
            var totalEpisodes = episodeMetadata.SeasonEpisodeCount ?? episodeNumber;

            float barTop = DrawProgressBar(skCanvas, settings, safeArea, height, episodeNumber, totalEpisodes);
            float currentBottomY = barTop - spacing;

            if (settings.ShowEpisode)
            {
                DrawEpisodeLabels(skCanvas, episodeMetadata, settings, height, safeArea, currentBottomY, episodeNumber, totalEpisodes);
                var labelFontSize = FontUtils.CalculateFontSizeFromPercentage(settings.EpisodeFontSize, height);
                currentBottomY -= labelFontSize + spacing;
            }

            if (settings.ShowTitle && !string.IsNullOrEmpty(episodeMetadata.EpisodeName))
            {
                DrawEpisodeTitle(skCanvas, episodeMetadata.EpisodeName, settings, height, safeArea, currentBottomY);
            }
        }

        // DrawProgressBar
        // Draws the season timeline: a rounded track across the safe width, filled to the
        // episode's position, with a marker dot at the fill point. Returns the bar's top edge.
        private static float DrawProgressBar(SKCanvas canvas, PosterSettings config, SKRect safeArea, int height, int episodeNumber, int totalEpisodes)
        {
            float barHeight = Math.Max(4f, height * 0.008f);
            float dotRadius = barHeight * 1.5f;
            float barY = safeArea.Bottom - dotRadius;

            float progress = totalEpisodes > 0
                ? Math.Clamp((float)episodeNumber / totalEpisodes, 0f, 1f)
                : 1f;

            var barColor = ColorUtils.ParseHexColor(config.EpisodeFontColor);
            float fillEndX = safeArea.Left + (safeArea.Width * progress);

            using var trackPaint = new SKPaint
            {
                Color = barColor.WithAlpha((byte)(barColor.Alpha * 0.3f)),
                StrokeWidth = barHeight,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                IsAntialias = true
            };
            using var fillPaint = new SKPaint
            {
                Color = barColor,
                StrokeWidth = barHeight,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                IsAntialias = true
            };
            using var shadowPaint = new SKPaint
            {
                Color = RenderConstants.ShadowColor,
                StrokeWidth = barHeight,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, RenderConstants.ShadowBlurSigma)
            };

            canvas.DrawLine(
                safeArea.Left + RenderConstants.ShadowOffset, barY + RenderConstants.ShadowOffset,
                safeArea.Right + RenderConstants.ShadowOffset, barY + RenderConstants.ShadowOffset,
                shadowPaint);
            canvas.DrawLine(safeArea.Left, barY, safeArea.Right, barY, trackPaint);
            canvas.DrawLine(safeArea.Left, barY, fillEndX, barY, fillPaint);

            using var dotPaint = new SKPaint
            {
                Color = barColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawCircle(fillEndX, barY, dotRadius, dotPaint);

            return barY - dotRadius;
        }

        // DrawEpisodeLabels
        // Draws the episode code left-aligned and the season position right-aligned
        // directly above the progress bar.
        private static void DrawEpisodeLabels(SKCanvas canvas, EpisodeMetadata episodeMetadata, PosterSettings config, int height, SKRect safeArea, float bottomY, int episodeNumber, int totalEpisodes)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize, height);
            var typeface = FontUtils.ResolveTypeface(config.EffectiveEpisodeFontPath, config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle));
            var color = ColorUtils.ParseHexColor(config.EpisodeFontColor);

            using var leftPaint = PaintFactory.CreateTextPaint(color, fontSize, typeface, SKTextAlign.Left);
            using var leftShadow = PaintFactory.CreateShadowTextPaint(fontSize, typeface, SKTextAlign.Left);
            using var rightPaint = PaintFactory.CreateTextPaint(color, fontSize, typeface, SKTextAlign.Right);
            using var rightShadow = PaintFactory.CreateShadowTextPaint(fontSize, typeface, SKTextAlign.Right);

            var codeText = EpisodeCodeUtils.FormatEpisodeCode(episodeMetadata.SeasonNumber ?? 0, episodeNumber);
            var positionText = episodeMetadata.SeasonEpisodeCount.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "{0} OF {1}", episodeNumber, totalEpisodes)
                : string.Format(CultureInfo.InvariantCulture, "EPISODE {0}", episodeNumber);

            float baselineY = bottomY - Math.Abs(leftPaint.FontMetrics.Descent);

            PaintFactory.DrawTextWithShadow(canvas, codeText, safeArea.Left, baselineY, leftPaint, leftShadow);
            PaintFactory.DrawTextWithShadow(canvas, positionText, safeArea.Right, baselineY, rightPaint, rightShadow);
        }

        // DrawEpisodeTitle
        // Draws the episode title left-aligned above the labels, wrapped to the safe width.
        private static void DrawEpisodeTitle(SKCanvas canvas, string title, PosterSettings config, int height, SKRect safeArea, float bottomY)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height);
            var typeface = FontUtils.ResolveTypeface(config.EffectiveTitleFontPath, config.TitleFontFamily, FontUtils.GetFontStyle(config.TitleFontStyle));

            using var titlePaint = PaintFactory.CreateTextPaint(ColorUtils.ParseHexColor(config.TitleFontColor), fontSize, typeface, SKTextAlign.Left);
            using var shadowPaint = PaintFactory.CreateShadowTextPaint(fontSize, typeface, SKTextAlign.Left);

            var maxWidth = safeArea.Width * RenderConstants.TextWidthMultiplier;
            var lines = TextUtils.FitTitleLines(title, titlePaint, maxWidth, config.LongTitleHandling);
            if (lines.Count == 0)
                return;

            var lineHeight = fontSize * RenderConstants.LineHeightMultiplier;
            var totalHeight = (lines.Count - 1) * lineHeight + fontSize;
            var startY = bottomY - totalHeight + fontSize;

            for (int i = 0; i < lines.Count; i++)
            {
                var lineY = startY + (i * lineHeight);
                PaintFactory.DrawTextWithShadow(canvas, lines[i], safeArea.Left, lineY, titlePaint, shadowPaint);
            }
        }

        // LogError
        // Logs an error that occurred during timeline poster generation.
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate timeline poster for {EpisodeName}", episodeName);
        }
    }
}
