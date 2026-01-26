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
            var textArea = CalculateTextKeepClearArea(safeArea, settings, height);
            
            var strokeBuilder = new BrushStrokeUtil(episodeMetadata.EpisodeNumberStart ?? 1);
            float baseWidth = height * 0.12f;
            using var brushMask = strokeBuilder.BuildStrokePath(safeArea, textArea, baseWidth);
            
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

        // GenerateBrushStrokePath
        // Generates a random brush stroke path that avoids the text area.
        private SKPath GenerateBrushStrokePath(int width, int height, SKRect safeArea, SKRect textArea, int seed)
        {
            var random = new Random(seed);

            using var centerPath = new SKPath();

            float textTop = textArea.Top;
            float usableHeight = textTop - safeArea.Top - 100f;
            float startY = safeArea.Top + (usableHeight * 0.3f) + (float)(random.NextDouble() - 0.5) * usableHeight * 0.2f;
            float endY = safeArea.Top + (usableHeight * 0.7f) + (float)(random.NextDouble() - 0.5) * usableHeight * 0.2f;

            int segments = 150;
            float frequency = 8f + (float)random.NextDouble() * 4f;
            float tightAmplitude = usableHeight * 0.08f;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float x = safeArea.Left + (t * safeArea.Width);

                float baseY = startY + (endY - startY) * t;

                float tightSwoop = (float)Math.Sin(t * Math.PI * frequency) * tightAmplitude;

                float wobble = (float)(random.NextDouble() - 0.5) * 8f;

                float y = baseY + tightSwoop + wobble;

                if (i == 0)
                {
                    centerPath.MoveTo(x, y);
                }
                else
                {
                    centerPath.LineTo(x, y);
                }
            }

            float baseWidth = width * 0.16f;
            float widthVariation = (float)random.NextDouble() * 0.3f + 0.85f;

            using var strokePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = baseWidth * widthVariation,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true
            };

            return strokePaint.GetFillPath(centerPath);
        }

        // CalculateTextKeepClearArea
        // Calculates the area that should remain clear for text elements.
        private SKRect CalculateTextKeepClearArea(SKRect safeArea, PosterSettings settings, int height)
        {
            var episodeFontSize = FontUtils.CalculateFontSizeFromPercentage(settings.EpisodeFontSize, height, settings.PosterSafeArea);
            var titleFontSize = FontUtils.CalculateFontSizeFromPercentage(settings.TitleFontSize, height, settings.PosterSafeArea);
            
            var episodeHeight = episodeFontSize;
            var spacing = episodeFontSize * 0.3f;
            var titleHeight = titleFontSize * 2.5f;
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
            var typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle));
            
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
            var titleFontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height, config.PosterSafeArea);
            var titleHeight = titleFontSize * 2.5f;
            var spacing = fontSize * 0.3f;
            
            float x = safeArea.Left;
            float y = safeArea.Bottom - titleHeight - spacing - Math.Abs(metrics.Descent);
            
            canvas.DrawText(episodeCode, x + 2f, y + 2f, shadowPaint);
            canvas.DrawText(episodeCode, x, y, textPaint);
        }

        // DrawTitle
        // Draws the episode title in the bottom-left corner of the poster.
        private void DrawTitle(SKCanvas canvas, EpisodeMetadata episodeMetadata, PosterSettings config, SKRect safeArea, int height)
        {
            var title = episodeMetadata.EpisodeName;
            if (string.IsNullOrWhiteSpace(title))
                return;

            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height, config.PosterSafeArea);
            var typeface = FontUtils.CreateTypeface(config.TitleFontFamily, FontUtils.GetFontStyle(config.TitleFontStyle));
            
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