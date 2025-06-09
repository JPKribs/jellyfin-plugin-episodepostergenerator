using System;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using MediaBrowser.Controller.Entities.TV;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services;

public class ImageProcessingService
{
    private readonly ILogger<ImageProcessingService> _logger;

    public ImageProcessingService(ILogger<ImageProcessingService> logger)
    {
        _logger = logger;
    }

    // MARK: ProcessImageWithText
    public string? ProcessImageWithText(string inputImagePath, string outputPath, Episode episode, PluginConfiguration config)
    {
        try
        {
            using var inputStream = File.OpenRead(inputImagePath);
            using var bitmap = SKBitmap.Decode(inputStream);
            using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height));
            var canvas = surface.Canvas;

            canvas.DrawBitmap(bitmap, 0, 0);

            DrawTextOverlay(canvas, bitmap.Width, bitmap.Height, episode, config);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
            using var outputStream = File.OpenWrite(outputPath);
            data.SaveTo(outputStream);

            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process image with text overlay");
            return null;
        }
    }

    // MARK: DrawTextOverlay
    private void DrawTextOverlay(SKCanvas canvas, int width, int height, Episode episode, PluginConfiguration config)
    {
        var episodeText = FormatEpisodeText(episode);
        var titleText = episode.Name ?? "Unknown Episode";

        var textColor = GetSkiaColor(config.TextColor);
        var shadowColor = SKColors.Black.WithAlpha(128);

        using var episodePaint = CreateTextPaint(textColor, config.EpisodeFontSize, true);
        using var titlePaint = CreateTextPaint(textColor, config.TitleFontSize, false);
        using var shadowPaint = CreateTextPaint(shadowColor, config.EpisodeFontSize, true);
        using var titleShadowPaint = CreateTextPaint(shadowColor, config.TitleFontSize, false);

        var episodePosition = GetEpisodePosition(width, height, episodeText, episodePaint, config.TextPosition);
        var titlePosition = GetTitlePosition(width, height, titleText, titlePaint, config.TextPosition, episodePosition.Y + config.EpisodeFontSize);

        canvas.DrawText(episodeText, episodePosition.X + 2, episodePosition.Y + 2, shadowPaint);
        canvas.DrawText(episodeText, episodePosition.X, episodePosition.Y, episodePaint);

        canvas.DrawText(titleText, titlePosition.X + 2, titlePosition.Y + 2, titleShadowPaint);
        canvas.DrawText(titleText, titlePosition.X, titlePosition.Y, titlePaint);
    }

    // MARK: CreateTextPaint
    private SKPaint CreateTextPaint(SKColor color, int fontSize, bool isBold)
    {
        return new SKPaint
        {
            Color = color,
            TextSize = fontSize,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };
    }

    // MARK: GetSkiaColor
    private SKColor GetSkiaColor(string colorName)
    {
        return colorName?.ToLowerInvariant() switch
        {
            "black" => SKColors.Black,
            "white" => SKColors.White,
            "yellow" => SKColors.Yellow,
            "red" => SKColors.Red,
            "blue" => SKColors.Blue,
            "green" => SKColors.Green,
            _ => SKColors.White
        };
    }

    // MARK: GetEpisodePosition
    private (float X, float Y) GetEpisodePosition(int width, int height, string text, SKPaint paint, string position)
    {
        var textBounds = new SKRect();
        paint.MeasureText(text, ref textBounds);

        return position?.ToLowerInvariant() switch
        {
            "top" => ((width - textBounds.Width) / 2, 50 + textBounds.Height),
            "bottomleft" => (50, height - 120),
            "bottomright" => (width - textBounds.Width - 50, height - 120),
            _ => ((width - textBounds.Width) / 2, height - 120)
        };
    }

    // MARK: GetTitlePosition
    private (float X, float Y) GetTitlePosition(int width, int height, string text, SKPaint paint, string position, float episodeBottom)
    {
        var textBounds = new SKRect();
        paint.MeasureText(text, ref textBounds);

        var yOffset = episodeBottom + 10;

        return position?.ToLowerInvariant() switch
        {
            "top" => ((width - textBounds.Width) / 2, yOffset),
            "bottomleft" => (50, yOffset),
            "bottomright" => (width - textBounds.Width - 50, yOffset),
            _ => ((width - textBounds.Width) / 2, yOffset)
        };
    }

    // MARK: FormatEpisodeText
    private string FormatEpisodeText(Episode episode)
    {
        var seasonNumber = episode.ParentIndexNumber ?? 0;
        var episodeNumber = episode.IndexNumber ?? 0;
        return $"S{seasonNumber:D2}E{episodeNumber:D2}";
    }
}