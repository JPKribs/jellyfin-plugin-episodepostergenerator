using System;
using System.Collections.Generic;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utilities;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    public class FrostedGlassPosterGenerator : BasePosterGenerator
    {
        // Style
        // The poster style this generator produces.
        public override PosterStyle Style => PosterStyle.FrostedGlass;

        // Description
        // A short, user facing description of this style shown in the configuration UI.
        public override string Description => "Episode text on a frosted glass panel that blurs the image behind it. Modern and readable on any background.";

        private readonly ILogger<FrostedGlassPosterGenerator> _logger;

        // The base canvas bitmap, captured during the canvas layer so the typography layer
        // can re-draw a blurred copy of it inside the panel. Generators are single-use
        // (one instance per Generate call), so holding the reference is safe.
        private SKBitmap? _canvasBitmap;

        // FrostedGlassPosterGenerator
        // Initializes a new instance of the frosted glass poster generator with logging support.
        public FrostedGlassPosterGenerator(ILogger<FrostedGlassPosterGenerator> logger)
        {
            _logger = logger;
        }

        // RenderCanvas
        // Draws the base canvas and captures it for the blurred panel rendering.
        protected override void RenderCanvas(SKCanvas skCanvas, SKBitmap canvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            _canvasBitmap = canvas;
            base.RenderCanvas(skCanvas, canvas, episodeMetadata, settings, width, height);
        }

        // RenderTypography
        // Renders the frosted panel with episode info and title centered inside it.
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            if (!settings.ShowTitle && !settings.ShowEpisode)
                return;

            var safeArea = GetSafeAreaBounds(width, height, settings);

            // Measure the panel content: optional episode code line above optional title lines.
            var titleFontSize = FontUtils.CalculateFontSizeFromPercentage(settings.TitleFontSize, height);
            var titleTypeface = FontUtils.ResolveTypeface(settings.EffectiveTitleFontPath, settings.TitleFontFamily, FontUtils.GetFontStyle(settings.TitleFontStyle));
            var episodeFontSize = FontUtils.CalculateFontSizeFromPercentage(settings.EpisodeFontSize, height);
            var episodeTypeface = FontUtils.ResolveTypeface(settings.EffectiveEpisodeFontPath, settings.EpisodeFontFamily, FontUtils.GetFontStyle(settings.EpisodeFontStyle));

            using var titlePaint = PaintFactory.CreateTextPaint(ColorUtils.ParseHexColor(settings.TitleFontColor), titleFontSize, titleTypeface);
            using var episodePaint = PaintFactory.CreateTextPaint(ColorUtils.ParseHexColor(settings.EpisodeFontColor), episodeFontSize, episodeTypeface);

            float padX = width * 0.04f;
            float padY = height * 0.03f;
            float maxTextWidth = safeArea.Width - (2 * padX);

            var titleLines = new List<string>();
            if (settings.ShowTitle && !string.IsNullOrEmpty(episodeMetadata.EpisodeName))
            {
                titleLines.AddRange(TextUtils.FitTextToWidth(episodeMetadata.EpisodeName, titlePaint, maxTextWidth));
            }

            string? episodeText = null;
            if (settings.ShowEpisode)
            {
                episodeText = EpisodeCodeUtils.FormatEpisodeCode(
                    episodeMetadata.SeasonNumber ?? 0,
                    episodeMetadata.EpisodeNumberStart ?? 0);
            }

            if (titleLines.Count == 0 && episodeText == null)
                return;

            float titleLineHeight = titleFontSize * RenderConstants.LineHeightMultiplier;
            float spacing = height * RenderConstants.DefaultSpacingRatio;

            float contentHeight = 0;
            if (episodeText != null)
                contentHeight += episodeFontSize;
            if (episodeText != null && titleLines.Count > 0)
                contentHeight += spacing;
            if (titleLines.Count > 0)
                contentHeight += (titleLines.Count - 1) * titleLineHeight + titleFontSize;

            float contentWidth = 0;
            if (episodeText != null)
                contentWidth = Math.Max(contentWidth, episodePaint.MeasureText(episodeText));
            foreach (var line in titleLines)
                contentWidth = Math.Max(contentWidth, titlePaint.MeasureText(line));

            float panelWidth = Math.Min(safeArea.Width, contentWidth + (2 * padX));
            float panelHeight = contentHeight + (2 * padY);
            float panelLeft = safeArea.MidX - (panelWidth / 2f);
            float panelTop = safeArea.Bottom - panelHeight;
            var panelRect = new SKRect(panelLeft, panelTop, panelLeft + panelWidth, panelTop + panelHeight);
            float cornerRadius = height * 0.02f;
            var roundedPanel = new SKRoundRect(panelRect, cornerRadius);

            DrawFrostedPanel(skCanvas, roundedPanel, episodeMetadata, settings, width, height);

            // Content, centered horizontally, stacked from the top padding down.
            float currentY = panelTop + padY;

            if (episodeText != null)
            {
                var metrics = episodePaint.FontMetrics;
                skCanvas.DrawText(episodeText, panelRect.MidX, currentY - metrics.Ascent, episodePaint);
                currentY += episodeFontSize + (titleLines.Count > 0 ? spacing : 0);
            }

            for (int i = 0; i < titleLines.Count; i++)
            {
                var metrics = titlePaint.FontMetrics;
                skCanvas.DrawText(titleLines[i], panelRect.MidX, currentY - metrics.Ascent, titlePaint);
                currentY += titleLineHeight;
            }
        }

        // DrawFrostedPanel
        // Fills the rounded panel with a blurred copy of the canvas (matching the poster's
        // overlay tint) plus a frost wash, then strokes a subtle border.
        private void DrawFrostedPanel(SKCanvas skCanvas, SKRoundRect panel, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            skCanvas.Save();
            skCanvas.ClipRoundRect(panel, antialias: true);

            if (_canvasBitmap != null)
            {
                float blurSigma = Math.Max(8f, width * 0.01f);
                using var blurPaint = new SKPaint
                {
                    IsAntialias = true,
                    FilterQuality = SKFilterQuality.High,
                    ImageFilter = SKImageFilter.CreateBlur(blurSigma, blurSigma)
                };
                skCanvas.DrawBitmap(_canvasBitmap, 0, 0, blurPaint);

                // Re-apply the overlay inside the clip so the blurred panel keeps the same
                // tint as the rest of the poster instead of showing the raw frame colors.
                RenderOverlay(skCanvas, episodeMetadata, settings, width, height);
            }

            using var frostPaint = PaintFactory.CreateFillPaint(SKColors.White.WithAlpha(38));
            skCanvas.DrawRect(panel.Rect, frostPaint);

            skCanvas.Restore();

            using var borderPaint = new SKPaint
            {
                Color = SKColors.White.WithAlpha(70),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(1.5f, height * 0.0015f),
                IsAntialias = true
            };
            skCanvas.DrawRoundRect(panel, borderPaint);
        }

        // LogError
        // Logs an error that occurred during frosted glass poster generation.
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate frosted glass poster for {EpisodeName}", episodeName);
        }
    }
}
