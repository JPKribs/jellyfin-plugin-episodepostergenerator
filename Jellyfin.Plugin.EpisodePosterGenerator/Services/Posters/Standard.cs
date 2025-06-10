using System;
using System.Globalization;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using MediaBrowser.Controller.Entities.TV;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

public class StandardPosterGenerator : BasePosterGenerator, IPosterGenerator
{
    // MARK: Generate
    public string? Generate(string inputImagePath, string outputPath, Episode episode, PluginConfiguration config)
    {
        try
        {
            using var inputStream = File.OpenRead(inputImagePath);
            using var bitmap = SKBitmap.Decode(inputStream);
            using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height));
            var canvas = surface.Canvas;

            canvas.Clear();
            canvas.DrawBitmap(bitmap, 0, 0);

            ApplySafeAreaConstraints(bitmap.Width, bitmap.Height, out var safeWidth, out var safeHeight, out var safeLeft, out var safeTop);

            DrawTextOverlay(canvas, episode, config, safeLeft, safeTop, safeWidth, safeHeight);

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

    // MARK: DrawTextOverlay
    private void DrawTextOverlay(SKCanvas canvas, Episode episode, PluginConfiguration config, float safeLeft, float safeTop, float safeWidth, float safeHeight)
    {
        var episodeText = $"S{episode.ParentIndexNumber ?? 0:D2}E{episode.IndexNumber ?? 0:D2}";
        var titleText = episode.Name ?? "Unknown Episode";

        var textColor = ColorUtils.ParseHexColor(config.TextColor ?? "#FFFFFF");
        var shadowColor = SKColors.Black.WithAlpha(180);

        using var episodePaint = CreateTextPaint(textColor, config.EpisodeFontSize);
        using var titlePaint = CreateTextPaint(textColor, config.TitleFontSize);
        using var episodeShadow = CreateTextPaint(shadowColor, config.EpisodeFontSize);
        using var titleShadow = CreateTextPaint(shadowColor, config.TitleFontSize);

        var epBounds = new SKRect();
        var titleBounds = new SKRect();
        episodePaint.MeasureText(episodeText, ref epBounds);
        titlePaint.MeasureText(titleText, ref titleBounds);

        var totalHeight = epBounds.Height + titleBounds.Height + 10;
        
        float baseY;
        switch (config.TextPosition?.ToLower(CultureInfo.InvariantCulture))
        {
            case "top":
                baseY = safeTop + totalHeight + 20;
                break;
            case "bottomleft":
            case "bottomright":
            case "bottom":
            default:
                baseY = safeTop + safeHeight - 40;
                break;
        }

        var episodeY = baseY - totalHeight + epBounds.Height;
        var titleY = episodeY + titleBounds.Height + 10;

        float epX, titleX;
        switch (config.TextPosition?.ToLower(CultureInfo.InvariantCulture))
        {
            case "bottomleft":
                epX = safeLeft + 20;
                titleX = safeLeft + 20;
                episodePaint.TextAlign = SKTextAlign.Left;
                titlePaint.TextAlign = SKTextAlign.Left;
                episodeShadow.TextAlign = SKTextAlign.Left;
                titleShadow.TextAlign = SKTextAlign.Left;
                break;
            case "bottomright":
                epX = safeLeft + safeWidth - 20;
                titleX = safeLeft + safeWidth - 20;
                episodePaint.TextAlign = SKTextAlign.Right;
                titlePaint.TextAlign = SKTextAlign.Right;
                episodeShadow.TextAlign = SKTextAlign.Right;
                titleShadow.TextAlign = SKTextAlign.Right;
                break;
            default:
                epX = safeLeft + (safeWidth / 2);
                titleX = safeLeft + (safeWidth / 2);
                break;
        }

        canvas.DrawText(episodeText, epX + 2, episodeY + 2, episodeShadow);
        canvas.DrawText(titleText, titleX + 2, titleY + 2, titleShadow);

        canvas.DrawText(episodeText, epX, episodeY, episodePaint);
        canvas.DrawText(titleText, titleX, titleY, titlePaint);
    }

    // MARK: CreateTextPaint
    private SKPaint CreateTextPaint(SKColor color, int size) => new()
    {
        Color = color,
        TextSize = size,
        IsAntialias = true,
        Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
        TextAlign = SKTextAlign.Center
    };
}