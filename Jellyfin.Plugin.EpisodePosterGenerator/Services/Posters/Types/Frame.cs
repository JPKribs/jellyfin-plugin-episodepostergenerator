using System;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using SkiaSharp;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    public class FramePosterGenerator : BasePosterGenerator
    {
        private readonly ILogger<FramePosterGenerator> _logger;

        // FramePosterGenerator
        // Initializes a new instance of the frame poster generator with logging support.
        public FramePosterGenerator(ILogger<FramePosterGenerator> logger)
        {
            _logger = logger;
        }

        // RenderTypography
        // Renders episode title, info, and decorative frame border on the poster.
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

        // LogError
        // Logs an error that occurred during frame poster generation.
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate frame poster for {EpisodeName}", episodeName);
        }

        // DrawEpisodeTitle
        // Draws the episode title at the top of the safe area and returns positioning info.
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

        // DrawEpisodeInfo
        // Draws the episode info at the bottom of the safe area and returns positioning info.
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

        // DrawFrameBorder
        // Draws a decorative rounded rectangle border with gaps for text elements.
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

            using var path = new SKPath();

            // Top-left corner arc
            path.AddArc(
                new SKRect(safeArea.Left, safeArea.Top, safeArea.Left + cornerRadius * 2, safeArea.Top + cornerRadius * 2),
                180, 90);

            // Top horizontal line (left side)
            path.MoveTo(safeArea.Left + cornerRadius, safeArea.Top);
            path.LineTo(titleLeftEdge, safeArea.Top);

            // Top horizontal line (right side)
            path.MoveTo(titleRightEdge, safeArea.Top);
            path.LineTo(safeArea.Right - cornerRadius, safeArea.Top);

            // Top-right corner arc
            path.AddArc(
                new SKRect(safeArea.Right - cornerRadius * 2, safeArea.Top, safeArea.Right, safeArea.Top + cornerRadius * 2),
                270, 90);

            // Right vertical line (top section)
            path.MoveTo(safeArea.Right, safeArea.Top + cornerRadius);
            path.LineTo(safeArea.Right, titleBottom);

            // Episode info present branch
            if (episodeInfo.HasValue)
            {
                path.LineTo(safeArea.Right, episodeTop);

                path.MoveTo(safeArea.Right, episodeTop);
                path.LineTo(safeArea.Right, safeArea.Bottom - cornerRadius);

                path.AddArc(
                    new SKRect(safeArea.Right - cornerRadius * 2, safeArea.Bottom - cornerRadius * 2, safeArea.Right, safeArea.Bottom),
                    0, 90);

                path.MoveTo(safeArea.Right - cornerRadius, safeArea.Bottom);
                path.LineTo(episodeRightEdge, safeArea.Bottom);

                path.MoveTo(episodeLeftEdge, safeArea.Bottom);
                path.LineTo(safeArea.Left + cornerRadius, safeArea.Bottom);

                path.AddArc(
                    new SKRect(safeArea.Left, safeArea.Bottom - cornerRadius * 2, safeArea.Left + cornerRadius * 2, safeArea.Bottom),
                    90, 90);

                path.MoveTo(safeArea.Left, safeArea.Bottom - cornerRadius);
                path.LineTo(safeArea.Left, episodeTop);

                path.MoveTo(safeArea.Left, episodeTop);
                path.LineTo(safeArea.Left, titleBottom);
            }
            // No episode info branch
            else
            {
                path.LineTo(safeArea.Right, safeArea.Bottom - cornerRadius);

                path.AddArc(
                    new SKRect(safeArea.Right - cornerRadius * 2, safeArea.Bottom - cornerRadius * 2, safeArea.Right, safeArea.Bottom),
                    0, 90);

                path.MoveTo(safeArea.Right - cornerRadius, safeArea.Bottom);
                path.LineTo(safeArea.Left + cornerRadius, safeArea.Bottom);

                path.AddArc(
                    new SKRect(safeArea.Left, safeArea.Bottom - cornerRadius * 2, safeArea.Left + cornerRadius * 2, safeArea.Bottom),
                    90, 90);

                path.MoveTo(safeArea.Left, safeArea.Bottom - cornerRadius);
                path.LineTo(safeArea.Left, titleBottom);
            }

            path.LineTo(safeArea.Left, safeArea.Top + cornerRadius);

            canvas.DrawPath(path, shadowPaint);

            canvas.DrawPath(path, borderPaint);
        }
    }
}
