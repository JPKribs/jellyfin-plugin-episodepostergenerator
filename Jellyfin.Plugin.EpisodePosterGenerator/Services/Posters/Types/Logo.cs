using System;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utilities;
using SkiaSharp;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters
{
    public class LogoPosterGenerator : BasePosterGenerator
    {
        // Style
        // The poster style this generator produces.
        public override PosterStyle Style => PosterStyle.Logo;

        // Description
        // A short, user facing description of this style shown in the configuration UI.
        public override string Description => "Series logo over the episode image. Puts branding first.";

        private const string EpisodeBlock = "episode";
        private const string TitleBlock = "title";

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
            var column = BuildColumn(episodeMetadata, settings, width, height);

            if (column.TryGetSlot(EpisodeBlock, out var codeSlot))
            {
                DrawEpisodeCode(skCanvas, episodeMetadata.SeasonNumber ?? 0, episodeMetadata.EpisodeNumberStart ?? 0, settings, height, codeSlot);
            }

            if (column.TryGetSlot(TitleBlock, out var titleSlot))
            {
                DrawEpisodeTitle(skCanvas, episodeMetadata.EpisodeName!, settings, height, titleSlot);
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
            var logoArea = GetLogoArea(episodeMetadata, config, width, height);

            // Logo image available
            if (!string.IsNullOrEmpty(logoPath))
                DrawSeriesLogoImage(canvas, logoPath, config.LogoPosition, config.LogoAlignment, config, logoArea, height);
            // Fall back to text
            else
                DrawSeriesLogoText(canvas, seriesName, config.LogoPosition, config.LogoAlignment, config, logoArea, height);
        }

        // BuildColumn
        // The single description of this style's vertical layout: episode code above a fixed two
        // line title zone, packed against the bottom of the safe area.
        //
        // Both the typography layer and the logo layer read this same column, so the logo can no
        // longer be placed from a second, separately maintained copy of the text's height — which
        // is what let a tall centred logo run straight through the episode code.
        private LayoutColumn BuildColumn(EpisodeMetadata episodeMetadata, PosterSettings config, int width, int height)
        {
            var safeArea = GetSafeAreaBounds(width, height, config);
            var column = new LayoutColumn(safeArea, GetElementSpacing(config, height), LayoutAnchor.Bottom);

            var episodeHeight = config.ShowEpisode
                ? FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize, height) * RenderConstants.LineHeightMultiplier
                : 0f;

            // The title zone is a fixed two lines so a wrapped or dropped title cannot move the
            // episode code between episodes of the same season.
            var titleHeight = config.ShowTitle && !string.IsNullOrEmpty(episodeMetadata.EpisodeName)
                ? FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height) * (1 + RenderConstants.LineHeightMultiplier)
                : 0f;

            return column
                .Add(EpisodeBlock, episodeHeight)
                .Add(TitleBlock, titleHeight);
        }

        // GetLogoArea
        // Whatever the text column leaves behind, so Center means "centred in the space actually
        // available" and the graphics layer cannot collide with the typography layer.
        private SKRect GetLogoArea(EpisodeMetadata episodeMetadata, PosterSettings config, int width, int height)
        {
            var remaining = BuildColumn(episodeMetadata, config, width, height).Remaining;
            var safeArea = GetSafeAreaBounds(width, height, config);

            // Never collapse the logo entirely, however much text is configured.
            var minHeight = safeArea.Height * 0.2f;
            return remaining.Height >= minHeight
                ? remaining
                : SKRect.Create(safeArea.Left, safeArea.Top, safeArea.Width, minHeight);
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking series logo path");
                return null;
            }
        }

        // DrawSeriesLogoImage
        // Draws the series logo image at the specified position and alignment.
        private void DrawSeriesLogoImage(SKCanvas canvas, string logoPath, Position position, Alignment alignment, PosterSettings config, SKRect logoArea, int height)
        {
            try
            {
                using var stream = File.OpenRead(logoPath);
                using var bitmap = SKBitmap.Decode(stream);
                if (bitmap == null) return;

                var logoHeight = height * (config.LogoHeight / 100f);
                var aspect = (float)bitmap.Width / bitmap.Height;
                var logoWidth = logoHeight * aspect;

                if (logoWidth > logoArea.Width)
                {
                    logoWidth = logoArea.Width;
                    logoHeight = logoWidth / aspect;
                }

                // A logo height large enough to reach the text strip is scaled down to fit the
                // space left for it rather than being allowed to overlap.
                if (logoHeight > logoArea.Height)
                {
                    logoHeight = logoArea.Height;
                    logoWidth = logoHeight * aspect;
                }

                var x = CalculateLogoX(alignment, logoArea, logoWidth);
                var y = CalculateLogoY(position, logoArea, logoHeight);
                var rect = new SKRect(x, y, x + logoWidth, y + logoHeight);

                using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
                canvas.DrawBitmap(bitmap, rect, paint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to draw series logo image: {Path}", logoPath);
            }
        }

        // DrawSeriesLogoText
        // Draws the series name as text when no logo image is available.
        private void DrawSeriesLogoText(SKCanvas canvas, string seriesName, Position position, Alignment alignment, PosterSettings config, SKRect logoArea, int height)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize * RenderConstants.LineHeightMultiplier, height);
            var color = ColorUtils.ParseHexColor(config.EpisodeFontColor ?? "#FFFFFF");
            var typeface = FontUtils.ResolveTypeface(config.EffectiveEpisodeFontPath, config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle));
            var textAlign = GetSKTextAlign(alignment);

            using var paint = PaintFactory.CreateTextPaint(color, fontSize, typeface, textAlign);
            using var shadowPaint = PaintFactory.CreateShadowTextPaint(fontSize, typeface, textAlign);

            var availableWidth = logoArea.Width * RenderConstants.TextWidthMultiplier;
            var lines = TextUtils.FitTextToWidth(seriesName, paint, availableWidth);

            var lineHeight = fontSize * RenderConstants.LineHeightMultiplier;
            var totalHeight = (lines.Count - 1) * lineHeight + fontSize;

            var x = CalculateLogoX(alignment, logoArea, 0);
            var y = CalculateLogoY(position, logoArea, totalHeight);

            for (int i = 0; i < lines.Count; i++)
            {
                var lineY = y + fontSize + (i * lineHeight);
                PaintFactory.DrawTextWithShadow(canvas, lines[i], x, lineY, paint, shadowPaint);
            }
        }

        // DrawEpisodeTitle
        // Draws the episode title inside the slot the column allotted it. The title is fitted to
        // that slot's height as well as its width, so it cannot spill past the space reserved.
        private void DrawEpisodeTitle(SKCanvas canvas, string title, PosterSettings config, int height, SKRect slot)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height);
            var typeface = FontUtils.ResolveTypeface(config.EffectiveTitleFontPath, config.TitleFontFamily, FontUtils.GetFontStyle(config.TitleFontStyle));
            var titleColor = ColorUtils.ParseHexColor(config.TitleFontColor);

            using var titlePaint = PaintFactory.CreateTextPaint(titleColor, fontSize, typeface);
            using var shadowPaint = PaintFactory.CreateShadowTextPaint(fontSize, typeface);

            var lineHeight = fontSize * RenderConstants.LineHeightMultiplier;
            var availableWidth = slot.Width * RenderConstants.TextWidthMultiplier;
            var lines = TextUtils.FitTitleLines(title, titlePaint, availableWidth, slot.Height, lineHeight, config.LongTitleHandling);
            if (lines.Count == 0)
                return;

            var startY = CenteredBaseline(slot, lines.Count, fontSize, lineHeight);

            for (int i = 0; i < lines.Count; i++)
            {
                PaintFactory.DrawTextWithShadow(canvas, lines[i], slot.MidX, startY + (i * lineHeight), titlePaint, shadowPaint);
            }
        }

        // DrawEpisodeCode
        // Draws the formatted episode code inside the slot the column allotted it.
        private void DrawEpisodeCode(SKCanvas canvas, int seasonNumber, int episodeNumber, PosterSettings config, int height, SKRect slot)
        {
            var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize, height);
            var color = ColorUtils.ParseHexColor(config.EpisodeFontColor ?? "#FFFFFF");
            var typeface = FontUtils.ResolveTypeface(config.EffectiveEpisodeFontPath, config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle));

            using var paint = PaintFactory.CreateTextPaint(color, fontSize, typeface);
            using var shadowPaint = PaintFactory.CreateShadowTextPaint(fontSize, typeface);

            var code = EpisodeCodeUtils.FormatEpisodeCode(seasonNumber, episodeNumber);

            PaintFactory.DrawTextWithShadow(canvas, code, slot.MidX, slot.Top + fontSize, paint, shadowPaint);
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
