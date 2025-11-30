using System;
using System.Collections.Generic;
using SkiaSharp;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    /// <summary>
    /// Poster with random paintbrush strokes cut out from the overlay layer, revealing the canvas beneath.
    /// Features episode code and title in the bottom-left corner with brush strokes avoiding that area.
    /// </summary>
    public class BrushPosterGenerator : BasePosterGenerator
    {
        private readonly ILogger<BrushPosterGenerator> _logger;

        public BrushPosterGenerator(ILogger<BrushPosterGenerator> logger)
        {
            _logger = logger;
        }

        // MARK: RenderOverlay
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
            
            skCanvas.Save();
            
            var brushMask = GenerateRandomBrushStrokes(width, height, safeArea, textArea, episodeMetadata.EpisodeNumberStart ?? 1);
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

        // MARK: CalculateTextKeepClearArea
        private SKRect CalculateTextKeepClearArea(SKRect safeArea, PosterSettings settings, int height)
        {
            var episodeFontSize = FontUtils.CalculateFontSizeFromPercentage(settings.EpisodeFontSize, height, settings.PosterSafeArea);
            var titleFontSize = FontUtils.CalculateFontSizeFromPercentage(settings.TitleFontSize, height, settings.PosterSafeArea);
            
            var totalTextHeight = (episodeFontSize * 1.2f) + (titleFontSize * 2.5f);
            
            var textWidth = safeArea.Width * 0.5f;
            
            return new SKRect(
                safeArea.Left,
                safeArea.Bottom - totalTextHeight,
                safeArea.Left + textWidth,
                safeArea.Bottom
            );
        }

        // MARK: GenerateRandomBrushStrokes
        private SKPath GenerateRandomBrushStrokes(int width, int height, SKRect safeArea, SKRect textArea, int seed)
        {
            var random = new Random(seed);
            var combinedPath = new SKPath();
            
            int strokeCount = random.Next(4, 8);
            int maxAttempts = strokeCount * 3;
            int successfulStrokes = 0;
            
            for (int attempt = 0; attempt < maxAttempts && successfulStrokes < strokeCount; attempt++)
            {
                var strokePath = GenerateSingleBrushStroke(width, height, safeArea, textArea, random);
                
                if (strokePath != null)
                {
                    combinedPath.AddPath(strokePath);
                    successfulStrokes++;
                }
            }
            
            return combinedPath;
        }

        // MARK: GenerateSingleBrushStroke
        private SKPath? GenerateSingleBrushStroke(int width, int height, SKRect safeArea, SKRect textArea, Random random)
        {
            var path = new SKPath();
            
            float startX = (float)(safeArea.Left + random.NextDouble() * safeArea.Width);
            float startY = (float)(safeArea.Top + random.NextDouble() * safeArea.Height * 0.4);
            
            float endX = (float)(safeArea.Left + random.NextDouble() * safeArea.Width);
            float endY = (float)(safeArea.Top + safeArea.Height * 0.6 + random.NextDouble() * safeArea.Height * 0.4);
            
            if (IsStrokeTooCloseToTextArea(startX, startY, endX, endY, textArea))
            {
                return null;
            }
            
            float cp1X = startX + (float)((random.NextDouble() - 0.5) * safeArea.Width * 0.4);
            float cp1Y = startY + (safeArea.Height * 0.25f) + (float)((random.NextDouble() - 0.5) * safeArea.Height * 0.2);
            
            float cp2X = endX + (float)((random.NextDouble() - 0.5) * safeArea.Width * 0.4);
            float cp2Y = endY - (safeArea.Height * 0.25f) + (float)((random.NextDouble() - 0.5) * safeArea.Height * 0.2);
            
            int segments = 100;
            var points = new List<SKPoint>();
            var widths = new List<float>();
            
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                
                float x = CalculateCubicBezier(t, startX, cp1X, cp2X, endX);
                float y = CalculateCubicBezier(t, startY, cp1Y, cp2Y, endY);
                points.Add(new SKPoint(x, y));
                
                float baseWidth = width * 0.06f + (float)(random.NextDouble() * width * 0.03f);
                float variationFactor = (float)(0.7 + random.NextDouble() * 0.6);
                float pressureCurve = (float)(Math.Sin(t * Math.PI) * variationFactor);
                widths.Add(baseWidth * pressureCurve);
            }
            
            var leftPoints = new List<SKPoint>();
            var rightPoints = new List<SKPoint>();
            
            for (int i = 0; i < points.Count; i++)
            {
                SKPoint point = points[i];
                float strokeWidth = widths[i];
                
                SKPoint tangent;
                if (i < points.Count - 1)
                {
                    tangent = new SKPoint(
                        points[i + 1].X - point.X,
                        points[i + 1].Y - point.Y
                    );
                }
                else
                {
                    tangent = new SKPoint(
                        point.X - points[i - 1].X,
                        point.Y - points[i - 1].Y
                    );
                }
                
                float length = (float)Math.Sqrt(tangent.X * tangent.X + tangent.Y * tangent.Y);
                if (length > 0)
                {
                    tangent.X /= length;
                    tangent.Y /= length;
                }
                
                SKPoint normal = new SKPoint(-tangent.Y, tangent.X);
                
                leftPoints.Add(new SKPoint(
                    point.X + normal.X * strokeWidth,
                    point.Y + normal.Y * strokeWidth
                ));
                rightPoints.Add(new SKPoint(
                    point.X - normal.X * strokeWidth,
                    point.Y - normal.Y * strokeWidth
                ));
            }
            
            path.MoveTo(leftPoints[0]);
            for (int i = 1; i < leftPoints.Count; i++)
            {
                path.LineTo(leftPoints[i]);
            }
            
            for (int i = rightPoints.Count - 1; i >= 0; i--)
            {
                path.LineTo(rightPoints[i]);
            }
            
            path.Close();
            return path;
        }

        // MARK: IsStrokeTooCloseToTextArea
        private bool IsStrokeTooCloseToTextArea(float startX, float startY, float endX, float endY, SKRect textArea)
        {
            float buffer = 50f;
            var expandedTextArea = new SKRect(
                textArea.Left - buffer,
                textArea.Top - buffer,
                textArea.Right + buffer,
                textArea.Bottom + buffer
            );
            
            return expandedTextArea.Contains(startX, startY) || 
                   expandedTextArea.Contains(endX, endY) ||
                   (startY > expandedTextArea.Top && endY > expandedTextArea.Top);
        }

        // MARK: CalculateCubicBezier
        private float CalculateCubicBezier(float t, float p0, float p1, float p2, float p3)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            
            return uuu * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + ttt * p3;
        }

        // MARK: RenderTypography
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            var safeArea = GetSafeAreaBounds(width, height, settings);
            
            DrawEpisodeCode(skCanvas, episodeMetadata, settings, safeArea, height);
            DrawTitle(skCanvas, episodeMetadata, settings, safeArea, height);
        }

        // MARK: DrawEpisodeCode
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
            
            float x = safeArea.Left;
            float y = safeArea.Bottom - titleHeight - (fontSize * 0.2f) - metrics.Ascent;
            
            canvas.DrawText(episodeCode, x + 2f, y + 2f, shadowPaint);
            canvas.DrawText(episodeCode, x, y, textPaint);
        }

        // MARK: DrawTitle
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
            float y = safeArea.Bottom - metrics.Ascent;
            
            foreach (var line in lines)
            {
                canvas.DrawText(line, x + 2f, y + 2f, shadowPaint);
                canvas.DrawText(line, x, y, titlePaint);
                y += lineHeight;
            }
        }

        // MARK: LogError
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate Brush poster for episode {EpisodeName}", episodeName);
        }
    }
}