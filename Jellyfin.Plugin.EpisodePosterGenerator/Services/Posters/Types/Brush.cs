using System;
using System.Collections.Generic;
using SkiaSharp;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    public class BrushPosterGenerator : BasePosterGenerator
    {
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
            var strokeBuilder = new BrushStrokeUtil(seed);
            using var brushMask = strokeBuilder.BuildStrokePath(safeArea, textArea, height);
            
            skCanvas.Save();
            skCanvas.ClipPath(brushMask, SKClipOperation.Difference, antialias: true);

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
            
            skCanvas.Restore();
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
            var episodeFontSize = FontUtils.CalculateFontSizeFromPercentage(settings.EpisodeFontSize, height, settings.PosterSafeArea);

            var episodeHeight = episodeFontSize;
            var spacing = episodeFontSize * 0.3f;
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
            var episodeCode = EpisodeCodeUtil.FormatEpisodeCode(
                episodeMetadata.SeasonNumber ?? 0,
                episodeMetadata.EpisodeNumberStart ?? 0);
            
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize, height, config.PosterSafeArea);
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
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 1.5f)
            };
            
            var metrics = textPaint.FontMetrics;
            var spacing = fontSize * 0.3f;

            // Reserve only the title's ACTUAL rendered height (it may wrap to 1+ lines),
            // not a fixed multiple, so the gap between the code and title stays tight
            // regardless of title length.
            var titleHeight = MeasureTitleHeight(episodeMetadata, config, safeArea, height);

            float x = safeArea.Left;
            float y = safeArea.Bottom - titleHeight - spacing - Math.Abs(metrics.Descent);
            
            canvas.DrawText(episodeCode, x + 2f, y + 2f, shadowPaint);
            canvas.DrawText(episodeCode, x, y, textPaint);
        }

        // MeasureTitleHeight
        // Returns the title's actual rendered height using the same font, wrapping, and line
        // height as DrawTitle, so the episode code can sit directly above it. Returns 0 when
        // there is no title.
        private float MeasureTitleHeight(EpisodeMetadata episodeMetadata, PosterSettings config, SKRect safeArea, int height)
        {
            var title = episodeMetadata.EpisodeName;
            if (string.IsNullOrWhiteSpace(title))
                return 0f;

            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height, config.PosterSafeArea);
            var typeface = FontUtils.ResolveTypeface(config.EffectiveTitleFontPath, config.TitleFontFamily, FontUtils.GetFontStyle(config.TitleFontStyle));

            using var titlePaint = new SKPaint
            {
                TextSize = fontSize,
                Typeface = typeface,
                TextAlign = SKTextAlign.Left
            };

            var maxTextWidth = safeArea.Width * 0.6f;
            var lines = TextUtils.FitTextToWidth(title, titlePaint, maxTextWidth);
            float lineHeight = fontSize * 1.2f;
            return lines.Count * lineHeight;
        }

        // DrawTitle
        // Draws the episode title in the bottom-left corner of the poster.
        private void DrawTitle(SKCanvas canvas, EpisodeMetadata episodeMetadata, PosterSettings config, SKRect safeArea, int height)
        {
            var title = episodeMetadata.EpisodeName;
            if (string.IsNullOrWhiteSpace(title))
                return;

            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height, config.PosterSafeArea);
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
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 1.5f)
            };
            
            var maxTextWidth = safeArea.Width * 0.6f;
            var lines = TextUtils.FitTextToWidth(title, titlePaint, maxTextWidth);
            
            var metrics = titlePaint.FontMetrics;
            float lineHeight = fontSize * 1.2f;
            float x = safeArea.Left;
            float y = safeArea.Bottom - Math.Abs(metrics.Descent);
            
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                canvas.DrawText(lines[i], x + 2f, y + 2f, shadowPaint);
                canvas.DrawText(lines[i], x, y, titlePaint);
                y -= lineHeight;
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