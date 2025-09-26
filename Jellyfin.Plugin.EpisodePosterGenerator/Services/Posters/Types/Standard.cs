using System;
using System.Globalization;
using SkiaSharp;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

/// <summary>
/// Generates standard-style episode posters with layered rendering approach.
/// Renders background screenshot, optional overlay tint, and bottom-aligned text elements.
/// Text elements stack from bottom to top: episode info, separator line, episode title.
/// </summary>
public class StandardPosterGenerator : BasePosterGenerator, IPosterGenerator
{
    // MARK: - Public Interface
    public string? Generate(SKBitmap canvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, string? outputPath = null)
    {
        try
        {
            using var surface = SKSurface.Create(new SKImageInfo(canvas.Width, canvas.Height));
            var skCanvas = surface.Canvas;

            // MARK: Render Layers
            DrawImageLayer(skCanvas, canvas);
            DrawOverlayLayer(skCanvas, canvas.Width, canvas.Height, config);
            DrawTextLayer(skCanvas, episodeMetadata, config, canvas.Width, canvas.Height);

            // MARK: Encode & Save
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);

            if (outputPath != null)
            {
                using var outputStream = System.IO.File.OpenWrite(outputPath);
                data.SaveTo(outputStream);
            }

            return outputPath;
        }
        catch
        {
            return null;
        }
    }

    // MARK: - Layer 1: Background Image
    private void DrawImageLayer(SKCanvas canvas, SKBitmap bitmap)
    {
        canvas.Clear();
        canvas.DrawBitmap(bitmap, 0, 0);
    }

    // MARK: - Layer 2: Optional Overlay
    private void DrawOverlayLayer(SKCanvas canvas, int width, int height, PluginConfiguration config)
    {
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

    // MARK: - Layer 3: Bottom-aligned Text
    private void DrawTextLayer(SKCanvas canvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int canvasWidth, int canvasHeight)
    {
        var seasonNumber = episodeMetadata.SeasonNumber ?? 0;
        var episodeNumber = episodeMetadata.EpisodeNumberStart ?? 0;
        var episodeTitle = episodeMetadata.EpisodeName ?? "-";

        var safeAreaMargin = GetSafeAreaMargin(config);
        var bottomSafeAreaBoundary = canvasHeight - (canvasHeight * safeAreaMargin);

        float spacingHeight = canvasHeight * 0.02f;
        float currentBottomY = bottomSafeAreaBoundary;

        if (config.ShowTitle && config.ShowEpisode)
        {
            var titleHeight = DrawEpisodeTitle(canvas, episodeTitle, config, canvasWidth, canvasHeight, currentBottomY);
            currentBottomY -= titleHeight + spacingHeight;

            var lineHeight = DrawSeparatorLine(config, canvas, canvasWidth, currentBottomY);
            currentBottomY -= lineHeight + spacingHeight;

            DrawEpisodeInfo(canvas, seasonNumber, episodeNumber, config, canvasWidth, canvasHeight, currentBottomY);
        }
        else if (config.ShowTitle)
        {
            DrawEpisodeTitle(canvas, episodeTitle, config, canvasWidth, canvasHeight, currentBottomY);
        }
        else if (config.ShowEpisode)
        {
            DrawEpisodeInfo(canvas, seasonNumber, episodeNumber, config, canvasWidth, canvasHeight, currentBottomY);
        }
    }

    // MARK: Episode Title Rendering
    private float DrawEpisodeTitle(SKCanvas canvas, string title, PluginConfiguration config, int canvasWidth, int canvasHeight, float bottomY)
    {
        var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, canvasHeight, 100f * GetSafeAreaMargin(config));
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

        var safeWidth = canvasWidth * (1 - 2 * GetSafeAreaMargin(config)) * 0.9f;
        var lines = TextUtils.FitTextToWidth(title, titlePaint, safeWidth);

        var lineHeight = fontSize * 1.2f;
        var totalHeight = (lines.Count - 1) * lineHeight + fontSize;

        var centerX = canvasWidth / 2f;
        var startY = bottomY - totalHeight + fontSize;

        for (int i = 0; i < lines.Count; i++)
        {
            var lineY = startY + (i * lineHeight);
            canvas.DrawText(lines[i], centerX + 2, lineY + 2, shadowPaint);
            canvas.DrawText(lines[i], centerX, lineY, titlePaint);
        }

        return totalHeight;
    }

    // MARK: Separator Line Rendering
    private float DrawSeparatorLine(PluginConfiguration config, SKCanvas canvas, int canvasWidth, float y)
    {
        var margin = canvasWidth * GetSafeAreaMargin(config);
        var startX = margin;
        var endX = canvasWidth - margin;

        using var shadowPaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(180),
            StrokeWidth = 2f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        canvas.DrawLine(startX + 2, y + 2, endX + 2, y + 2, shadowPaint);

        using var linePaint = new SKPaint
        {
            Color = SKColors.White,
            StrokeWidth = 2f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        canvas.DrawLine(startX, y, endX, y, linePaint);

        return 4f;
    }

    // MARK: Episode Info (Season • Episode)
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

        var fontMetrics = episodePaint.FontMetrics;
        var baselineY = bottomY - Math.Abs(fontMetrics.Descent);

        var seasonWidth = episodePaint.MeasureText(seasonText);
        var episodeWidth = episodePaint.MeasureText(episodeText);
        var bulletWidth = bulletPaint.MeasureText(bulletText);

        var centerX = canvasWidth / 2f;
        var bulletX = centerX;
        var seasonX = bulletX - (bulletWidth / 2f) - (seasonWidth / 2f);
        var episodeX = bulletX + (bulletWidth / 2f) + (episodeWidth / 2f);

        canvas.DrawText(seasonText, seasonX + 2, baselineY + 2, shadowPaint);
        canvas.DrawText(seasonText, seasonX, baselineY, episodePaint);

        canvas.DrawText(bulletText, bulletX + 2, baselineY + 2, bulletShadowPaint);
        canvas.DrawText(bulletText, bulletX, baselineY, bulletPaint);

        canvas.DrawText(episodeText, episodeX + 2, baselineY + 2, shadowPaint);
        canvas.DrawText(episodeText, episodeX, baselineY, episodePaint);
    }
}