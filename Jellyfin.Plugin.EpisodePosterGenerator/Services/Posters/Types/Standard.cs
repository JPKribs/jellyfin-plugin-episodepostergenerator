using System;
using System.Globalization;
using SkiaSharp;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utilities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    public class StandardPosterGenerator : BasePosterGenerator
    {
        // Style
        // The poster style this generator produces.
        public override PosterStyle Style => PosterStyle.Standard;

        // Description
        // A short, user facing description of this style shown in the configuration UI.
        public override string Description => "Full frame image with text at the bottom. Clean and versatile.";

        private const string EpisodeBlock = "episode";
        private const string SeparatorBlock = "separator";
        private const string TitleBlock = "title";

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
            var column = new LayoutColumn(safeArea, GetElementSpacing(settings, height), LayoutAnchor.Bottom);

            var showSeparator = settings.ShowTitle && settings.ShowEpisode;

            // Measured top-to-bottom, then placed: the separator and episode info sit above a fixed
            // two line title zone, so a wrapped or dropped title cannot move them between episodes.
            column
                .Add(EpisodeBlock, settings.ShowEpisode
                    ? FontUtils.CalculateFontSizeFromPercentage(settings.EpisodeFontSize, height)
                    : 0f)
                .Add(SeparatorBlock, showSeparator ? RenderConstants.SeparatorLineHeight : 0f)
                .Add(TitleBlock, settings.ShowTitle
                    ? FontUtils.CalculateFontSizeFromPercentage(settings.TitleFontSize, height) * (1 + RenderConstants.LineHeightMultiplier)
                    : 0f);

            if (column.TryGetSlot(EpisodeBlock, out var episodeSlot))
            {
                DrawEpisodeInfo(skCanvas, seasonNumber, episodeNumber, settings, height, episodeSlot);
            }

            if (column.TryGetSlot(SeparatorBlock, out var separatorSlot))
            {
                DrawSeparatorLine(settings, skCanvas, separatorSlot);
            }

            if (column.TryGetSlot(TitleBlock, out var titleSlot))
            {
                DrawEpisodeTitle(skCanvas, episodeTitle, settings, height, titleSlot);
            }
        }

        // LogError
        // Logs an error that occurred during standard poster generation.
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate standard poster for {EpisodeName}", episodeName);
        }

        // DrawEpisodeTitle
        // Draws the episode title inside the slot the column allotted it.
        private void DrawEpisodeTitle(SKCanvas canvas, string title, PosterSettings config, int canvasHeight, SKRect slot)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, canvasHeight);
            var typeface = FontUtils.ResolveTypeface(config.EffectiveTitleFontPath, config.TitleFontFamily, FontUtils.GetFontStyle(config.TitleFontStyle));
            var titleColor = ColorUtils.ParseHexColor(config.TitleFontColor);

            using var titlePaint = PaintFactory.CreateTextPaint(titleColor, fontSize, typeface);
            using var shadowPaint = PaintFactory.CreateShadowTextPaint(fontSize, typeface);

            var lineHeight = fontSize * RenderConstants.LineHeightMultiplier;
            var safeWidth = slot.Width * RenderConstants.TextWidthMultiplier;
            var lines = TextUtils.FitTitleLines(title, titlePaint, safeWidth, slot.Height, lineHeight, config.LongTitleHandling);
            if (lines.Count == 0)
                return;

            var startY = CenteredBaseline(slot, lines.Count, fontSize, lineHeight);

            for (int i = 0; i < lines.Count; i++)
            {
                PaintFactory.DrawTextWithShadow(canvas, lines[i], slot.MidX, startY + (i * lineHeight), titlePaint, shadowPaint);
            }
        }

        // DrawSeparatorLine
        // Draws a horizontal separator line with shadow effect.
        private static void DrawSeparatorLine(PosterSettings config, SKCanvas canvas, SKRect slot)
        {
            var startX = slot.Left;
            var endX = slot.Right;
            var y = slot.MidY;

            using var shadowPaint = PaintFactory.CreateShadowLinePaint();
            using var linePaint = PaintFactory.CreateLinePaint(ColorUtils.ParseHexColor(config.EpisodeFontColor));

            PaintFactory.DrawLineWithShadow(canvas, startX, y, endX, y, linePaint, shadowPaint);
        }

        // DrawEpisodeInfo
        // Draws the season and episode numbers with a bullet separator.
        private static void DrawEpisodeInfo(SKCanvas canvas, int seasonNumber, int episodeNumber, PosterSettings config, int canvasHeight, SKRect slot)
        {
            var episodeFontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize, canvasHeight);
            var episodeColor = ColorUtils.ParseHexColor(config.EpisodeFontColor ?? "#FFFFFF");
            var episodeTypeface = FontUtils.ResolveTypeface(config.EffectiveEpisodeFontPath, config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle));
            var bulletTypeface = FontUtils.ResolveTypeface(config.EffectiveEpisodeFontPath, config.EpisodeFontFamily, SKFontStyle.Normal);

            using var episodePaint = PaintFactory.CreateTextPaint(episodeColor, episodeFontSize, episodeTypeface);
            using var shadowPaint = PaintFactory.CreateShadowTextPaint(episodeFontSize, episodeTypeface);
            using var bulletPaint = PaintFactory.CreateTextPaint(episodeColor, episodeFontSize, bulletTypeface);
            using var bulletShadowPaint = PaintFactory.CreateShadowTextPaint(episodeFontSize, bulletTypeface);

            var seasonText = seasonNumber.ToString(CultureInfo.InvariantCulture);
            var episodeText = episodeNumber.ToString(CultureInfo.InvariantCulture);
            var bulletText = " • ";

            var fontMetrics = episodePaint.FontMetrics;
            var baselineY = slot.Bottom - Math.Abs(fontMetrics.Descent);

            var seasonWidth = episodePaint.MeasureText(seasonText);
            var episodeWidth = episodePaint.MeasureText(episodeText);
            var bulletWidth = bulletPaint.MeasureText(bulletText);

            var centerX = slot.MidX;
            var bulletX = centerX;
            var seasonX = bulletX - (bulletWidth / 2f) - (seasonWidth / 2f);
            var episodeX = bulletX + (bulletWidth / 2f) + (episodeWidth / 2f);

            PaintFactory.DrawTextWithShadow(canvas, seasonText, seasonX, baselineY, episodePaint, shadowPaint);
            PaintFactory.DrawTextWithShadow(canvas, bulletText, bulletX, baselineY, bulletPaint, bulletShadowPaint);
            PaintFactory.DrawTextWithShadow(canvas, episodeText, episodeX, baselineY, episodePaint, shadowPaint);
        }
    }
}
