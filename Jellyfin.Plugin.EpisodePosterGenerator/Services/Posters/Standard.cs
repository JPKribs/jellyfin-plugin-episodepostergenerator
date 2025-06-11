using System;
using System.Collections.Generic;
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

            if (!string.IsNullOrEmpty(config.OverlayTint) && config.OverlayTint != "#00000000")
            {
                var tintValue = config.OverlayTint;
                
                // MARK: HandleOverlayTint
                if (tintValue.StartsWith('#') && tintValue.Length == 7)
                {
                    tintValue = string.Concat("#80", tintValue.AsSpan(1));
                }
                
                var overlayColor = ColorUtils.ParseHexColor(tintValue);
                using var overlayPaint = new SKPaint
                {
                    Color = overlayColor,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRect(SKRect.Create(bitmap.Width, bitmap.Height), overlayPaint);
            }

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
        var canvasWidth = canvas.DeviceClipBounds.Width;
        var canvasHeight = canvas.DeviceClipBounds.Height;
        
        DrawEpisodeInfo(canvas, episode, config, canvasWidth, canvasHeight);
        
        if (config.ShowTitle)
        {
            var titleText = episode.Name ?? "Unknown Episode";
            EpisodeTitleUtil.DrawTitle(canvas, titleText, TitlePosition.Bottom, config, canvasWidth, canvasHeight);
        }
    }
    
    // MARK: DrawEpisodeInfo
    private void DrawEpisodeInfo(SKCanvas canvas, Episode episode, PluginConfiguration config, float canvasWidth, float canvasHeight)
    {
        var seasonNumber = episode.ParentIndexNumber ?? 0;
        var episodeNumber = episode.IndexNumber ?? 0;
        
        var episodeFontSize = FontUtils.CalculateFontSizeFromPercentage(config.EpisodeFontSize, canvasHeight);
        var episodeColor = ColorUtils.ParseHexColor(config.EpisodeFontColor ?? "#FFFFFF");
        var shadowColor = SKColors.Black.WithAlpha(180);
        
        using var episodePaint = CreateTextPaint(episodeColor, episodeFontSize, config.EpisodeFontFamily, config.EpisodeFontStyle);
        using var shadowPaint = CreateTextPaint(shadowColor, episodeFontSize, config.EpisodeFontFamily, config.EpisodeFontStyle);
        
        episodePaint.TextAlign = SKTextAlign.Center;
        shadowPaint.TextAlign = SKTextAlign.Center;
        
        var seasonText = seasonNumber.ToString(CultureInfo.InvariantCulture);
        var episodeText = episodeNumber.ToString(CultureInfo.InvariantCulture);
        var bulletText = " • ";
        
        var seasonWidth = episodePaint.MeasureText(seasonText);
        var episodeWidth = episodePaint.MeasureText(episodeText);
        var bulletWidth = episodePaint.MeasureText(bulletText);
        
        var totalWidth = seasonWidth + bulletWidth + episodeWidth;
        var centerX = canvasWidth / 2f;
        
        var bulletX = centerX;
        var seasonX = bulletX - (bulletWidth / 2f) - (seasonWidth / 2f);
        var episodeX = bulletX + (bulletWidth / 2f) + (episodeWidth / 2f);
        
        var bottomOffset = config.ShowTitle ? canvasHeight * 0.25f : canvasHeight * 0.1f;
        var episodeY = canvasHeight - bottomOffset;
        
        canvas.DrawText(seasonText, seasonX + 2, episodeY + 2, shadowPaint);
        canvas.DrawText(seasonText, seasonX, episodeY, episodePaint);
        
        canvas.DrawText(bulletText, bulletX + 2, episodeY + 2, shadowPaint);
        canvas.DrawText(bulletText, bulletX, episodeY, episodePaint);
        
        canvas.DrawText(episodeText, episodeX + 2, episodeY + 2, shadowPaint);
        canvas.DrawText(episodeText, episodeX, episodeY, episodePaint);
        
        if (config.ShowTitle)
        {
            DrawSeparatorLine(canvas, episodeY + episodeFontSize * 0.3f, canvasWidth);
        }
    }
    
    // MARK: DrawSeparatorLine
    private void DrawSeparatorLine(SKCanvas canvas, float y, float canvasWidth)
    {
        var margin = canvasWidth * 0.05f; // 5% margin on each side
        var startX = margin;
        var endX = canvasWidth - margin;
        
        // Draw shadow first
        using var shadowPaint = new SKPaint
        {
            Color = SKColors.Black.WithAlpha(180),
            StrokeWidth = 2f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };
        
        canvas.DrawLine(startX + 2, y + 2, endX + 2, y + 2, shadowPaint);
        
        // Draw white line
        using var linePaint = new SKPaint
        {
            Color = SKColors.White,
            StrokeWidth = 2f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };
        
        canvas.DrawLine(startX, y, endX, y, linePaint);
    }

    // MARK: CreateTextPaint
    private SKPaint CreateTextPaint(SKColor color, int size, string fontFamily, string fontStyle) => new()
    {
        Color = color,
        TextSize = size,
        IsAntialias = true,
        Typeface = FontUtils.CreateTypeface(fontFamily, FontUtils.GetFontStyle(fontStyle)),
        TextAlign = SKTextAlign.Center
    };
}