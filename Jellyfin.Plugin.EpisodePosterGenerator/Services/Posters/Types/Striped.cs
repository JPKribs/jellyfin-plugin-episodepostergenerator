using System;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utilities;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    public class StripedPosterGenerator : BasePosterGenerator
    {
        // Band geometry, as ratios of poster height. The sash is drawn wider than the
        // canvas so its ends stay covered at the tilt angle.
        private const float BandAngleDegrees = -7f;
        private const float BandCenterYRatio = 0.74f;
        private const float BandHeightRatio = 0.14f;
        private const float PinstripeHeightRatio = 0.018f;
        private const float PinstripeGapRatio = 0.018f;

        // Style
        // The poster style this generator produces.
        public override PosterStyle Style => PosterStyle.Striped;

        // Description
        // A short, user facing description of this style shown in the configuration UI.
        public override string Description => "Tilted color sash with pinstripes carrying the episode title across the image. Sporty and graphic.";

        private readonly ILogger<StripedPosterGenerator> _logger;

        // StripedPosterGenerator
        // Initializes a new instance of the striped poster generator with logging support.
        public StripedPosterGenerator(ILogger<StripedPosterGenerator> logger)
        {
            _logger = logger;
        }

        // RenderOverlay
        // Draws the tilted sash: a solid main band with a thin pinstripe above and below,
        // using the overlay color for the band and the secondary color for the pinstripes.
        protected override void RenderOverlay(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            if (string.IsNullOrEmpty(settings.OverlayColor))
                return;

            var bandColor = ColorUtils.ParseHexColor(settings.OverlayColor);
            if (bandColor.Alpha == 0)
                return;

            var pinstripeColor = ColorUtils.ParseHexColor(settings.OverlaySecondaryColor);
            if (pinstripeColor.Alpha == 0)
                pinstripeColor = bandColor;

            float bandCenterY = height * BandCenterYRatio;
            float bandHeight = height * BandHeightRatio;
            float pinHeight = height * PinstripeHeightRatio;
            float pinGap = height * PinstripeGapRatio;

            // Overdraw horizontally so the tilted band's ends never expose the corners.
            float overdraw = width * 0.25f;

            skCanvas.Save();
            skCanvas.RotateDegrees(BandAngleDegrees, width / 2f, bandCenterY);

            using var bandPaint = PaintFactory.CreateFillPaint(bandColor);
            using var pinPaint = PaintFactory.CreateFillPaint(pinstripeColor);

            float bandTop = bandCenterY - (bandHeight / 2f);
            skCanvas.DrawRect(new SKRect(-overdraw, bandTop, width + overdraw, bandTop + bandHeight), bandPaint);
            skCanvas.DrawRect(new SKRect(-overdraw, bandTop - pinGap - pinHeight, width + overdraw, bandTop - pinGap), pinPaint);
            skCanvas.DrawRect(new SKRect(-overdraw, bandTop + bandHeight + pinGap, width + overdraw, bandTop + bandHeight + pinGap + pinHeight), pinPaint);

            skCanvas.Restore();
        }

        // RenderTypography
        // Draws the episode title along the sash and the episode code in the top-right
        // corner. When the title is disabled the episode code rides the sash instead.
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            var safeArea = GetSafeAreaBounds(width, height, settings);
            var episodeCode = EpisodeCodeUtils.FormatEpisodeCode(
                episodeMetadata.SeasonNumber ?? 0,
                episodeMetadata.EpisodeNumberStart ?? 0);

            bool titleOnBand = settings.ShowTitle && !string.IsNullOrEmpty(episodeMetadata.EpisodeName);

            if (titleOnBand)
            {
                DrawBandText(skCanvas, episodeMetadata.EpisodeName!, settings, width, height,
                    settings.EffectiveTitleFontPath, settings.TitleFontFamily, settings.TitleFontStyle,
                    settings.TitleFontSize, settings.TitleFontColor, safeArea);
            }
            else if (settings.ShowEpisode)
            {
                DrawBandText(skCanvas, episodeCode, settings, width, height,
                    settings.EffectiveEpisodeFontPath, settings.EpisodeFontFamily, settings.EpisodeFontStyle,
                    settings.EpisodeFontSize, settings.EpisodeFontColor, safeArea);
                return;
            }

            if (settings.ShowEpisode && titleOnBand)
            {
                DrawCornerEpisodeCode(skCanvas, episodeCode, settings, height, safeArea);
            }
        }

        // DrawBandText
        // Draws a single line of text centered along the tilted sash, sized to fit the
        // band height and truncated with an ellipsis to the safe width.
        private static void DrawBandText(SKCanvas canvas, string text, PosterSettings settings, int width, int height,
            string? fontPath, string fontFamily, string fontStyle, float fontSizePercent, string fontColor, SKRect safeArea)
        {
            var typeface = FontUtils.ResolveTypeface(fontPath, fontFamily, FontUtils.GetFontStyle(fontStyle));

            float bandCenterY = height * BandCenterYRatio;
            float bandHeight = height * BandHeightRatio;
            float maxTextWidth = safeArea.Width * RenderConstants.TextWidthMultiplier;

            // The configured size is a ceiling; the band height caps it so text never
            // spills off the sash.
            float configuredSize = FontUtils.CalculateFontSizeFromPercentage(fontSizePercent, height);
            float fontSize = Math.Min(configuredSize, bandHeight * 0.55f);

            using var textPaint = PaintFactory.CreateTextPaint(ColorUtils.ParseHexColor(fontColor), fontSize, typeface);
            using var shadowPaint = PaintFactory.CreateShadowTextPaint(fontSize, typeface);

            var line = TextUtils.TruncateWithEllipsis(text, textPaint, maxTextWidth);

            var metrics = textPaint.FontMetrics;
            float baselineY = bandCenterY - ((metrics.Ascent + metrics.Descent) / 2f);

            canvas.Save();
            canvas.RotateDegrees(BandAngleDegrees, width / 2f, bandCenterY);
            PaintFactory.DrawTextWithShadow(canvas, line, width / 2f, baselineY, textPaint, shadowPaint);
            canvas.Restore();
        }

        // DrawCornerEpisodeCode
        // Draws the episode code horizontally in the top-right corner of the safe area,
        // deliberately unrotated to contrast with the tilted sash.
        private static void DrawCornerEpisodeCode(SKCanvas canvas, string episodeCode, PosterSettings settings, int height, SKRect safeArea)
        {
            var typeface = FontUtils.ResolveTypeface(settings.EffectiveEpisodeFontPath, settings.EpisodeFontFamily, FontUtils.GetFontStyle(settings.EpisodeFontStyle));
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(settings.EpisodeFontSize, height);

            using var textPaint = PaintFactory.CreateTextPaint(ColorUtils.ParseHexColor(settings.EpisodeFontColor), fontSize, typeface, SKTextAlign.Right);
            using var shadowPaint = PaintFactory.CreateShadowTextPaint(fontSize, typeface, SKTextAlign.Right);

            float baselineY = safeArea.Top - textPaint.FontMetrics.Ascent;
            PaintFactory.DrawTextWithShadow(canvas, episodeCode, safeArea.Right, baselineY, textPaint, shadowPaint);
        }

        // LogError
        // Logs an error that occurred during striped poster generation.
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate striped poster for {EpisodeName}", episodeName);
        }
    }
}
