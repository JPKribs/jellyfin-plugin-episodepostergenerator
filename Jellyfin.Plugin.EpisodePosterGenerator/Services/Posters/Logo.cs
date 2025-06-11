using System;
using System.Globalization;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using MediaBrowser.Controller.Entities.TV;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

/// <summary>
/// Generates logo-style episode posters with series logo, episode code, and optional title.
/// Creates posters with solid background color and bottom-aligned text elements stacked vertically.
/// Text elements are stacked from bottom to top: series logo, episode code (S##E##), and episode title.
/// Uses inherited safe area calculations for consistent margins across all poster generators.
/// </summary>
public class LogoPosterGenerator : BasePosterGenerator, IPosterGenerator
{
    /// <summary>
    /// Generates a logo-style poster with series logo, episode code, and optional title.
    /// Creates a solid color background with bottom-aligned text elements similar to standard style.
    /// Uses S##E## formatting for episode codes with appropriate zero-padding.
    /// </summary>
    /// <param name="inputImagePath">Path to the source image (ignored - uses solid color background).</param>
    /// <param name="outputPath">Path where the generated poster will be saved.</param>
    /// <param name="episode">Episode metadata containing season/episode numbers, title, and series information.</param>
    /// <param name="config">Plugin configuration with styling, font, and color settings.</param>
    /// <returns>Path to the generated poster file, or null if generation fails.</returns>
    // MARK: Generate
    public string? Generate(string inputImagePath, string outputPath, Episode episode, PluginConfiguration config)
    {
        try
        {
            var seasonNumber = episode.ParentIndexNumber ?? 0;
            var episodeNumber = episode.IndexNumber ?? 0;
            var episodeTitle = episode.Name ?? "Unknown Episode";
            var seriesName = episode.Series?.Name ?? "Unknown Series";

            // Create a standard canvas size (can be configured later if needed)
            const int width = 1920;
            const int height = 1080;
            var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            // Clear canvas and draw solid background color
            canvas.Clear(SKColors.Transparent);

            // Draw solid background color from config (like numeral style)
            var backgroundColor = ColorUtils.ParseHexColor(config.BackgroundColor);
            using var backgroundPaint = new SKPaint
            {
                Color = backgroundColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(SKRect.Create(width, height), backgroundPaint);

            // Draw text layers from bottom to top
            DrawTextLayers(canvas, episode, seriesName, seasonNumber, episodeNumber, episodeTitle, config, width, height);

            // Encode and save the final image
            using var finalImage = surface.Snapshot();
            using var data = finalImage.Encode(SKEncodedImageFormat.Jpeg, 95);
            using var outputStream = File.OpenWrite(outputPath);
            data.SaveTo(outputStream);

            return outputPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Logo poster generation failed: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Draws all text layers with bottom-aligned, stacked text elements.
    /// Elements are rendered from bottom to top: series logo, episode code, episode title.
    /// Maintains 2% canvas height spacing between elements to prevent overlapping.
    /// Uses inherited safe area calculations for consistent margins.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="episode">Episode metadata for accessing series information.</param>
    /// <param name="seriesName">Name of the series for logo text.</param>
    /// <param name="seasonNumber">Season number for episode code formatting.</param>
    /// <param name="episodeNumber">Episode number for episode code formatting.</param>
    /// <param name="episodeTitle">Episode title for optional display.</param>
    /// <param name="config">Plugin configuration with text styling settings.</param>
    /// <param name="canvasWidth">Canvas width in pixels.</param>
    /// <param name="canvasHeight">Canvas height in pixels.</param>
    // MARK: DrawTextLayers
    private void DrawTextLayers(SKCanvas canvas, Episode episode, string seriesName, int seasonNumber, int episodeNumber, string episodeTitle, PluginConfiguration config, int canvasWidth, int canvasHeight)
    {
        // 2% of canvas height for spacing between elements
        var spacingHeight = canvasHeight * 0.02f;

        // Use inherited safe area calculation from BasePosterGenerator
        ApplySafeAreaConstraints(canvasWidth, canvasHeight, config, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);

        // Start from bottom and work upward using inherited SafeAreaMargin
        var currentY = canvasHeight - (canvasHeight * GetSafeAreaMargin(config));

        // Draw episode title (bottom element) if enabled
        if (config.ShowTitle)
        {
            var titleHeight = DrawEpisodeTitle(canvas, episodeTitle, config, canvasWidth, canvasHeight, currentY);
            currentY -= titleHeight + spacingHeight;
        }

        // Draw episode code (middle element)
        var episodeCodeHeight = DrawEpisodeCode(canvas, seasonNumber, episodeNumber, config, canvasWidth, canvasHeight, currentY);
        currentY -= episodeCodeHeight + spacingHeight;

        // Draw series logo/name (top element)
        DrawSeriesLogo(canvas, episode, seriesName, config, canvasWidth, canvasHeight, currentY);
    }

    /// <summary>
    /// Draws the episode title text with automatic text wrapping and shadow effects.
    /// Text is centered horizontally and positioned at the specified bottom Y coordinate.
    /// Supports up to two lines with ellipsis truncation if text is too long.
    /// Uses inherited safe area margins for consistent text positioning.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="title">Episode title text to render.</param>
    /// <param name="config">Plugin configuration with title font settings.</param>
    /// <param name="canvasWidth">Canvas width for centering calculations.</param>
    /// <param name="canvasHeight">Canvas height for font size calculations.</param>
    /// <param name="bottomY">Y coordinate for the bottom of the text block.</param>
    /// <returns>Total height of the rendered text block.</returns>
    // MARK: DrawEpisodeTitle
    private float DrawEpisodeTitle(SKCanvas canvas, string title, PluginConfiguration config, int canvasWidth, int canvasHeight, float bottomY)
    {
        var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, canvasHeight, (100f * GetSafeAreaMargin(config)));
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

        // Calculate available width within inherited safe area margins
        var safeWidth = canvasWidth * (1 - 2 * GetSafeAreaMargin(config)) * 0.9f;
        var lines = TextUtils.FitTextToWidth(title, titlePaint, safeWidth);

        var lineHeight = fontSize * 1.2f;
        var totalHeight = (lines.Count - 1) * lineHeight + fontSize;

        var centerX = canvasWidth / 2f;
        var startY = bottomY - totalHeight + fontSize;

        // Draw each line with shadow offset
        for (int i = 0; i < lines.Count; i++)
        {
            var lineY = startY + (i * lineHeight);
            canvas.DrawText(lines[i], centerX + 2, lineY + 2, shadowPaint);
            canvas.DrawText(lines[i], centerX, lineY, titlePaint);
        }

        return totalHeight;
    }

    /// <summary>
    /// Draws the episode code in S##E## format with proper zero-padding.
    /// Uses S01E12 format for standard episodes, but expands for 3+ digit numbers (S01E100).
    /// Text is centered horizontally with shadow effects for better visibility.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="seasonNumber">Season number for formatting.</param>
    /// <param name="episodeNumber">Episode number for formatting.</param>
    /// <param name="config">Plugin configuration with episode font settings.</param>
    /// <param name="canvasWidth">Canvas width for centering calculations.</param>
    /// <param name="canvasHeight">Canvas height for font size calculations.</param>
    /// <param name="bottomY">Y coordinate for the bottom of the text.</param>
    /// <returns>Height of the rendered episode code text.</returns>
    // MARK: DrawEpisodeCode
    private float DrawEpisodeCode(SKCanvas canvas, int seasonNumber, int episodeNumber, PluginConfiguration config, int canvasWidth, int canvasHeight, float bottomY)
    {
        var episodeFontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize, canvasHeight);
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

        // Format episode code with appropriate zero-padding
        var episodeCode = EpisodeCodeUtil.FormatEpisodeCode(seasonNumber, episodeNumber);

        var centerX = canvasWidth / 2f;
        var textY = bottomY;

        // Draw episode code with shadow offset
        canvas.DrawText(episodeCode, centerX + 2, textY + 2, shadowPaint);
        canvas.DrawText(episodeCode, centerX, textY, episodePaint);

        return episodeFontSize;
    }

    /// <summary>
    /// Draws the series logo image or name text at the top of the text stack.
    /// First attempts to load and draw the actual series logo image at appropriate size.
    /// Falls back to rendering series name as text with same styling as episode text if no logo found.
    /// Logo images are scaled proportionally to fit within safe area constraints.
    /// Text fallback uses TextUtils for consistent wrapping and truncation behavior.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="episode">Episode metadata for accessing parent series information.</param>
    /// <param name="seriesName">Name of the series for text fallback display.</param>
    /// <param name="config">Plugin configuration with font settings for text fallback.</param>
    /// <param name="canvasWidth">Canvas width for centering and scaling calculations.</param>
    /// <param name="canvasHeight">Canvas height for scaling and font size calculations.</param>
    /// <param name="bottomY">Y coordinate for the bottom of the logo or text.</param>
    /// <returns>Total height of the rendered logo image or text block.</returns>
    // MARK: DrawSeriesLogo
    private float DrawSeriesLogo(SKCanvas canvas, Episode episode, string seriesName, PluginConfiguration config, int canvasWidth, int canvasHeight, float bottomY)
    {
        // Attempt to get series logo image first
        var logoPath = GetSeriesLogoPath(episode);

        if (!string.IsNullOrEmpty(logoPath))
        {
            // Draw logo image if found
            return DrawSeriesLogoImage(canvas, logoPath, config, canvasWidth, canvasHeight, bottomY);
        }
        else
        {
            // Fallback to text rendering using TextUtils
            return DrawSeriesLogoText(canvas, seriesName, config, canvasWidth, canvasHeight, bottomY);
        }
    }

    /// <summary>
    /// Retrieves the file path to the series logo image from the parent series.
    /// First attempts to find a dedicated logo image, then falls back to the primary series image.
    /// Returns null if no suitable image is found or if the series information is unavailable.
    /// Used to display actual series artwork instead of text when available.
    /// </summary>
    /// <param name="episode">Episode metadata containing reference to parent series information.</param>
    /// <returns>File path to the series logo/primary image, or null if no image is found or accessible.</returns>
    // MARK: GetSeriesLogoPath
    private string? GetSeriesLogoPath(Episode episode)
    {
        try
        {
            // Get parent series reference from episode metadata
            var series = episode.Series;
            if (series == null)
                return null;

            // First priority: Check for dedicated logo image type
            var logoPath = series.GetImagePath(MediaBrowser.Model.Entities.ImageType.Logo, 0);
            if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
            {
                return logoPath;
            }

            // Second priority: Fallback to primary series image if no logo exists
            var primaryPath = series.GetImagePath(MediaBrowser.Model.Entities.ImageType.Primary, 0);
            if (!string.IsNullOrEmpty(primaryPath) && File.Exists(primaryPath))
            {
                return primaryPath;
            }

            // No suitable image found
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to retrieve series logo from episode metadata: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Draws the series logo image scaled to use 75% of remaining space after episode elements.
    /// Calculates space used by episode code and title, then scales logo to fill 75% of leftover space.
    /// Logo is centered horizontally and positioned to maximize visual impact while respecting safe areas.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="logoPath">File path to the series logo image.</param>
    /// <param name="config">Plugin configuration for safe area margin calculations.</param>
    /// <param name="canvasWidth">Canvas width for centering calculations.</param>
    /// <param name="canvasHeight">Canvas height for scaling calculations.</param>
    /// <param name="bottomY">Y coordinate for the bottom of the logo image.</param>
    /// <returns>Total height of the rendered logo image.</returns>
    // MARK: DrawSeriesLogoImage
    private float DrawSeriesLogoImage(SKCanvas canvas, string logoPath, PluginConfiguration config, int canvasWidth, int canvasHeight, float bottomY)
    {
        try
        {
            using var logoStream = File.OpenRead(logoPath);
            using var logoBitmap = SKBitmap.Decode(logoStream);

            if (logoBitmap == null)
                return 0f;

            // Calculate safe area constraints
            var safeAreaMargin = GetSafeAreaMargin(config);
            var safeWidth = canvasWidth * (1 - 2 * safeAreaMargin);
            var topSafeAreaBoundary = canvasHeight * safeAreaMargin;
            var bottomSafeAreaBoundary = canvasHeight * safeAreaMargin;

            // Calculate space used by episode elements
            var episodeCodeHeight = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize, canvasHeight);
            var spacingHeight = canvasHeight * 0.02f; // 2% spacing
            
            var usedSpaceByEpisodeElements = episodeCodeHeight + spacingHeight;
            
            // Add title space if enabled
            if (config.ShowTitle)
            {
                var titleFontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, canvasHeight, (100f * safeAreaMargin));
                var titleLineHeight = titleFontSize * 1.2f;
                var titleHeight = titleLineHeight * 2; // Max 2 lines
                usedSpaceByEpisodeElements += titleHeight + spacingHeight;
            }

            // Calculate total remaining space
            var totalUsableSpace = canvasHeight - topSafeAreaBoundary - bottomSafeAreaBoundary;
            var remainingSpace = totalUsableSpace - usedSpaceByEpisodeElements;
            
            // Use 75% of remaining space for logo
            var logoAllowedHeight = remainingSpace * 0.75f;
            var maxLogoWidth = safeWidth * 0.9f; // 90% of safe width

            // Calculate scaled dimensions maintaining aspect ratio
            var logoAspect = (float)logoBitmap.Width / logoBitmap.Height;
            
            // Try scaling by allowed height first
            var scaledHeight = logoAllowedHeight;
            var scaledWidth = scaledHeight * logoAspect;
            
            // If too wide, scale by available width instead
            if (scaledWidth > maxLogoWidth)
            {
                scaledWidth = maxLogoWidth;
                scaledHeight = scaledWidth / logoAspect;
            }
            
            // Ensure we don't exceed the allowed height
            if (scaledHeight > logoAllowedHeight)
            {
                scaledHeight = logoAllowedHeight;
                scaledWidth = scaledHeight * logoAspect;
            }

            // Center horizontally and position at bottomY
            var logoX = (canvasWidth - scaledWidth) / 2f;
            var logoY = bottomY - scaledHeight;

            var destRect = new SKRect(logoX, logoY, logoX + scaledWidth, logoY + scaledHeight);

            using var paint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };

            canvas.DrawBitmap(logoBitmap, destRect, paint);

            return scaledHeight;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to draw series logo image: {ex.Message}");
            return 0f;
        }
    }

    /// <summary>
    /// Draws the series name as text when no logo image is available.
    /// Uses TextUtils for consistent text wrapping and truncation behavior.
    /// Text is rendered slightly larger than episode text and includes shadow effects.
    /// Supports up to two lines with ellipsis truncation if series name is too long.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="seriesName">Name of the series to display as text.</param>
    /// <param name="config">Plugin configuration with font settings.</param>
    /// <param name="canvasWidth">Canvas width for centering calculations.</param>
    /// <param name="canvasHeight">Canvas height for font size calculations.</param>
    /// <param name="bottomY">Y coordinate for the bottom of the text block.</param>
    /// <returns>Total height of the rendered text block.</returns>
    // MARK: DrawSeriesLogoText
    private float DrawSeriesLogoText(SKCanvas canvas, string seriesName, PluginConfiguration config, int canvasWidth, int canvasHeight, float bottomY)
    {
        // Use episode font settings for series logo text (20% larger than episode font)
        var logoFontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize * 1.2f, canvasHeight);
        var logoColor = ColorUtils.ParseHexColor(config.EpisodeFontColor ?? "#FFFFFF");
        var shadowColor = SKColors.Black.WithAlpha(180);

        using var logoPaint = new SKPaint
        {
            Color = logoColor,
            TextSize = logoFontSize,
            IsAntialias = true,
            Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
            TextAlign = SKTextAlign.Center
        };

        using var shadowPaint = new SKPaint
        {
            Color = shadowColor,
            TextSize = logoFontSize,
            IsAntialias = true,
            Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
            TextAlign = SKTextAlign.Center
        };

        // Calculate available width within inherited safe area margins
        var safeWidth = canvasWidth * (1 - 2 * GetSafeAreaMargin(config)) * 0.9f;
        var lines = TextUtils.FitTextToWidth(seriesName, logoPaint, safeWidth);

        var lineHeight = logoFontSize * 1.2f;
        var totalHeight = (lines.Count - 1) * lineHeight + logoFontSize;

        var centerX = canvasWidth / 2f;
        var startY = bottomY - totalHeight + logoFontSize;

        // Draw each line with shadow offset
        for (int i = 0; i < lines.Count; i++)
        {
            var lineY = startY + (i * lineHeight);
            canvas.DrawText(lines[i], centerX + 2, lineY + 2, shadowPaint);
            canvas.DrawText(lines[i], centerX, lineY, logoPaint);
        }

        return totalHeight;
    }
}