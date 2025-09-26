using System;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using SkiaSharp;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    /// <summary>
    /// Generates logo-style episode posters with series logo and episode information.
    /// Uses 4-layer rendering: Canvas → Overlay → Graphics (series logo) → Typography (episode info)
    /// </summary>
    public class LogoPosterGenerator : BasePosterGenerator
    {
        private readonly ILogger<LogoPosterGenerator> _logger;

        public LogoPosterGenerator(ILogger<LogoPosterGenerator> logger)
        {
            _logger = logger;
        }

        // MARK: RenderGraphics
        protected override void RenderGraphics(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int width, int height)
        {
            // First try to render any configured graphic path (base implementation)
            base.RenderGraphics(skCanvas, episodeMetadata, config, width, height);

            // Then render the series logo
            RenderSeriesLogo(skCanvas, episodeMetadata, config, width, height);
        }

        // MARK: RenderTypography
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int width, int height)
        {
            var safeArea = GetSafeAreaBounds(width, height, config);
            float spacing = height * 0.02f;
            float currentY = safeArea.Bottom;

            // Draw episode title if enabled
            if (config.ShowTitle && !string.IsNullOrEmpty(episodeMetadata.EpisodeName))
            {
                var titleHeight = DrawEpisodeTitle(skCanvas, episodeMetadata.EpisodeName, config, width, height, currentY, safeArea);
                currentY -= titleHeight + spacing;
            }

            // Draw episode code if enabled
            if (config.ShowEpisode)
            {
                DrawEpisodeCode(skCanvas, episodeMetadata.SeasonNumber ?? 0, episodeMetadata.EpisodeNumberStart ?? 0, config, width, height, currentY);
            }
        }

        // MARK: LogError
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate logo poster for {EpisodeName}", episodeName);
        }

        // MARK: RenderSeriesLogo
        private void RenderSeriesLogo(SKCanvas canvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int width, int height)
        {
            var seriesName = episodeMetadata.SeriesName ?? "Unknown Series";
            var logoPath = GetSeriesLogoPath(episodeMetadata);

            if (!string.IsNullOrEmpty(logoPath))
                DrawSeriesLogoImage(canvas, logoPath, config.LogoPosition, config.LogoAlignment, config, width, height);
            else
                DrawSeriesLogoText(canvas, seriesName, config.LogoPosition, config.LogoAlignment, config, width, height);
        }

        // MARK: GetSeriesLogoPath
        private string? GetSeriesLogoPath(EpisodeMetadata episodeMetadata)
        {
            try
            {
                var path = episodeMetadata.VideoMetadata?.SeriesLogoFilePath;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return path;
                return null;
            }
            catch { return null; }
        }

        // MARK: DrawSeriesLogoImage
        private void DrawSeriesLogoImage(SKCanvas canvas, string logoPath, Position position, Alignment alignment, PluginConfiguration config, int width, int height)
        {
            try
            {
                using var stream = File.OpenRead(logoPath);
                using var bitmap = SKBitmap.Decode(stream);
                if (bitmap == null) return;

                var safeArea = GetSafeAreaBounds(width, height, config);

                var logoHeight = height * (config.LogoHeight / 100f);
                var aspect = (float)bitmap.Width / bitmap.Height;
                var logoWidth = logoHeight * aspect;

                if (logoWidth > safeArea.Width)
                {
                    logoWidth = safeArea.Width;
                    logoHeight = logoWidth / aspect;
                }

                var x = CalculateLogoX(alignment, safeArea, logoWidth);
                var y = CalculateLogoY(position, safeArea, logoHeight);
                var rect = new SKRect(x, y, x + logoWidth, y + logoHeight);

                using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
                canvas.DrawBitmap(bitmap, rect, paint);
            }
            catch { }
        }

        // MARK: DrawSeriesLogoText
        private void DrawSeriesLogoText(SKCanvas canvas, string seriesName, Position position, Alignment alignment, PluginConfiguration config, int width, int height)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize * 1.2f, height);
            var color = ColorUtils.ParseHexColor(config.EpisodeFontColor ?? "#FFFFFF");
            var shadowColor = SKColors.Black.WithAlpha(180);

            using var paint = new SKPaint
            {
                Color = color,
                TextSize = fontSize,
                IsAntialias = true,
                Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
                TextAlign = GetSKTextAlign(alignment)
            };

            using var shadowPaint = new SKPaint
            {
                Color = shadowColor,
                TextSize = fontSize,
                IsAntialias = true,
                Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
                TextAlign = GetSKTextAlign(alignment)
            };

            var safeArea = GetSafeAreaBounds(width, height, config);
            var availableWidth = safeArea.Width * 0.9f;
            var lines = TextUtils.FitTextToWidth(seriesName, paint, availableWidth);

            var lineHeight = fontSize * 1.2f;
            var totalHeight = (lines.Count - 1) * lineHeight + fontSize;

            var x = CalculateLogoX(alignment, safeArea, 0);
            var y = CalculateLogoY(position, safeArea, totalHeight);

            for (int i = 0; i < lines.Count; i++)
            {
                var lineY = y + fontSize + (i * lineHeight);
                canvas.DrawText(lines[i], x + 2, lineY + 2, shadowPaint);
                canvas.DrawText(lines[i], x, lineY, paint);
            }
        }

        // MARK: DrawEpisodeTitle
        private float DrawEpisodeTitle(SKCanvas canvas, string title, PluginConfiguration config, int width, int height, float bottomY, SKRect safeArea)
        {
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
            var startY = bottomY - totalHeight + fontSize;

            for (int i = 0; i < lines.Count; i++)
            {
                var lineY = startY + (i * lineHeight);
                canvas.DrawText(lines[i], centerX + 2, lineY + 2, shadowPaint);
                canvas.DrawText(lines[i], centerX, lineY, titlePaint);
            }

            return totalHeight;
        }

        // MARK: DrawEpisodeCode
        private void DrawEpisodeCode(SKCanvas canvas, int seasonNumber, int episodeNumber, PluginConfiguration config, int width, int height, float bottomY)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize, height);
            var color = ColorUtils.ParseHexColor(config.EpisodeFontColor ?? "#FFFFFF");
            var shadowColor = SKColors.Black.WithAlpha(180);

            using var paint = new SKPaint
            {
                Color = color,
                TextSize = fontSize,
                IsAntialias = true,
                Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
                TextAlign = SKTextAlign.Center
            };

            using var shadowPaint = new SKPaint
            {
                Color = shadowColor,
                TextSize = fontSize,
                IsAntialias = true,
                Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
                TextAlign = SKTextAlign.Center
            };

            var safeArea = GetSafeAreaBounds(width, height, config);
            var code = EpisodeCodeUtil.FormatEpisodeCode(seasonNumber, episodeNumber);
            var centerX = safeArea.MidX;
            
            canvas.DrawText(code, centerX + 2, bottomY + 2, shadowPaint);
            canvas.DrawText(code, centerX, bottomY, paint);
        }

        // MARK: CalculateLogoX
        private float CalculateLogoX(Alignment alignment, SKRect safeArea, float logoWidth) => alignment switch
        {
            Alignment.Left => safeArea.Left,
            Alignment.Center => safeArea.Left + (safeArea.Width - logoWidth) / 2f,
            Alignment.Right => safeArea.Right - logoWidth,
            _ => safeArea.Left + (safeArea.Width - logoWidth) / 2f
        };

        // MARK: CalculateLogoY
        private float CalculateLogoY(Position position, SKRect safeArea, float logoHeight) => position switch
        {
            Position.Top => safeArea.Top,
            Position.Center => safeArea.Top + (safeArea.Height - logoHeight) / 2f,
            Position.Bottom => safeArea.Bottom - logoHeight,
            _ => safeArea.Top + (safeArea.Height - logoHeight) / 2f
        };

        // MARK: GetSKTextAlign
        private SKTextAlign GetSKTextAlign(Alignment alignment) => alignment switch
        {
            Alignment.Left => SKTextAlign.Left,
            Alignment.Center => SKTextAlign.Center,
            Alignment.Right => SKTextAlign.Right,
            _ => SKTextAlign.Center
        };
    }
}