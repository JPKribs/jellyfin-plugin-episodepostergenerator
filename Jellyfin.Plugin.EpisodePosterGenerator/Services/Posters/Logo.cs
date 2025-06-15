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
/// Creates posters with layered rendering: input image background, optional overlay, positioned logo, and bottom-aligned text.
/// Logo positioning is configurable using Position and Alignment enums with canvas-relative safe area calculations.
/// Text elements are stacked from bottom to top: series logo text fallback, episode code (S##E##), and episode title.
/// Uses inherited safe area calculations for consistent margins across all poster generators.
/// </summary>
public class LogoPosterGenerator : BasePosterGenerator, IPosterGenerator
{
    /// <summary>
    /// Generates a logo-style poster with layered composition and configurable logo positioning.
    /// Layer order: input image background, overlay, positioned logo, text elements.
    /// Logo is positioned using full canvas dimensions with safe area constraints and scaled by LogoHeight percentage.
    /// Text elements maintain bottom-aligned stacking for consistent layout with other poster styles.
    /// </summary>
    /// <param name="inputImagePath">Path to the source image file to use as background layer.</param>
    /// <param name="outputPath">Path where the generated poster will be saved.</param>
    /// <param name="episode">Episode metadata containing season/episode numbers, title, and series information.</param>
    /// <param name="config">Plugin configuration with styling, positioning, and font settings.</param>
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

            // Load and decode the input image for background layer
            using var inputStream = File.OpenRead(inputImagePath);
            using var inputBitmap = SKBitmap.Decode(inputStream);
            if (inputBitmap == null)
                return null;

            var width = inputBitmap.Width;
            var height = inputBitmap.Height;
            var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            // Layer 1: Draw input image as background
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(inputBitmap, 0, 0);

            // Layer 2: Draw overlay if configured
            DrawOverlayLayer(canvas, width, height, config);

            // Layer 3: Draw series logo with configurable positioning
            DrawSeriesLogoLayer(canvas, episode, seriesName, config, width, height);

            // Layer 4: Draw text layers from bottom to top
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
    /// Draws the overlay layer on top of the background image for enhanced text visibility.
    /// Applies background color overlay only if configured and not transparent.
    /// Uses the same overlay logic as other poster generators for consistent behavior.
    /// </summary>
    /// <param name="canvas">Canvas to draw the overlay on.</param>
    /// <param name="width">Canvas width in pixels.</param>
    /// <param name="height">Canvas height in pixels.</param>
    /// <param name="config">Plugin configuration containing background color settings.</param>
    // MARK: DrawOverlayLayer
    private void DrawOverlayLayer(SKCanvas canvas, int width, int height, PluginConfiguration config)
    {
        // Apply background color overlay for consistent text visibility
        var backgroundColor = ColorUtils.ParseHexColor(config.OverlayColor);
        
        // Only draw overlay if it has opacity (not fully transparent)
        if (backgroundColor.Alpha > 0)
        {
            using var backgroundPaint = new SKPaint
            {
                Color = backgroundColor,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(SKRect.Create(width, height), backgroundPaint);
        }
    }

    /// <summary>
    /// Draws the series logo layer with configurable positioning and scaling.
    /// Logo is positioned using full canvas dimensions with safe area constraints.
    /// Scaling is controlled by LogoHeight percentage while maintaining aspect ratio.
    /// Falls back to text rendering if no logo image is available.
    /// </summary>
    /// <param name="canvas">Canvas to draw the logo on.</param>
    /// <param name="episode">Episode metadata for accessing parent series information.</param>
    /// <param name="seriesName">Name of the series for text fallback display.</param>
    /// <param name="config">Plugin configuration with logo positioning and sizing settings.</param>
    /// <param name="canvasWidth">Canvas width for positioning calculations.</param>
    /// <param name="canvasHeight">Canvas height for positioning and scaling calculations.</param>
    // MARK: DrawSeriesLogoLayer
    private void DrawSeriesLogoLayer(SKCanvas canvas, Episode episode, string seriesName, PluginConfiguration config, int canvasWidth, int canvasHeight)
    {
        // Attempt to get series logo image first
        var logoPath = GetSeriesLogoPath(episode);

        if (!string.IsNullOrEmpty(logoPath))
        {
            // Draw logo image with configurable positioning
            DrawSeriesLogoImage(canvas, logoPath, config.LogoPosition, config.LogoAlignment, config, canvasWidth, canvasHeight);
        }
        else
        {
            // Fallback to text rendering with same positioning
            DrawSeriesLogoText(canvas, seriesName, config.LogoPosition, config.LogoAlignment, config, canvasWidth, canvasHeight);
        }
    }

    /// <summary>
    /// Draws all text layers with bottom-aligned, stacked text elements.
    /// Elements are rendered from bottom to top: episode code (if enabled), episode title (if enabled).
    /// Maintains 2% canvas height spacing between elements to prevent overlapping.
    /// Uses inherited safe area calculations for consistent margins.
    /// Both episode code and title display are controlled by their respective configuration flags.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="episode">Episode metadata for accessing series information.</param>
    /// <param name="seriesName">Name of the series for reference.</param>
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

        // Draw episode code (top element of text layer) if enabled
        if (config.ShowEpisode)
        {
            DrawEpisodeCode(canvas, seasonNumber, episodeNumber, config, canvasWidth, canvasHeight, currentY);
        }
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
    /// Draws the series logo image with configurable positioning and scaling within safe area constraints.
    /// Logo is positioned using canvas-relative coordinates with Position and Alignment enums.
    /// Scaling is controlled by LogoHeight percentage while maintaining original aspect ratio.
    /// Safe area margins are applied to prevent logo from being positioned too close to canvas edges.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="logoPath">File path to the series logo image.</param>
    /// <param name="position">Vertical position (Top, Center, Bottom) for logo placement.</param>
    /// <param name="alignment">Horizontal alignment (Left, Center, Right) for logo placement.</param>
    /// <param name="config">Plugin configuration for safe area margin and LogoHeight percentage calculations.</param>
    /// <param name="canvasWidth">Canvas width for positioning calculations.</param>
    /// <param name="canvasHeight">Canvas height for positioning and scaling calculations.</param>
    /// <returns>Total height of the rendered logo image, or 0 if rendering failed.</returns>
    // MARK: DrawSeriesLogoImage
    private float DrawSeriesLogoImage(SKCanvas canvas, string logoPath, Position position, Alignment alignment, PluginConfiguration config, int canvasWidth, int canvasHeight)
    {
        try
        {
            using var logoStream = File.OpenRead(logoPath);
            using var logoBitmap = SKBitmap.Decode(logoStream);

            if (logoBitmap == null)
                return 0f;

            // Calculate safe area constraints using inherited method
            ApplySafeAreaConstraints(canvasWidth, canvasHeight, config, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);

            // Calculate logo dimensions based on LogoHeight percentage
            var logoHeight = canvasHeight * (config.LogoHeight / 100f);
            var logoAspect = (float)logoBitmap.Width / logoBitmap.Height;
            var logoWidth = logoHeight * logoAspect;

            // Ensure logo doesn't exceed safe area width
            if (logoWidth > safeWidth)
            {
                logoWidth = safeWidth;
                logoHeight = logoWidth / logoAspect;
            }

            // Calculate positioning within safe area
            var logoX = CalculateLogoX(alignment, safeLeft, safeWidth, logoWidth);
            var logoY = CalculateLogoY(position, safeTop, safeHeight, logoHeight);

            var destRect = new SKRect(logoX, logoY, logoX + logoWidth, logoY + logoHeight);

            using var paint = new SKPaint
            {
                IsAntialias = true,
                FilterQuality = SKFilterQuality.High
            };

            canvas.DrawBitmap(logoBitmap, destRect, paint);

            return logoHeight;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to draw series logo image: {ex.Message}");
            return 0f;
        }
    }

    /// <summary>
    /// Draws the series name as text when no logo image is available.
    /// Uses configurable positioning with Position and Alignment enums within safe area constraints.
    /// Text is rendered with same positioning logic as logo images for consistent behavior.
    /// Supports text wrapping and uses episode font settings with slight size increase for prominence.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="seriesName">Name of the series to display as text.</param>
    /// <param name="position">Vertical position (Top, Center, Bottom) for text placement.</param>
    /// <param name="alignment">Horizontal alignment (Left, Center, Right) for text placement.</param>
    /// <param name="config">Plugin configuration with font settings and safe area calculations.</param>
    /// <param name="canvasWidth">Canvas width for positioning calculations.</param>
    /// <param name="canvasHeight">Canvas height for font size and positioning calculations.</param>
    /// <returns>Total height of the rendered text block.</returns>
    // MARK: DrawSeriesLogoText
    private float DrawSeriesLogoText(SKCanvas canvas, string seriesName, Position position, Alignment alignment, PluginConfiguration config, int canvasWidth, int canvasHeight)
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
            TextAlign = GetSKTextAlign(alignment)
        };

        using var shadowPaint = new SKPaint
        {
            Color = shadowColor,
            TextSize = logoFontSize,
            IsAntialias = true,
            Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, FontUtils.GetFontStyle(config.EpisodeFontStyle)),
            TextAlign = GetSKTextAlign(alignment)
        };

        // Calculate safe area and available width
        ApplySafeAreaConstraints(canvasWidth, canvasHeight, config, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);
        var availableWidth = safeWidth * 0.9f;
        var lines = TextUtils.FitTextToWidth(seriesName, logoPaint, availableWidth);

        var lineHeight = logoFontSize * 1.2f;
        var totalHeight = (lines.Count - 1) * lineHeight + logoFontSize;

        // Calculate positioning
        var textX = CalculateLogoX(alignment, safeLeft, safeWidth, 0);
        var textY = CalculateLogoY(position, safeTop, safeHeight, totalHeight);

        // Draw each line with shadow offset
        for (int i = 0; i < lines.Count; i++)
        {
            var lineY = textY + logoFontSize + (i * lineHeight);
            canvas.DrawText(lines[i], textX + 2, lineY + 2, shadowPaint);
            canvas.DrawText(lines[i], textX, lineY, logoPaint);
        }

        return totalHeight;
    }

    /// <summary>
    /// Calculates the horizontal X coordinate for logo positioning based on alignment within safe area.
    /// Supports left, center, and right alignment within the safe drawing area.
    /// </summary>
    /// <param name="alignment">Horizontal alignment (Left, Center, Right).</param>
    /// <param name="safeLeft">Left boundary of safe area.</param>
    /// <param name="safeWidth">Width of safe area.</param>
    /// <param name="logoWidth">Width of logo (0 for text alignment calculations).</param>
    /// <returns>X coordinate for logo positioning.</returns>
    // MARK: CalculateLogoX
    private float CalculateLogoX(Alignment alignment, float safeLeft, float safeWidth, float logoWidth)
    {
        return alignment switch
        {
            Alignment.Left => safeLeft,
            Alignment.Center => safeLeft + (safeWidth - logoWidth) / 2f,
            Alignment.Right => safeLeft + safeWidth - logoWidth,
            _ => safeLeft + (safeWidth - logoWidth) / 2f
        };
    }

    /// <summary>
    /// Calculates the vertical Y coordinate for logo positioning based on position within safe area.
    /// Supports top, center, and bottom positioning within the safe drawing area.
    /// </summary>
    /// <param name="position">Vertical position (Top, Center, Bottom).</param>
    /// <param name="safeTop">Top boundary of safe area.</param>
    /// <param name="safeHeight">Height of safe area.</param>
    /// <param name="logoHeight">Height of logo.</param>
    /// <returns>Y coordinate for logo positioning.</returns>
    // MARK: CalculateLogoY
    private float CalculateLogoY(Position position, float safeTop, float safeHeight, float logoHeight)
    {
        return position switch
        {
            Position.Top => safeTop,
            Position.Center => safeTop + (safeHeight - logoHeight) / 2f,
            Position.Bottom => safeTop + safeHeight - logoHeight,
            _ => safeTop + (safeHeight - logoHeight) / 2f
        };
    }

    /// <summary>
    /// Converts Alignment enum to corresponding SKTextAlign value for text rendering.
    /// </summary>
    /// <param name="alignment">Alignment enum value.</param>
    /// <returns>Corresponding SKTextAlign value.</returns>
    // MARK: GetSKTextAlign
    private SKTextAlign GetSKTextAlign(Alignment alignment)
    {
        return alignment switch
        {
            Alignment.Left => SKTextAlign.Left,
            Alignment.Center => SKTextAlign.Center,
            Alignment.Right => SKTextAlign.Right,
            _ => SKTextAlign.Center
        };
    }
}