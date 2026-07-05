using System;
using System.Globalization;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utilities;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    public class FadePosterGenerator : BasePosterGenerator
    {
        // Portion of the poster width the overlay stays fully solid, and where it ends.
        private const float SolidStop = 0.4f;
        private const float TransparentStop = 0.75f;

        // Style
        // The poster style this generator produces.
        public override PosterStyle Style => PosterStyle.Fade;

        // Description
        // A short, user facing description of this style shown in the configuration UI.
        public override string Description => "One sided fade with a big episode number and a vertical title. Editorial and bold.";

        private readonly ILogger<FadePosterGenerator> _logger;

        // FadePosterGenerator
        // Initializes a new instance of the fade poster generator with logging support.
        public FadePosterGenerator(ILogger<FadePosterGenerator> logger)
        {
            _logger = logger;
        }

        // RenderOverlay
        // Draws a hard one-sided gradient: solid overlay color on the left that holds until
        // partway across, then falls off to transparent so the right side shows the frame.
        protected override void RenderOverlay(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            if (string.IsNullOrEmpty(settings.OverlayColor))
                return;

            var primaryColor = ColorUtils.ParseHexColor(settings.OverlayColor);
            if (primaryColor.Alpha == 0)
                return;

            var rect = SKRect.Create(width, height);
            var colors = new[] { primaryColor, primaryColor, primaryColor.WithAlpha(0) };
            var positions = new[] { 0f, SolidStop, TransparentStop };

            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.MidY),
                new SKPoint(rect.Right, rect.MidY),
                colors, positions, SKShaderTileMode.Clamp);

            using var overlayPaint = new SKPaint
            {
                Shader = shader,
                Style = SKPaintStyle.Fill,
                IsDither = true
            };
            skCanvas.DrawRect(rect, overlayPaint);
        }

        // RenderTypography
        // Renders a large episode number at the bottom-left with the title rotated
        // vertically along the left edge above it.
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            var safeArea = GetSafeAreaBounds(width, height, settings);
            float numberTop = safeArea.Bottom;

            if (settings.ShowEpisode)
            {
                numberTop = DrawEpisodeNumber(skCanvas, episodeMetadata, settings, safeArea);
            }

            if (settings.ShowTitle && !string.IsNullOrEmpty(episodeMetadata.EpisodeName))
            {
                DrawVerticalTitle(skCanvas, episodeMetadata.EpisodeName, settings, height, safeArea, numberTop);
            }
        }

        // DrawEpisodeNumber
        // Draws the zero-padded episode number large at the bottom-left of the safe area.
        // Returns the top edge of the drawn number so the title can stack above it.
        private static float DrawEpisodeNumber(SKCanvas canvas, EpisodeMetadata episodeMetadata, PosterSettings config, SKRect safeArea)
        {
            var numberText = (episodeMetadata.EpisodeNumberStart ?? 0).ToString("D2", CultureInfo.InvariantCulture);
            var typeface = FontUtils.ResolveTypeface(config.EffectiveEpisodeFontPath, config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle));

            // The number is the focal element: auto-size it to the solid side of the fade.
            float maxWidth = safeArea.Width * 0.45f;
            float maxHeight = safeArea.Height * 0.3f;
            float fontSize = FontUtils.CalculateOptimalFontSize(numberText, typeface, maxWidth, maxHeight);

            using var numberPaint = PaintFactory.CreateTextPaint(ColorUtils.ParseHexColor(config.EpisodeFontColor), fontSize, typeface, SKTextAlign.Left);
            using var shadowPaint = PaintFactory.CreateShadowTextPaint(fontSize, typeface, SKTextAlign.Left);

            var bounds = FontUtils.MeasureTextDimensions(numberText, typeface, fontSize);
            float baselineY = safeArea.Bottom;

            PaintFactory.DrawTextWithShadow(canvas, numberText, safeArea.Left, baselineY, numberPaint, shadowPaint);

            return baselineY - bounds.Height;
        }

        // DrawVerticalTitle
        // Draws the uppercase title rotated 90 degrees, running upward along the left edge
        // from just above the episode number, truncated to the available vertical space.
        private static void DrawVerticalTitle(SKCanvas canvas, string title, PosterSettings config, int height, SKRect safeArea, float numberTop)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height);
            var typeface = FontUtils.ResolveTypeface(config.EffectiveTitleFontPath, config.TitleFontFamily, FontUtils.GetFontStyle(config.TitleFontStyle));

            using var titlePaint = PaintFactory.CreateTextPaint(ColorUtils.ParseHexColor(config.TitleFontColor), fontSize, typeface, SKTextAlign.Left);
            using var shadowPaint = PaintFactory.CreateShadowTextPaint(fontSize, typeface, SKTextAlign.Left);

            float spacing = height * RenderConstants.DefaultSpacingRatio;
            float startY = numberTop - spacing;
            float availableRun = startY - safeArea.Top;
            if (availableRun <= fontSize)
                return;

            var text = TextUtils.FitTitleLine(title.ToUpperInvariant(), titlePaint, availableRun, config.LongTitleHandling);
            if (text == null)
                return;

            // Rotate around the anchor so the text runs bottom-to-top along the left edge.
            // The baseline sits on the anchor's vertical line; shift right by the ascent so
            // glyphs stay inside the safe area.
            float anchorX = safeArea.Left + Math.Abs(titlePaint.FontMetrics.Ascent);
            canvas.Save();
            canvas.RotateDegrees(-90, anchorX, startY);
            PaintFactory.DrawTextWithShadow(canvas, text, anchorX, startY, titlePaint, shadowPaint);
            canvas.Restore();
        }

        // LogError
        // Logs an error that occurred during fade poster generation.
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate fade poster for {EpisodeName}", episodeName);
        }
    }
}
