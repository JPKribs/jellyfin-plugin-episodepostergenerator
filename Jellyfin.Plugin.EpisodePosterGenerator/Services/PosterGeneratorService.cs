using System;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using MediaBrowser.Controller.Entities.TV;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services;

public class PosterGeneratorService
{
    private readonly ILogger<PosterGeneratorService> _logger;

    public PosterGeneratorService(ILogger<PosterGeneratorService> logger)
    {
        _logger = logger;
    }

    public string? ProcessImageWithText(string inputPath, string outputPath, Episode episode, PluginConfiguration config)
    {
        try
        {
            using var input = File.OpenRead(inputPath);
            using var original = SKBitmap.Decode(input);
            if (original == null)
            {
                _logger.LogWarning("Could not decode image at path: {Path}", inputPath);
                return null;
            }

            var targetSize = GetTargetSize(original.Width, original.Height, config.PosterFill);
            using var scaled = new SKBitmap(targetSize.Width, targetSize.Height);
            using (var canvas = new SKCanvas(scaled))
            {
                canvas.Clear(SKColors.Black);
                DrawPosterImage(canvas, original, targetSize, config.PosterFill);

                DrawOverlayWithKnockout(canvas, scaled.Info, episode, config);
            }

            using var image = SKImage.FromBitmap(scaled);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var output = File.OpenWrite(outputPath);
            data.SaveTo(output);

            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process image with text overlay.");
            return null;
        }
    }

    private SKSizeI GetTargetSize(int originalWidth, int originalHeight, PosterFill fill)
    {
        const float targetAspect = 16f / 9f;

        if (fill == PosterFill.Original)
            return new SKSizeI(originalWidth, originalHeight);

        float originalAspect = (float)originalWidth / originalHeight;

        if (fill == PosterFill.Fill) // Pad to 16:9
        {
            if (originalAspect > targetAspect)
            {
                int height = originalHeight;
                int width = (int)(height * targetAspect);
                return new SKSizeI(width, height);
            }
            else
            {
                int width = originalWidth;
                int height = (int)(width / targetAspect);
                return new SKSizeI(width, height);
            }
        }

        // PosterFill.Fit: Crop to 16:9
        if (originalAspect > targetAspect)
        {
            int height = originalHeight;
            int width = (int)(height * targetAspect);
            return new SKSizeI(width, height);
        }
        else
        {
            int width = originalWidth;
            int height = (int)(width / targetAspect);
            return new SKSizeI(width, height);
        }
    }

    private void DrawPosterImage(SKCanvas canvas, SKBitmap original, SKSizeI targetSize, PosterFill fill)
    {
        var destRect = new SKRect(0, 0, targetSize.Width, targetSize.Height);

        if (fill == PosterFill.Fit)
        {
            var srcAspect = (float)original.Width / original.Height;
            var dstAspect = (float)targetSize.Width / targetSize.Height;

            SKRect srcRect;
            if (srcAspect > dstAspect)
            {
                int cropWidth = (int)(original.Height * dstAspect);
                int x = (original.Width - cropWidth) / 2;
                srcRect = new SKRect(x, 0, x + cropWidth, original.Height);
            }
            else
            {
                int cropHeight = (int)(original.Width / dstAspect);
                int y = (original.Height - cropHeight) / 2;
                srcRect = new SKRect(0, y, original.Width, y + cropHeight);
            }

            canvas.DrawBitmap(original, srcRect, destRect);
        }
        else
        {
            canvas.DrawBitmap(original, destRect);
        }
    }

    private void DrawOverlayWithKnockout(SKCanvas canvas, SKImageInfo info, Episode episode, PluginConfiguration config)
    {
        string knockoutText = $"S{episode.ParentIndexNumber:00}E{episode.IndexNumber:00}";
        string bottomText = episode.Name ?? "";

        // Draw black overlay
        using var overlayPaint = new SKPaint { Color = SKColor.Parse(config.OverlayColor) };
        canvas.DrawRect(new SKRect(0, 0, info.Width, info.Height), overlayPaint);

        // Prepare knockout text path
        using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
        using var textPaint = new SKPaint
        {
            Typeface = typeface,
            TextSize = info.Height / 4f,
            IsAntialias = true
        };

        var textPath = textPaint.GetTextPath(knockoutText, 0, 0);
        var bounds = new SKRect();
        textPath.GetBounds(out bounds);

        var scale = info.Width * 0.7f / bounds.Width;
        textPath.Transform(SKMatrix.CreateScale(scale, scale));
        textPath.GetBounds(out bounds);

        var dx = (info.Width - bounds.Width) / 2 - bounds.Left;
        var dy = (info.Height - bounds.Height) / 2 - bounds.Top;
        textPath.Transform(SKMatrix.CreateTranslation(dx, dy));

        // Create overlay path with knockout
        var fullRect = new SKPath();
        fullRect.AddRect(new SKRect(0, 0, info.Width, info.Height));
        fullRect.Op(textPath, SKPathOp.Difference);

        // Clip and clear to knockout
        canvas.Save();
        canvas.ClipPath(fullRect, SKClipOperation.Intersect);
        canvas.Clear(SKColors.Transparent);
        canvas.Restore();

        // Draw bottom text
        using var bottomPaint = new SKPaint
        {
            Typeface = SKTypeface.FromFamilyName("Arial"),
            TextSize = config.TitleFontSize,
            Color = SKColors.White,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };
        canvas.DrawText(bottomText, info.Width / 2, info.Height - 50, bottomPaint);
    }
}