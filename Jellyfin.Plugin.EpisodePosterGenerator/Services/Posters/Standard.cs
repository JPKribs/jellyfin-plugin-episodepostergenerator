using System;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using MediaBrowser.Controller.Entities.TV;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;

public class StandardPosterGenerator
{
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

            DrawTextOverlay(canvas, bitmap.Width, bitmap.Height, episode, config);

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

    private void DrawTextOverlay(SKCanvas canvas, int width, int height, Episode episode, PluginConfiguration config)
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
        var baseY = height - 40 - totalHeight;

        var episodeY = baseY + epBounds.Height;
        var titleY = episodeY + titleBounds.Height + 10;

        var epX = (width - epBounds.Width) / 2;
        var titleX = (width - titleBounds.Width) / 2;

        // Shadow
        canvas.DrawText(episodeText, epX + 2, episodeY + 2, episodeShadow);
        canvas.DrawText(titleText, titleX + 2, titleY + 2, titleShadow);

        // Main text
        canvas.DrawText(episodeText, epX, episodeY, episodePaint);
        canvas.DrawText(titleText, titleX, titleY, titlePaint);
    }

    private SKPaint CreateTextPaint(SKColor color, int size) => new()
    {
        Color = color,
        TextSize = size,
        IsAntialias = true,
        Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
    };
}