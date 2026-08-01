using System;
using System.Collections.Generic;
using SkiaSharp;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utilities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    public class BrushPosterGenerator : BasePosterGenerator
    {
        // Style
        // The poster style this generator produces.
        public override PosterStyle Style => PosterStyle.Brush;

        // Description
        // A short, user facing description of this style shown in the configuration UI.
        public override string Description => "Brush strokes reveal the image through a flat overlay. Painted, editorial look.";

        private readonly ILogger<BrushPosterGenerator> _logger;

        // BrushPosterGenerator
        // Initializes a new instance of the brush poster generator with logging support.
        public BrushPosterGenerator(ILogger<BrushPosterGenerator> logger)
        {
            _logger = logger;
        }

        // RenderOverlay
        // Creates an overlay with brush stroke cutouts revealing the canvas beneath.
        protected override void RenderOverlay(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            if (string.IsNullOrEmpty(settings.OverlayColor))
                return;

            var primaryColor = ColorUtils.ParseHexColor(settings.OverlayColor);
            if (primaryColor.Alpha == 0)
                return;

            var rect = SKRect.Create(width, height);
            var safeArea = GetSafeAreaBounds(width, height, settings);
            var textArea = CalculateTextKeepClearArea(safeArea, settings, height, episodeMetadata);

            // Seed from the episode's file path so the same episode always produces the
            // same stroke layout, but different episodes vary. Falls back to series id +
            // season + episode if the file path isn't populated (e.g. demo generator).
            var seed = GenerateBrushSeed(episodeMetadata);
            var strokeBuilder = new BrushStrokeBuilder(seed);
            using var brushMask = strokeBuilder.BuildStrokePath(safeArea, textArea, height);

            // Draw the overlay into its own layer, then erase the stroke mask out of it with
            // a slightly blurred punch. The feathered edge reads as paint on canvas; a hard
            // ClipPath edge reads as a digital cut.
            skCanvas.SaveLayer(null);

            if (settings.OverlayGradient == OverlayGradient.None)
            {
                using var overlayPaint = new SKPaint
                {
                    Color = primaryColor,
                    Style = SKPaintStyle.Fill
                };
                skCanvas.DrawRect(rect, overlayPaint);
            }
            else
            {
                var secondaryColor = ColorUtils.ParseHexColor(settings.OverlaySecondaryColor);
                if (secondaryColor.Alpha == 0) secondaryColor = primaryColor;

                var gradient = CreateOverlayGradient(settings.OverlayGradient, rect, primaryColor, secondaryColor);
                if (gradient != null)
                {
                    using var overlayPaint = new SKPaint
                    {
                        Shader = gradient,
                        Style = SKPaintStyle.Fill,
                        IsDither = true
                    };
                    skCanvas.DrawRect(rect, overlayPaint);
                }
            }

            // Sigma scales with the poster, so this one cannot use the shared cached filter.
            // SKPaint does not own its mask filter, hence the explicit using.
            using var punchBlur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, Math.Max(2f, height * 0.002f));
            using var punchPaint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Fill,
                BlendMode = SKBlendMode.DstOut,
                IsAntialias = true,
                MaskFilter = punchBlur
            };
            skCanvas.DrawPath(brushMask, punchPaint);

            skCanvas.Restore();

            // Optional outline tracing the stroke edge, sharing the Cutout style's border toggle
            // and its contrast rule. Drawn after the layer is restored: inside it, the DstOut
            // punch above would erase the outline along with the overlay.
            if (settings.CutoutBorder)
            {
                using var outlinePaint = new SKPaint
                {
                    Color = ColorUtils.GetContrastingOutline(primaryColor),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = Math.Max(1f, height * 0.003f),
                    IsAntialias = true,
                    StrokeCap = SKStrokeCap.Round,
                    StrokeJoin = SKStrokeJoin.Round
                };
                skCanvas.DrawPath(brushMask, outlinePaint);
            }
        }

        // GenerateBrushSeed
        // Produces a deterministic int seed for an episode using a stable FNV-1a hash of
        // the episode's file path. The string overload of GetHashCode() is randomized per
        // process on modern .NET, so we hash the bytes ourselves to keep posters stable
        // across server restarts.
        private static int GenerateBrushSeed(EpisodeMetadata metadata)
        {
            var path = metadata.VideoMetadata?.EpisodeFilePath;
            if (!string.IsNullOrEmpty(path))
            {
                unchecked
                {
                    int hash = (int)2166136261;
                    foreach (char c in path)
                    {
                        hash ^= c;
                        hash *= 16777619;
                    }
                    return hash;
                }
            }

            int fallback = 0;
            if (metadata.SeriesId != Guid.Empty)
            {
                var bytes = metadata.SeriesId.ToByteArray();
                fallback = BitConverter.ToInt32(bytes, 0)
                    ^ BitConverter.ToInt32(bytes, 4)
                    ^ BitConverter.ToInt32(bytes, 8)
                    ^ BitConverter.ToInt32(bytes, 12);
            }
            fallback = (fallback * 397) ^ (metadata.SeasonNumber ?? 0);
            fallback = (fallback * 397) ^ (metadata.EpisodeNumberStart ?? 1);
            return fallback;
        }

        // CalculateTextKeepClearArea
        // Calculates the area that should remain clear for text elements.
        private SKRect CalculateTextKeepClearArea(SKRect safeArea, PosterSettings settings, int height, EpisodeMetadata episodeMetadata)
        {
            var episodeFontSize = FontUtils.CalculateFontSizeFromPercentage(settings.EpisodeFontSize, height);

            var episodeHeight = episodeFontSize;
            var spacing = GetElementSpacing(settings, height);
            var titleHeight = MeasureTitleHeight(episodeMetadata, settings, safeArea, height);
            var totalTextHeight = episodeHeight + spacing + titleHeight;
            
            var textWidth = safeArea.Width * 0.5f;
            
            return new SKRect(
                safeArea.Left,
                safeArea.Bottom - totalTextHeight,
                safeArea.Left + textWidth,
                safeArea.Bottom
            );
        }

        // RenderTypography
        // Renders the episode code and title text on the poster.
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            var safeArea = GetSafeAreaBounds(width, height, settings);
            
            DrawEpisodeCode(skCanvas, episodeMetadata, settings, safeArea, height);
            DrawTitle(skCanvas, episodeMetadata, settings, safeArea, height);
        }

        // DrawEpisodeCode
        // Draws the episode code in the bottom-left corner of the poster.
        private void DrawEpisodeCode(SKCanvas canvas, EpisodeMetadata episodeMetadata, PosterSettings config, SKRect safeArea, int height)
        {
            var episodeCode = EpisodeCodeUtils.FormatEpisodeCode(
                episodeMetadata.SeasonNumber ?? 0,
                episodeMetadata.EpisodeNumberStart ?? 0);
            
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize, height);
            var typeface = FontUtils.ResolveTypeface(config.EffectiveEpisodeFontPath, config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle));

            var textColor = ColorUtils.ParseHexColor(config.EpisodeFontColor);
            var shadowColor = SKColors.Black.WithAlpha(180);
            
            using var textPaint = new SKPaint
            {
                Color = textColor,
                TextSize = fontSize,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                Typeface = typeface,
                TextAlign = SKTextAlign.Left
            };
            
            using var shadowPaint = new SKPaint
            {
                Color = shadowColor,
                TextSize = fontSize,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                Typeface = typeface,
                TextAlign = SKTextAlign.Left,
                MaskFilter = PaintFactory.ShadowBlur
            };
            
            var metrics = textPaint.FontMetrics;
            var spacing = GetElementSpacing(config, height);

            // Sits above a fixed two line reservation rather than the title's actual height,
            // so the code lands in the same place on every episode regardless of how long its
            // title is. DrawTitle fills that block from the top, so a one line title still
            // renders directly beneath this.
            var titleHeight = MeasureTitleHeight(episodeMetadata, config, safeArea, height);

            float x = safeArea.Left;
            float y = safeArea.Bottom - titleHeight - spacing - Math.Abs(metrics.Descent);
            
            canvas.DrawText(episodeCode, x + 2f, y + 2f, shadowPaint);
            canvas.DrawText(episodeCode, x, y, textPaint);
        }

        // MeasureTitleHeight
        // Returns a fixed two line title reservation so the episode code and the stroke
        // keep clear area sit at the same spot no matter how many lines the title used
        // or whether a long title was dropped. Returns 0 when there is no title.
        private float MeasureTitleHeight(EpisodeMetadata episodeMetadata, PosterSettings config, SKRect safeArea, int height)
        {
            if (!config.ShowTitle || string.IsNullOrWhiteSpace(episodeMetadata.EpisodeName))
                return 0f;

            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height);
            return 2f * fontSize * 1.2f;
        }

        // DrawTitle
        // Draws the episode title in the bottom-left corner of the poster.
        private void DrawTitle(SKCanvas canvas, EpisodeMetadata episodeMetadata, PosterSettings config, SKRect safeArea, int height)
        {
            var title = episodeMetadata.EpisodeName;
            if (!config.ShowTitle || string.IsNullOrWhiteSpace(title))
                return;

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
                TextAlign = SKTextAlign.Left
            };
            
            using var shadowPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(180),
                TextSize = fontSize,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                Typeface = typeface,
                TextAlign = SKTextAlign.Left,
                MaskFilter = PaintFactory.ShadowBlur
            };
            
            var maxTextWidth = safeArea.Width * 0.6f;
            var lines = TextUtils.FitTitleLines(title, titlePaint, maxTextWidth, config.LongTitleHandling);
            if (lines.Count == 0)
                return;

            var metrics = titlePaint.FontMetrics;
            float lineHeight = fontSize * 1.2f;
            float x = safeArea.Left;

            // The block below the episode code is a fixed two lines tall so the code above it never
            // shifts between episodes. A one line title leaves a line of slack, split evenly above
            // and below by CenteredBaseline rather than pooled at one end.
            float blockHeight = MeasureTitleHeight(episodeMetadata, config, safeArea, height);
            var slot = SKRect.Create(safeArea.Left, safeArea.Bottom - blockHeight, safeArea.Width, blockHeight);
            float y = CenteredBaseline(slot, lines.Count, fontSize, lineHeight) - Math.Abs(metrics.Descent);

            foreach (var line in lines)
            {
                canvas.DrawText(line, x + 2f, y + 2f, shadowPaint);
                canvas.DrawText(line, x, y, titlePaint);
                y += lineHeight;
            }
        }

        // LogError
        // Logs an error that occurred during brush poster generation.
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate Brush poster for episode {EpisodeName}", episodeName);
        }
    }
}