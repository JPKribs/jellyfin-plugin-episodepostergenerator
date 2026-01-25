using System;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using SkiaSharp;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    public class LogoPosterGenerator : BasePosterGenerator
    {
        private readonly ILogger<LogoPosterGenerator> _logger;

        // LogoPosterGenerator
        // Initializes a new instance of the logo poster generator with logging support.
        public LogoPosterGenerator(ILogger<LogoPosterGenerator> logger)
        {
            _logger = logger;
        }

        // RenderGraphics
        // Renders configured graphics and the series logo on the poster.
        protected override void RenderGraphics(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            base.RenderGraphics(skCanvas, episodeMetadata, settings, width, height);

            RenderSeriesLogo(skCanvas, episodeMetadata, settings, width, height);
        }

        // RenderTypography
        // Renders episode title and code text on the poster.
        protected override void RenderTypography(SKCanvas skCanvas, EpisodeMetadata episodeMetadata, PosterSettings settings, int width, int height)
        {
            var safeArea = GetSafeAreaBounds(width, height, settings);
            float spacing = height * 0.02f;
            float currentY = safeArea.Bottom;

            if (settings.ShowTitle && !string.IsNullOrEmpty(episodeMetadata.EpisodeName))
            {
                var titleHeight = DrawEpisodeTitle(skCanvas, episodeMetadata.EpisodeName, settings, width, height, currentY, safeArea);
                currentY -= titleHeight + spacing;
            }

            if (settings.ShowEpisode)
            {
                DrawEpisodeCode(skCanvas, episodeMetadata.SeasonNumber ?? 0, episodeMetadata.EpisodeNumberStart ?? 0, settings, width, height, currentY);
            }
        }

        // LogError
        // Logs an error that occurred during logo poster generation.
        protected override void LogError(Exception ex, string? episodeName)
        {
            _logger.LogError(ex, "Failed to generate logo poster for {EpisodeName}", episodeName);
        }

        // RenderSeriesLogo
        // Renders the series logo image or falls back to text if no logo is available.
        private void RenderSeriesLogo(SKCanvas canvas, EpisodeMetadata episodeMetadata, PosterSettings config, int width, int height)
        {
            var seriesName = episodeMetadata.SeriesName ?? "Unknown Series";
            var logoPath = GetSeriesLogoPath(episodeMetadata);

            // Logo image available
            if (!string.IsNullOrEmpty(logoPath))
                DrawSeriesLogoImage(canvas, logoPath, config.LogoPosition, config.LogoAlignment, config, width, height);
            // Fall back to text
            else
                DrawSeriesLogoText(canvas, seriesName, config.LogoPosition, config.LogoAlignment, config, width, height);
        }

        // GetSeriesLogoPath
        // Returns the path to the series logo file if it exists.
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

        // DrawSeriesLogoImage
        // Draws the series logo image at the specified position and alignment.
        private void DrawSeriesLogoImage(SKCanvas canvas, string logoPath, Position position, Alignment alignment, PosterSettings config, int width, int height)
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

        // DrawSeriesLogoText
        // Draws the series name as text when no logo image is available.
        private void DrawSeriesLogoText(SKCanvas canvas, string seriesName, Position position, Alignment alignment, PosterSettings config, int width, int height)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize * 1.2f, height);
            var color = ColorUtils.ParseHexColor(config.EpisodeFontColor ?? "#FFFFFF");
            var shadowColor = SKColors.Black.WithAlpha(180);

            using var paint = new SKPaint
            {
                Color = color,
                TextSize = fontSize,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
                TextAlign = GetSKTextAlign(alignment)
            };

            using var shadowPaint = new SKPaint
            {
                Color = shadowColor,
                TextSize = fontSize,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
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

        // DrawEpisodeTitle
        // Draws the episode title with shadow effect and returns the total height used.
        private float DrawEpisodeTitle(SKCanvas canvas, string title, PosterSettings config, int width, int height, float bottomY, SKRect safeArea)
        {
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
            var startY = bottomY - totalHeight + fontSize;

            for (int i = 0; i < lines.Count; i++)
            {
                var lineY = startY + (i * lineHeight);
                canvas.DrawText(lines[i], centerX + 2, lineY + 2, shadowPaint);
                canvas.DrawText(lines[i], centerX, lineY, titlePaint);
            }

            return totalHeight;
        }

        // DrawEpisodeCode
        // Draws the formatted episode code with shadow effect.
        private void DrawEpisodeCode(SKCanvas canvas, int seasonNumber, int episodeNumber, PosterSettings config, int width, int height, float bottomY)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize, height);
            var color = ColorUtils.ParseHexColor(config.EpisodeFontColor ?? "#FFFFFF");
            var shadowColor = SKColors.Black.WithAlpha(180);

            using var paint = new SKPaint
            {
                Color = color,
                TextSize = fontSize,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
                TextAlign = SKTextAlign.Center
            };

            using var shadowPaint = new SKPaint
            {
                Color = shadowColor,
                TextSize = fontSize,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = true,
                Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
                TextAlign = SKTextAlign.Center
            };

            var safeArea = GetSafeAreaBounds(width, height, config);
            var code = EpisodeCodeUtil.FormatEpisodeCode(seasonNumber, episodeNumber);
            var centerX = safeArea.MidX;

            canvas.DrawText(code, centerX + 2, bottomY + 2, shadowPaint);
            canvas.DrawText(code, centerX, bottomY, paint);
        }

        // CalculateLogoX
        // Calculates the horizontal position for the logo based on alignment.
        private float CalculateLogoX(Alignment alignment, SKRect safeArea, float logoWidth) => alignment switch
        {
            Alignment.Left => safeArea.Left,
            Alignment.Center => safeArea.Left + (safeArea.Width - logoWidth) / 2f,
            Alignment.Right => safeArea.Right - logoWidth,
            _ => safeArea.Left + (safeArea.Width - logoWidth) / 2f
        };

        // CalculateLogoY
        // Calculates the vertical position for the logo based on position.
        private float CalculateLogoY(Position position, SKRect safeArea, float logoHeight) => position switch
        {
            Position.Top => safeArea.Top,
            Position.Center => safeArea.Top + (safeArea.Height - logoHeight) / 2f,
            Position.Bottom => safeArea.Bottom - logoHeight,
            _ => safeArea.Top + (safeArea.Height - logoHeight) / 2f
        };

        // GetSKTextAlign
        // Converts an Alignment enum value to the corresponding SKTextAlign.
        private SKTextAlign GetSKTextAlign(Alignment alignment) => alignment switch
        {
            Alignment.Left => SKTextAlign.Left,
            Alignment.Center => SKTextAlign.Center,
            Alignment.Right => SKTextAlign.Right,
            _ => SKTextAlign.Center
        };
    }
}
