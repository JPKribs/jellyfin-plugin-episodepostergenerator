using System;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;
using MediaBrowser.Controller.Entities.TV;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services;

public class PosterGeneratorService
{
    public string? ProcessImageWithText(string inputPath, string outputPath, Episode episode, PluginConfiguration config)
    {
        try
        {
            using var input = File.OpenRead(inputPath);
            using var original = SKBitmap.Decode(input);
            if (original == null)
                return null;

            var targetSize = GetTargetSize(original.Width, original.Height, config);
            using var scaled = new SKBitmap(targetSize.Width, targetSize.Height);
            using (var canvas = new SKCanvas(scaled))
            {
                canvas.Clear(SKColors.Black);
                DrawPosterImage(canvas, original, targetSize, config.PosterFill, original.Width, original.Height);
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");
            using (var image = SKImage.FromBitmap(scaled))
            using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 90))
            using (var output = File.OpenWrite(tempPath))
            {
                data.SaveTo(output);
            }

            return GeneratePoster(tempPath, outputPath, episode, config);
        }
        catch
        {
            return null;
        }
    }

    private string? GeneratePoster(string inputPath, string outputPath, Episode episode, PluginConfiguration config)
    {
        return config.PosterStyle switch
        {
            PosterStyle.Standard => new StandardPosterGenerator().Generate(inputPath, outputPath, episode, config),
            PosterStyle.Cutout => new CutoutPosterGenerator().Generate(inputPath, outputPath, episode, config),
            PosterStyle.Numeral => new NumeralPosterGenerator().Generate(inputPath, outputPath, episode, config),
            _ => null
        };
    }

    private SKSizeI GetTargetSize(int originalWidth, int originalHeight, PluginConfiguration config)
    {
        if (config.PosterFill == PosterFill.Original)
            return new SKSizeI(originalWidth, originalHeight);

        float targetAspect = ParseAspectRatio(config.PosterDimensionRatio);
        float originalAspect = (float)originalWidth / originalHeight;

        if (config.PosterFill == PosterFill.Fill)
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

        if (originalAspect > targetAspect)
        {
            int width = originalWidth;
            int height = (int)(width / targetAspect);
            return new SKSizeI(width, height);
        }
        else
        {
            int height = originalHeight;
            int width = (int)(height * targetAspect);
            return new SKSizeI(width, height);
        }
    }

    // MARK: ParseAspectRatio
    private float ParseAspectRatio(string ratio)
    {
        if (string.IsNullOrEmpty(ratio))
            return 16f / 9f;

        var parts = ratio.Split(':');
        if (parts.Length == 2 && 
            float.TryParse(parts[0], out var width) && 
            float.TryParse(parts[1], out var height) && 
            height > 0)
        {
            return width / height;
        }

        return 16f / 9f;
    }

    private void DrawPosterImage(SKCanvas canvas, SKBitmap original, SKSizeI targetSize, PosterFill fill, int originalWidth, int originalHeight)
    {
        var destRect = new SKRect(0, 0, targetSize.Width, targetSize.Height);

        if (fill == PosterFill.Fit)
        {
            var srcAspect = (float)originalWidth / originalHeight;
            var dstAspect = (float)targetSize.Width / targetSize.Height;

            SKRect srcRect;
            if (srcAspect > dstAspect)
            {
                int cropWidth = (int)(originalHeight * dstAspect);
                int x = (originalWidth - cropWidth) / 2;
                srcRect = new SKRect(x, 0, x + cropWidth, originalHeight);
            }
            else
            {
                int cropHeight = (int)(originalWidth / dstAspect);
                int y = (originalHeight - cropHeight) / 2;
                srcRect = new SKRect(0, y, originalWidth, y + cropHeight);
            }

            canvas.DrawBitmap(original, srcRect, destRect);
        }
        else
        {
            canvas.DrawBitmap(original, destRect);
        }
    }
}