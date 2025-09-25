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
/// Generates standard-style episode posters with layered rendering approach.
/// Creates posters with episode screenshot background, optional overlay tint, and bottom-aligned text elements.
/// Text elements are stacked from bottom to top: episode info, separator line, and episode title.
/// </summary>
public class StandardPosterGenerator : BasePosterGenerator, IPosterGenerator
{
    /// <summary>
    /// Generates a standard-style poster image with layered rendering and bottom-aligned text elements.
    /// Renders in order: image layer, overlay layer, then text layer with proper spacing.
    /// </summary>
    /// <param name="inputImagePath">Path to the source image file.</param>
    /// <param name="outputPath">Path where the generated poster will be saved.</param>
    /// <param name="episode">Episode metadata containing season/episode numbers and title.</param>
    /// <param name="config">Plugin configuration with styling and font settings.</param>
    /// <returns>Path to the generated poster file, or null if generation fails.</returns>
    // MARK: Generate
    public string? Generate(string inputImagePath, string outputPath, Episode episode, PluginConfiguration config)
    {
        try
        {
            using var inputStream = File.OpenRead(inputImagePath);
            using var bitmap = SKBitmap.Decode(inputStream);
            using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height));
            var canvas = surface.Canvas;

            // Render layers in order: image, overlay, text
            DrawImageLayer(canvas, bitmap);
            DrawOverlayLayer(canvas, bitmap.Width, bitmap.Height, config);
            DrawTextLayer(canvas, episode, config, bitmap.Width, bitmap.Height);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
            using var outputStream = File.OpenWrite(outputPath);
            data.SaveTo(outputStream);

            return outputPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Draws the base image layer by clearing the canvas and rendering the source bitmap.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="bitmap">Source image bitmap to render.</param>
    // MARK: DrawImageLayer
    private void DrawImageLayer(SKCanvas canvas, SKBitmap bitmap)
    {
        canvas.Clear();
        canvas.DrawBitmap(bitmap, 0, 0);
    }

    /// <summary>
    /// Draws an optional color overlay layer on top of the image for tinting effects.
    /// Applies overlay only if a valid tint color is configured and not transparent.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="width">Canvas width in pixels.</param>
    /// <param name="height">Canvas height in pixels.</param>
    /// <param name="config">Plugin configuration containing overlay tint settings.</param>
    // MARK: DrawOverlayLayer
    private void DrawOverlayLayer(SKCanvas canvas, int width, int height, PluginConfiguration config)
    {
        // Skip overlay if no tint is configured or it's transparent
        if (string.IsNullOrEmpty(config.OverlayColor))
            return;

        var overlayColor = ColorUtils.ParseHexColor(config.OverlayColor);

        using var overlayPaint = new SKPaint
        {
            Color = overlayColor,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(SKRect.Create(width, height), overlayPaint);
    }

    /// <summary>
    /// Draws the text layer with bottom-aligned, stacked text elements.
    /// Handles various combinations of title and episode visibility:
    /// - Both enabled: episode info, separator line, episode title (bottom to top)
    /// - Title only: episode title positioned directly above safe area
    /// - Episode only: episode info positioned directly above safe area
    /// - Neither enabled: no text drawn
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="episode">Episode metadata for text content.</param>
    /// <param name="config">Plugin configuration with text styling settings.</param>
    /// <param name="canvasWidth">Canvas width in pixels.</param>
    /// <param name="canvasHeight">Canvas height in pixels.</param>
    // MARK: DrawTextLayer
    private void DrawTextLayer(SKCanvas canvas, Episode episode, PluginConfiguration config, int canvasWidth, int canvasHeight)
    {
        var seasonNumber = episode.ParentIndexNumber ?? 0;
        var episodeNumber = episode.IndexNumber ?? 0;
        var episodeTitle = episode.Name ?? "Unknown Episode";

        // Calculate where the safe area boundary is at the bottom
        var safeAreaMargin = GetSafeAreaMargin(config);
        var bottomSafeAreaBoundary = canvasHeight - (canvasHeight * safeAreaMargin);

        if (config.ShowTitle && config.ShowEpisode)
        {
            // Both title and episode enabled: use stacked layout with spacing between elements
            var spacingHeight = canvasHeight * 0.02f;
            var currentBottomY = bottomSafeAreaBoundary; // Start at safe area boundary

            // Draw episode title (bottom element) - position it ABOVE the safe area
            var titleHeight = DrawEpisodeTitle(canvas, episodeTitle, config, canvasWidth, canvasHeight, currentBottomY);
            currentBottomY -= titleHeight + spacingHeight;

            // Draw separator line between title and episode info
            var lineHeight = DrawSeparatorLine(config, canvas, canvasWidth, currentBottomY);
            currentBottomY -= lineHeight + spacingHeight;

            // Draw episode info (top element)
            DrawEpisodeInfo(canvas, seasonNumber, episodeNumber, config, canvasWidth, canvasHeight, currentBottomY);
        }
        else if (config.ShowTitle)
        {
            // Title only: position title directly above the bottom safe area boundary
            DrawEpisodeTitle(canvas, episodeTitle, config, canvasWidth, canvasHeight, bottomSafeAreaBoundary);
        }
        else if (config.ShowEpisode)
        {
            // Episode only: position episode info directly above the bottom safe area boundary
            DrawEpisodeInfo(canvas, seasonNumber, episodeNumber, config, canvasWidth, canvasHeight, bottomSafeAreaBoundary);
        }
        // If neither title nor episode are enabled, draw nothing
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
    /// Draws a horizontal separator line across the canvas width with shadow effect.
    /// Line spans the inherited safe area width and is drawn with white color over a black shadow.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="canvasWidth">Canvas width for line positioning.</param>
    /// <param name="y">Y coordinate for the line position.</param>
    /// <returns>Height of the drawn line element (including stroke width).</returns>
    // MARK: DrawSeparatorLine
    private float DrawSeparatorLine(PluginConfiguration config, SKCanvas canvas, int canvasWidth, float y)
    {
        // Use inherited SafeAreaMargin for consistent positioning
        var margin = canvasWidth * GetSafeAreaMargin(config);
        var startX = margin;
        var endX = canvasWidth - margin;

        // Draw shadow line first
        using var shadowPaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(180),
            StrokeWidth = 2f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        canvas.DrawLine(startX + 2, y + 2, endX + 2, y + 2, shadowPaint);

        // Draw main white line
        using var linePaint = new SKPaint
        {
            Color = SKColors.White,
            StrokeWidth = 2f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        canvas.DrawLine(startX, y, endX, y, linePaint);

        return 4f; // Line height including stroke width
    }

    /// <summary>
    /// Draws episode information (season and episode numbers) centered on the bullet separator.
    /// Format: "S • E" where S is season number, E is episode number, centered on the bullet.
    /// The bullet separator uses the same font size as numbers but with normal font style.
    /// All text elements are rendered with shadow effects for better visibility.
    /// Positions text so the bottom of descenders aligns with the specified Y coordinate.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="seasonNumber">Season number to display.</param>
    /// <param name="episodeNumber">Episode number to display.</param>
    /// <param name="config">Plugin configuration with episode font settings.</param>
    /// <param name="canvasWidth">Canvas width for centering calculations.</param>
    /// <param name="canvasHeight">Canvas height for font size calculations.</param>
    /// <param name="bottomY">Y coordinate where the bottom of the text should be positioned.</param>
    // MARK: DrawEpisodeInfo
    private void DrawEpisodeInfo(SKCanvas canvas, int seasonNumber, int episodeNumber, PluginConfiguration config, int canvasWidth, int canvasHeight, float bottomY)
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

        // Bullet uses same font size but normal style
        using var bulletPaint = new SKPaint
        {
            Color = episodeColor,
            TextSize = episodeFontSize,
            IsAntialias = true,
            Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, SKFontStyle.Normal),
            TextAlign = SKTextAlign.Center
        };

        using var bulletShadowPaint = new SKPaint
        {
            Color = shadowColor,
            TextSize = episodeFontSize,
            IsAntialias = true,
            Typeface = FontUtils.CreateTypeface(config.EpisodeFontFamily, SKFontStyle.Normal),
            TextAlign = SKTextAlign.Center
        };

        var seasonText = seasonNumber.ToString(CultureInfo.InvariantCulture);
        var episodeText = episodeNumber.ToString(CultureInfo.InvariantCulture);
        var bulletText = " • ";

        // Calculate baseline position so text bottom aligns with bottomY
        var fontMetrics = episodePaint.FontMetrics;
        var baselineY = bottomY - Math.Abs(fontMetrics.Descent);

        // Measure text widths for precise positioning
        var seasonWidth = episodePaint.MeasureText(seasonText);
        var episodeWidth = episodePaint.MeasureText(episodeText);
        var bulletWidth = bulletPaint.MeasureText(bulletText);

        // Center the bullet, then position season and episode text relative to it
        var centerX = canvasWidth / 2f;
        var bulletX = centerX;
        var seasonX = bulletX - (bulletWidth / 2f) - (seasonWidth / 2f);
        var episodeX = bulletX + (bulletWidth / 2f) + (episodeWidth / 2f);

        // Draw season and episode numbers
        canvas.DrawText(seasonText, seasonX + 2, baselineY + 2, shadowPaint);
        canvas.DrawText(seasonText, seasonX, baselineY, episodePaint);

        // Draw bullet with same baseline as numbers
        canvas.DrawText(bulletText, bulletX + 2, baselineY + 2, bulletShadowPaint);
        canvas.DrawText(bulletText, bulletX, baselineY, bulletPaint);

        canvas.DrawText(episodeText, episodeX + 2, baselineY + 2, shadowPaint);
        canvas.DrawText(episodeText, episodeX, baselineY, episodePaint);
    }
}