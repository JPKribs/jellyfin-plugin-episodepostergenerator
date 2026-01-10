using System;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using SkiaSharp;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    /// <summary>
    /// Generates frame-style posters with decorative border and episode information.
    /// </summary>
    public class FramePosterGenerator : BasePosterGenerator
    {
        private readonly ILogger<FramePosterGenerator> _logger;

        public FramePosterGenerator(ILogger<FramePosterGenerator> logger)
        {
            _logger = logger;
        }

        // MARK: RenderTypography
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            if (string.IsNullOrEmpty(episodeMetadata.EpisodeName))
                return;

            var safeArea = GetSafeAreaBounds(width, height, settings);
            float spacing = height * 0.02f;

            var titleInfo = DrawEpisodeTitle(skCanvas, episodeMetadata.EpisodeName, settings, width, height, safeArea);

            TextInfo? episodeInfo = null;
            if (settings.ShowEpisode && episodeMetadata.SeasonNumber.HasValue && episodeMetadata.EpisodeNumberStart.HasValue)
            {
                episodeInfo = DrawEpisodeInfo(skCanvas, episodeMetadata.SeasonNumber.Value, episodeMetadata.EpisodeNumberStart.Value, settings, width, height, safeArea);
            }

            DrawFrameBorder(skCanvas, safeArea, titleInfo, episodeInfo, spacing);
        }

        // MARK: LogError
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate frame poster for {EpisodeName}", episodeName);
        }

        // MARK: DrawEpisodeTitle
        private TextInfo DrawEpisodeTitle(SKCanvas canvas, string title, PosterSettings config, int width, int height, SKRect safeArea)
        {
            title = title.ToUpperInvariant();
            
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height);
            var typeface = FontUtils.CreateTypeface(config.TitleFontFamily, FontUtils.GetFontStyle(config.TitleFontStyle));

            using var titlePaint = new SKPaint
            {
                Color = ColorUtils.ParseHexColor(config.TitleFontColor),
                TextSize = fontSize,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center
            };

            using var shadowPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(180),
                TextSize = fontSize,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center
            };

            var availableWidth = safeArea.Width * 0.9f;
            var lines = TextUtils.FitTextToWidth(title, titlePaint, availableWidth);

            var lineHeight = fontSize * 1.2f;
            var totalHeight = (lines.Count - 1) * lineHeight + fontSize;
            var centerX = safeArea.MidX;
            
            var fontMetrics = titlePaint.FontMetrics;
            var textActualHeight = Math.Abs(fontMetrics.Ascent) + Math.Abs(fontMetrics.Descent);
            var textPadding = height * 0.01f;
            var startY = safeArea.Top + textPadding + Math.Abs(fontMetrics.Ascent);

            float maxTextWidth = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                var lineY = startY + (i * lineHeight);
                canvas.DrawText(lines[i], centerX + 2, lineY + 2, shadowPaint);
                canvas.DrawText(lines[i], centerX, lineY, titlePaint);

                var textWidth = titlePaint.MeasureText(lines[i]);
                if (textWidth > maxTextWidth)
                    maxTextWidth = textWidth;
            }

            var totalActualHeight = (lines.Count - 1) * lineHeight + textActualHeight;

            return new TextInfo
            {
                Height = totalActualHeight,
                Width = maxTextWidth,
                CenterX = centerX,
                Y = safeArea.Top + textPadding
            };
        }

        // MARK: DrawEpisodeInfo
        private TextInfo DrawEpisodeInfo(SKCanvas canvas, int seasonNumber, int episodeNumber, PosterSettings config, int width, int height, SKRect safeArea)
        {
            var episodeFontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize, height);
            var episodeColor = ColorUtils.ParseHexColor(config.EpisodeFontColor ?? "#FFFFFF");
            var shadowColor = SKColors.Black.WithAlpha(180);

            using var episodePaint = new SKPaint
            {
                Color = episodeColor,
                TextSize = episodeFontSize,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
                TextAlign = SKTextAlign.Center,
                TextEncoding = SKTextEncoding.Utf8
            };

            using var shadowPaint = new SKPaint
            {
                Color = shadowColor,
                TextSize = episodeFontSize,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
                TextAlign = SKTextAlign.Center,
                TextEncoding = SKTextEncoding.Utf8
            };

            var episodeText = EpisodeCodeUtil.FormatFullText(seasonNumber, episodeNumber, true, true);
            var centerX = safeArea.MidX;

            var fontMetrics = episodePaint.FontMetrics;
            var textActualHeight = Math.Abs(fontMetrics.Ascent) + Math.Abs(fontMetrics.Descent);
            var textPadding = height * 0.01f;
            var y = safeArea.Bottom - textPadding - Math.Abs(fontMetrics.Descent);

            canvas.DrawText(episodeText, centerX + 2, y + 2, shadowPaint);
            canvas.DrawText(episodeText, centerX, y, episodePaint);

            var textWidth = episodePaint.MeasureText(episodeText);

            return new TextInfo
            {
                Height = textActualHeight,
                Width = textWidth,
                CenterX = centerX,
                Y = safeArea.Bottom - textPadding - textActualHeight
            };
        }

        // MARK: DrawFrameBorder
        private void DrawFrameBorder(SKCanvas canvas, SKRect safeArea, TextInfo titleInfo, TextInfo? episodeInfo, float spacing)
        {
            using var borderPaint = new SKPaint
            {
                Color = SKColors.White,
                StrokeWidth = 4f,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };

            using var shadowPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(200),
                StrokeWidth = 6f,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };

            var cornerRadius = 20f;

            // Calculate text bounds - border should align exactly with text edges
            var titleLeftEdge = titleInfo.CenterX - (titleInfo.Width / 2f) - spacing;
            var titleRightEdge = titleInfo.CenterX + (titleInfo.Width / 2f) + spacing;
            var titleBottom = titleInfo.Y + titleInfo.Height;

            float episodeLeftEdge = 0;
            float episodeRightEdge = 0;
            float episodeTop = 0;

            if (episodeInfo.HasValue)
            {
                episodeLeftEdge = episodeInfo.Value.CenterX - (episodeInfo.Value.Width / 2f) - spacing;
                episodeRightEdge = episodeInfo.Value.CenterX + (episodeInfo.Value.Width / 2f) + spacing;
                episodeTop = episodeInfo.Value.Y;
            }

            // Draw the complete rounded rectangle border in segments
            using var path = new SKPath();

            // Top-left corner arc
            path.AddArc(
                new SKRect(safeArea.Left, safeArea.Top, safeArea.Left + cornerRadius * 2, safeArea.Top + cornerRadius * 2),
                180, 90);

            // Top horizontal line (left side) - stopping before title
            path.MoveTo(safeArea.Left + cornerRadius, safeArea.Top);
            path.LineTo(titleLeftEdge, safeArea.Top);

            // Top horizontal line (right side) - starting after title
            path.MoveTo(titleRightEdge, safeArea.Top);
            path.LineTo(safeArea.Right - cornerRadius, safeArea.Top);

            // Top-right corner arc
            path.AddArc(
                new SKRect(safeArea.Right - cornerRadius * 2, safeArea.Top, safeArea.Right, safeArea.Top + cornerRadius * 2),
                270, 90);

            // Right vertical line (top section) - from corner to title area
            path.MoveTo(safeArea.Right, safeArea.Top + cornerRadius);
            path.LineTo(safeArea.Right, titleBottom);

            if (episodeInfo.HasValue)
            {
                // Right vertical line (middle section) - between title and episode
                path.LineTo(safeArea.Right, episodeTop);

                // Right vertical line (bottom section) - from episode to corner
                path.MoveTo(safeArea.Right, episodeTop);
                path.LineTo(safeArea.Right, safeArea.Bottom - cornerRadius);

                // Bottom-right corner arc
                path.AddArc(
                    new SKRect(safeArea.Right - cornerRadius * 2, safeArea.Bottom - cornerRadius * 2, safeArea.Right, safeArea.Bottom),
                    0, 90);

                // Bottom horizontal line (right side) - stopping before episode
                path.MoveTo(safeArea.Right - cornerRadius, safeArea.Bottom);
                path.LineTo(episodeRightEdge, safeArea.Bottom);

                // Bottom horizontal line (left side) - starting after episode
                path.MoveTo(episodeLeftEdge, safeArea.Bottom);
                path.LineTo(safeArea.Left + cornerRadius, safeArea.Bottom);

                // Bottom-left corner arc
                path.AddArc(
                    new SKRect(safeArea.Left, safeArea.Bottom - cornerRadius * 2, safeArea.Left + cornerRadius * 2, safeArea.Bottom),
                    90, 90);

                // Left vertical line (bottom section) - from corner to episode
                path.MoveTo(safeArea.Left, safeArea.Bottom - cornerRadius);
                path.LineTo(safeArea.Left, episodeTop);

                // Left vertical line (middle section) - between episode and title
                path.MoveTo(safeArea.Left, episodeTop);
                path.LineTo(safeArea.Left, titleBottom);
            }
            else
            {
                // Right vertical line continues to bottom corner
                path.LineTo(safeArea.Right, safeArea.Bottom - cornerRadius);

                // Bottom-right corner arc
                path.AddArc(
                    new SKRect(safeArea.Right - cornerRadius * 2, safeArea.Bottom - cornerRadius * 2, safeArea.Right, safeArea.Bottom),
                    0, 90);

                // Bottom horizontal line (full width)
                path.MoveTo(safeArea.Right - cornerRadius, safeArea.Bottom);
                path.LineTo(safeArea.Left + cornerRadius, safeArea.Bottom);

                // Bottom-left corner arc
                path.AddArc(
                    new SKRect(safeArea.Left, safeArea.Bottom - cornerRadius * 2, safeArea.Left + cornerRadius * 2, safeArea.Bottom),
                    90, 90);

                // Left vertical line (from bottom corner to title)
                path.MoveTo(safeArea.Left, safeArea.Bottom - cornerRadius);
                path.LineTo(safeArea.Left, titleBottom);
            }

            // Left vertical line (top section) - from title to top corner
            path.LineTo(safeArea.Left, safeArea.Top + cornerRadius);

            // Draw shadow first (for depth)
            canvas.DrawPath(path, shadowPaint);

            // Draw border on top
            canvas.DrawPath(path, borderPaint);
        }
    }
}