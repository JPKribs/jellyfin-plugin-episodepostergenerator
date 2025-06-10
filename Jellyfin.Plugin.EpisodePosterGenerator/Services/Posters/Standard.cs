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
                var overlayColor = ColorUtils.ParseHexColor(config.OverlayTint);
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
        var episodeText = $"S{episode.ParentIndexNumber ?? 0:D2}E{episode.IndexNumber ?? 0:D2}";
        var titleText = episode.Name ?? "Unknown Episode";

        var episodeColor = ColorUtils.ParseHexColor(config.EpisodeFontColor ?? "#FFFFFF");
        var titleColor = ColorUtils.ParseHexColor(config.TitleFontColor ?? "#FFFFFF");
        var shadowColor = SKColors.Black.WithAlpha(180);

        using var episodePaint = CreateTextPaint(episodeColor, config.EpisodeFontSize);
        using var titlePaint = CreateTextPaint(titleColor, config.TitleFontSize);
        using var episodeShadow = CreateTextPaint(shadowColor, config.EpisodeFontSize);
        using var titleShadow = CreateTextPaint(shadowColor, config.TitleFontSize);

        // Position at bottom of safe area
        float maxTitleWidth = safeWidth * 0.9f;
        var titleLines = WrapTitleText(titleText, titlePaint, maxTitleWidth);
        
        float lineHeight = config.TitleFontSize * 1.2f;
        float totalTitleHeight = titleLines.Count * lineHeight;
        float episodeHeight = config.EpisodeFontSize;
        float spacing = 10f;
        float totalHeight = episodeHeight + spacing + totalTitleHeight;
        
        float startY = safeTop + safeHeight - totalHeight - 20;
        
        // Draw episode code
        float episodeY = startY + episodeHeight;
        float centerX = safeLeft + (safeWidth / 2);
        
        canvas.DrawText(episodeText, centerX + 2, episodeY + 2, episodeShadow);
        canvas.DrawText(episodeText, centerX, episodeY, episodePaint);
        
        // Draw title lines
        float titleStartY = episodeY + spacing + lineHeight;
        for (int i = 0; i < titleLines.Count; i++)
        {
            float y = titleStartY + (i * lineHeight);
            canvas.DrawText(titleLines[i], centerX + 2, y + 2, titleShadow);
            canvas.DrawText(titleLines[i], centerX, y, titlePaint);
        }
    }

    // MARK: WrapTitleText
    private List<string> WrapTitleText(string text, SKPaint paint, float maxWidth)
    {
        var lines = new List<string>();
        
        // Try single line first
        if (paint.MeasureText(text) <= maxWidth)
        {
            lines.Add(text);
            return lines;
        }
        
        // Split into words for wrapping
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 1)
        {
            // Single word too long, truncate with ellipsis
            lines.Add(TruncateWithEllipsis(text, paint, maxWidth));
            return lines;
        }
        
        // Try to fit in two lines
        string line1 = "";
        string line2 = "";
        
        // Start with roughly half the words in each line
        int splitPoint = words.Length / 2;
        
        for (int i = 0; i < words.Length; i++)
        {
            if (i < splitPoint)
            {
                line1 += (i > 0 ? " " : "") + words[i];
            }
            else
            {
                line2 += (i > splitPoint ? " " : "") + words[i];
            }
        }
        
        // Adjust if lines are too long
        while (paint.MeasureText(line1) > maxWidth && line1.Contains(' ', StringComparison.Ordinal))
        {
            var lastSpace = line1.LastIndexOf(' ');
            var movedWord = line1.Substring(lastSpace + 1);
            line1 = line1.Substring(0, lastSpace);
            line2 = movedWord + " " + line2;
        }
        
        while (paint.MeasureText(line2) > maxWidth && line2.Contains(' ', StringComparison.Ordinal))
        {
            var firstSpace = line2.IndexOf(' ', StringComparison.Ordinal);
            var movedWord = line2.Substring(0, firstSpace);
            line2 = line2.Substring(firstSpace + 1);
            line1 += " " + movedWord;
        }
        
        // Final check and truncation if needed
        if (paint.MeasureText(line1) > maxWidth)
        {
            line1 = TruncateWithEllipsis(line1, paint, maxWidth);
        }
        
        if (paint.MeasureText(line2) > maxWidth)
        {
            line2 = TruncateWithEllipsis(line2, paint, maxWidth);
        }
        
        lines.Add(line1);
        if (!string.IsNullOrWhiteSpace(line2))
        {
            lines.Add(line2);
        }
        
        return lines;
    }

    // MARK: TruncateWithEllipsis
    private string TruncateWithEllipsis(string text, SKPaint paint, float maxWidth)
    {
        if (paint.MeasureText(text) <= maxWidth)
            return text;

        var ellipsis = "...";
        var ellipsisWidth = paint.MeasureText(ellipsis);
        var availableWidth = maxWidth - ellipsisWidth;

        for (int i = text.Length - 1; i >= 0; i--)
        {
            var substring = text.Substring(0, i);
            if (paint.MeasureText(substring) <= availableWidth)
            {
                return substring + ellipsis;
            }
        }

        return ellipsis;
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