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
    /// Uses 4-layer rendering: Canvas → Overlay → Graphics → Typography (frame lines + text)
    /// </summary>
    public class FramePosterGenerator : BasePosterGenerator
    {
        private readonly ILogger<FramePosterGenerator> _logger;

        public FramePosterGenerator(ILogger<FramePosterGenerator> logger)
        {
            _logger = logger;
        }

        // MARK: RenderTypography
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int width, int height)
        {
            if (string.IsNullOrEmpty(episodeMetadata.EpisodeName))
                return;

            var safeArea = GetSafeAreaBounds(width, height, config);
            float spacing = height * 0.02f;

            var titleInfo = DrawEpisodeTitle(skCanvas, episodeMetadata.EpisodeName, config, width, height, safeArea);

            TextInfo? episodeInfo = null;
            if (config.ShowEpisode && episodeMetadata.SeasonNumber.HasValue && episodeMetadata.EpisodeNumberStart.HasValue)
            {
                episodeInfo = DrawEpisodeInfo(skCanvas, episodeMetadata.SeasonNumber.Value, episodeMetadata.EpisodeNumberStart.Value, config, width, height, safeArea);
            }

            DrawFrameBorder(skCanvas, safeArea, titleInfo, episodeInfo, spacing);
        }

        // MARK: LogError
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate frame poster for {EpisodeName}", episodeName);
        }

        // MARK: DrawEpisodeTitle
        private TextInfo DrawEpisodeTitle(SKCanvas canvas, string title, PluginConfiguration config, int width, int height, SKRect safeArea)
        {
            title = title.ToUpperInvariant();
            
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height);
            var typeface = FontUtils.CreateTypeface(config.TitleFontFamily, FontUtils.GetFontStyle(config.TitleFontStyle));

            using var titlePaint = new SKPaint
            {
                Color = ColorUtils.ParseHexColor(config.TitleFontColor),
                TextSize = fontSize,
                IsAntialias = true,
                Typeface = typeface,
                TextAlign = SKTextAlign.Center
            };

            using var shadowPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(180),
                TextSize = fontSize,
                IsAntialias = true,
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
            var startY = safeArea.Top + Math.Abs(fontMetrics.Ascent);

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
                Y = safeArea.Top
            };
        }

        // MARK: DrawEpisodeInfo
        private TextInfo DrawEpisodeInfo(SKCanvas canvas, int seasonNumber, int episodeNumber, PluginConfiguration config, int width, int height, SKRect safeArea)
        {
            var episodeFontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize, height);
            var episodeColor = ColorUtils.ParseHexColor(config.EpisodeFontColor ?? "#FFFFFF");
            var shadowColor = SKColors.Black.WithAlpha(180);

            using var episodePaint = new SKPaint
            {
                Color = episodeColor,
                TextSize = episodeFontSize,
                IsAntialias = true,
                Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
                TextAlign = SKTextAlign.Center
            };

            using var shadowPaint = new SKPaint
            {
                Color = shadowColor,
                TextSize = episodeFontSize,
                IsAntialias = true,
                Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
                TextAlign = SKTextAlign.Center
            };

            var episodeText = EpisodeCodeUtil.FormatFullText(seasonNumber, episodeNumber, true, true);
            var centerX = safeArea.MidX;
            
            var fontMetrics = episodePaint.FontMetrics;
            var textActualHeight = Math.Abs(fontMetrics.Ascent) + Math.Abs(fontMetrics.Descent);
            var y = safeArea.Bottom - Math.Abs(fontMetrics.Descent);

            canvas.DrawText(episodeText, centerX + 2, y + 2, shadowPaint);
            canvas.DrawText(episodeText, centerX, y, episodePaint);

            var textWidth = episodePaint.MeasureText(episodeText);

            return new TextInfo
            {
                Height = textActualHeight,
                Width = textWidth,
                CenterX = centerX,
                Y = safeArea.Bottom - textActualHeight
            };
        }

        // MARK: DrawFrameBorder
        private void DrawFrameBorder(SKCanvas canvas, SKRect safeArea, TextInfo titleInfo, TextInfo? episodeInfo, float spacing)
        {
            using var borderPaint = new SKPaint
            {
                Color = SKColors.White,
                StrokeWidth = 3f,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            using var shadowPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(180),
                StrokeWidth = 5f,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            var topLineY = safeArea.Top + (titleInfo.Height / 2f);
            var topTextBottom = safeArea.Top + titleInfo.Height + spacing;
            
            var bottomLineY = episodeInfo.HasValue 
                ? safeArea.Bottom - (episodeInfo.Value.Height / 2f)
                : safeArea.Bottom;
            var bottomTextTop = episodeInfo.HasValue 
                ? safeArea.Bottom - episodeInfo.Value.Height - spacing 
                : safeArea.Bottom;

            canvas.DrawLine(safeArea.Left + 2, topLineY + 2, safeArea.Left + 2, topTextBottom + 2, shadowPaint);
            canvas.DrawLine(safeArea.Left, topLineY, safeArea.Left, topTextBottom, borderPaint);

            canvas.DrawLine(safeArea.Left + 2, topTextBottom + 2, safeArea.Left + 2, bottomTextTop + 2, shadowPaint);
            canvas.DrawLine(safeArea.Left, topTextBottom, safeArea.Left, bottomTextTop, borderPaint);

            if (episodeInfo.HasValue)
            {
                canvas.DrawLine(safeArea.Left + 2, bottomTextTop + 2, safeArea.Left + 2, bottomLineY + 2, shadowPaint);
                canvas.DrawLine(safeArea.Left, bottomTextTop, safeArea.Left, bottomLineY, borderPaint);
            }

            canvas.DrawLine(safeArea.Right + 2, topLineY + 2, safeArea.Right + 2, topTextBottom + 2, shadowPaint);
            canvas.DrawLine(safeArea.Right, topLineY, safeArea.Right, topTextBottom, borderPaint);

            canvas.DrawLine(safeArea.Right + 2, topTextBottom + 2, safeArea.Right + 2, bottomTextTop + 2, shadowPaint);
            canvas.DrawLine(safeArea.Right, topTextBottom, safeArea.Right, bottomTextTop, borderPaint);

            if (episodeInfo.HasValue)
            {
                canvas.DrawLine(safeArea.Right + 2, bottomTextTop + 2, safeArea.Right + 2, bottomLineY + 2, shadowPaint);
                canvas.DrawLine(safeArea.Right, bottomTextTop, safeArea.Right, bottomLineY, borderPaint);
            }

            var titleLeftEdge = titleInfo.CenterX - (titleInfo.Width / 2f) - spacing;
            var titleRightEdge = titleInfo.CenterX + (titleInfo.Width / 2f) + spacing;

            canvas.DrawLine(safeArea.Left + 2, topLineY + 2, titleLeftEdge + 2, topLineY + 2, shadowPaint);
            canvas.DrawLine(safeArea.Left, topLineY, titleLeftEdge, topLineY, borderPaint);

            canvas.DrawLine(titleRightEdge + 2, topLineY + 2, safeArea.Right + 2, topLineY + 2, shadowPaint);
            canvas.DrawLine(titleRightEdge, topLineY, safeArea.Right, topLineY, borderPaint);

            if (episodeInfo.HasValue)
            {
                var episodeLeftEdge = episodeInfo.Value.CenterX - (episodeInfo.Value.Width / 2f) - spacing;
                var episodeRightEdge = episodeInfo.Value.CenterX + (episodeInfo.Value.Width / 2f) + spacing;

                canvas.DrawLine(safeArea.Left + 2, bottomLineY + 2, episodeLeftEdge + 2, bottomLineY + 2, shadowPaint);
                canvas.DrawLine(safeArea.Left, bottomLineY, episodeLeftEdge, bottomLineY, borderPaint);

                canvas.DrawLine(episodeRightEdge + 2, bottomLineY + 2, safeArea.Right + 2, bottomLineY + 2, shadowPaint);
                canvas.DrawLine(episodeRightEdge, bottomLineY, safeArea.Right, bottomLineY, borderPaint);
            }
            else
            {
                canvas.DrawLine(safeArea.Left + 2, bottomLineY + 2, safeArea.Right + 2, bottomLineY + 2, shadowPaint);
                canvas.DrawLine(safeArea.Left, bottomLineY, safeArea.Right, bottomLineY, borderPaint);
            }
        }
    }
}