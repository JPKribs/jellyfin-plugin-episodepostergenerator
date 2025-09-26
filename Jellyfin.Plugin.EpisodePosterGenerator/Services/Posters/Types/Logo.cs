using System;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

/// <summary>
/// Generates logo-style episode posters with series logo, episode code, and optional title.
/// Supports layered rendering: background bitmap, overlay, positioned logo, and bottom-aligned text.
/// </summary>
public class LogoPosterGenerator : BasePosterGenerator, IPosterGenerator
{
    // MARK: - Public Interface

    public string? Generate(SKBitmap canvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, string? outputPath)
    {
        try
        {
            // MARK: Temp Output Path
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(
                    Path.GetTempPath(),
                    $"{Guid.NewGuid()}.{config.PosterFileType.ToString().ToLowerInvariant()}");
            }

            int width = canvas.Width;
            int height = canvas.Height;

            var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var skCanvas = surface.Canvas;
            skCanvas.Clear(SKColors.Transparent);

            // MARK: Layers
            DrawBackgroundLayer(skCanvas, canvas);
            DrawOverlayLayer(skCanvas, width, height, config);
            DrawSeriesLogoLayer(skCanvas, episodeMetadata, config, width, height);
            DrawTextLayers(skCanvas, episodeMetadata, config, width, height);

            // MARK: Encode and Save
            using var finalImage = surface.Snapshot();
            using var data = finalImage.Encode(config.PosterFileType switch
            {
                PosterFileType.JPEG => SKEncodedImageFormat.Jpeg,
                PosterFileType.PNG => SKEncodedImageFormat.Png,
                PosterFileType.WEBP => SKEncodedImageFormat.Webp,
                PosterFileType.GIF => SKEncodedImageFormat.Gif,
                _ => SKEncodedImageFormat.Jpeg
            }, 95);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

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

    // MARK: - Layer 1: Background
    private void DrawBackgroundLayer(SKCanvas canvas, SKBitmap background)
    {
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawBitmap(background, 0, 0, paint);
    }

    // MARK: - Layer 2: Overlay
    private void DrawOverlayLayer(SKCanvas canvas, int width, int height, PluginConfiguration config)
    {
        var overlayColor = ColorUtils.ParseHexColor(config.OverlayColor);
        if (overlayColor.Alpha > 0)
        {
            using var paint = new SKPaint { Color = overlayColor, Style = SKPaintStyle.Fill };
            canvas.DrawRect(SKRect.Create(width, height), paint);
        }
    }

    // MARK: - Layer 3: Series Logo
    private void DrawSeriesLogoLayer(SKCanvas canvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int width, int height)
    {
        var seriesName = episodeMetadata.SeriesName ?? "Unknown Series";
        var logoPath = GetSeriesLogoPath(episodeMetadata);

        if (!string.IsNullOrEmpty(logoPath))
            DrawSeriesLogoImage(canvas, logoPath, config.LogoPosition, config.LogoAlignment, config, width, height);
        else
            DrawSeriesLogoText(canvas, seriesName, config.LogoPosition, config.LogoAlignment, config, width, height);
    }

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

    private float DrawSeriesLogoImage(SKCanvas canvas, string logoPath, Position position, Alignment alignment, PluginConfiguration config, int width, int height)
    {
        try
        {
            using var stream = File.OpenRead(logoPath);
            using var bitmap = SKBitmap.Decode(stream);
            if (bitmap == null) return 0f;

            ApplySafeAreaConstraints(width, height, config, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);

            var logoHeight = height * (config.LogoHeight / 100f);
            var aspect = (float)bitmap.Width / bitmap.Height;
            var logoWidth = logoHeight * aspect;

            if (logoWidth > safeWidth)
            {
                logoWidth = safeWidth;
                logoHeight = logoWidth / aspect;
            }

            var x = CalculateLogoX(alignment, safeLeft, safeWidth, logoWidth);
            var y = CalculateLogoY(position, safeTop, safeHeight, logoHeight);
            var rect = new SKRect(x, y, x + logoWidth, y + logoHeight);

            using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
            canvas.DrawBitmap(bitmap, rect, paint);

            return logoHeight;
        }
        catch { return 0f; }
    }

    private float DrawSeriesLogoText(SKCanvas canvas, string seriesName, Position position, Alignment alignment, PluginConfiguration config, int width, int height)
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

        ApplySafeAreaConstraints(width, height, config, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);
        var availableWidth = safeWidth * 0.9f;
        var lines = TextUtils.FitTextToWidth(seriesName, paint, availableWidth);

        var lineHeight = fontSize * 1.2f;
        var totalHeight = (lines.Count - 1) * lineHeight + fontSize;

        var x = CalculateLogoX(alignment, safeLeft, safeWidth, 0);
        var y = CalculateLogoY(position, safeTop, safeHeight, totalHeight);

        for (int i = 0; i < lines.Count; i++)
        {
            var lineY = y + fontSize + (i * lineHeight);
            canvas.DrawText(lines[i], x + 2, lineY + 2, shadowPaint);
            canvas.DrawText(lines[i], x, lineY, paint);
        }

        return totalHeight;
    }

    // MARK: - Layer 4: Text Drawing
    private void DrawTextLayers(SKCanvas canvas, EpisodeMetadata episodeMetadata, PluginConfiguration config, int width, int height)
    {
        float spacing = height * 0.02f;
        ApplySafeAreaConstraints(width, height, config, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);

        float currentY = height - (height * GetSafeAreaMargin(config));

        if (config.ShowTitle && !string.IsNullOrEmpty(episodeMetadata.EpisodeName))
        {
            var titleHeight = DrawEpisodeTitle(canvas, episodeMetadata.EpisodeName, config, width, height, currentY);
            currentY -= titleHeight + spacing;
        }

        if (config.ShowEpisode)
        {
            DrawEpisodeCode(canvas, episodeMetadata.SeasonNumber ?? 0, episodeMetadata.EpisodeNumberStart ?? 0, config, width, height, currentY);
        }
    }

    private float DrawEpisodeTitle(SKCanvas canvas, string title, PluginConfiguration config, int width, int height, float bottomY)
    {
        var fontSize = FontUtils.CalculateFontSizeFromPercentage(config.TitleFontSize, height, (100f * GetSafeAreaMargin(config)));
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

        var safeWidth = width * (1 - 2 * GetSafeAreaMargin(config)) * 0.9f;
        var lines = TextUtils.FitTextToWidth(title, titlePaint, safeWidth);

        var lineHeight = fontSize * 1.2f;
        var totalHeight = (lines.Count - 1) * lineHeight + fontSize;
        var centerX = width / 2f;
        var startY = bottomY - totalHeight + fontSize;

        for (int i = 0; i < lines.Count; i++)
        {
            var lineY = startY + (i * lineHeight);
            canvas.DrawText(lines[i], centerX + 2, lineY + 2, shadowPaint);
            canvas.DrawText(lines[i], centerX, lineY, titlePaint);
        }

        return totalHeight;
    }

    private float DrawEpisodeCode(SKCanvas canvas, int seasonNumber, int episodeNumber, PluginConfiguration config, int width, int height, float bottomY)
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

        var code = EpisodeCodeUtil.FormatEpisodeCode(seasonNumber, episodeNumber);
        var centerX = width / 2f;
        canvas.DrawText(code, centerX + 2, bottomY + 2, shadowPaint);
        canvas.DrawText(code, centerX, bottomY, paint);

        return fontSize;
    }

    // MARK: - Helpers
    private float CalculateLogoX(Alignment alignment, float safeLeft, float safeWidth, float logoWidth) => alignment switch
    {
        Alignment.Left => safeLeft,
        Alignment.Center => safeLeft + (safeWidth - logoWidth) / 2f,
        Alignment.Right => safeLeft + safeWidth - logoWidth,
        _ => safeLeft + (safeWidth - logoWidth) / 2f
    };

    private float CalculateLogoY(Position position, float safeTop, float safeHeight, float logoHeight) => position switch
    {
        Position.Top => safeTop,
        Position.Center => safeTop + (safeHeight - logoHeight) / 2f,
        Position.Bottom => safeTop + safeHeight - logoHeight,
        _ => safeTop + (safeHeight - logoHeight) / 2f
    };

    private SKTextAlign GetSKTextAlign(Alignment alignment) => alignment switch
    {
        Alignment.Left => SKTextAlign.Left,
        Alignment.Center => SKTextAlign.Center,
        Alignment.Right => SKTextAlign.Right,
        _ => SKTextAlign.Center
    };
}