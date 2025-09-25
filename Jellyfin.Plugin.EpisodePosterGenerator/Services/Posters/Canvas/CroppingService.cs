using System;
using System.IO;
using SkiaSharp;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Handles cropping and resizing of extracted episode frames for posters.
    /// Supports letterbox detection and PosterFill options.
    /// </summary>
    public class CroppingService
    {
        private readonly ILogger<CroppingService> _logger;

        // MARK: Constructor
        public CroppingService(ILogger<CroppingService> logger)
        {
            _logger = logger;
        }

        // MARK: CropPoster
        public SKBitmap CropPoster(SKBitmap source, VideoMetadata metadata, PluginConfiguration config)
        {
            ArgumentNullException.ThrowIfNull(source);

            SKBitmap cropped = source;

            // Letterbox detection
            if (config.EnableLetterboxDetection)
            {
                cropped = RemoveLetterbox(cropped, metadata, config.LetterboxBlackThreshold, config.LetterboxConfidence);
            }

            // Poster resizing/cropping
            cropped = RemoveLetterbox(cropped, metadata, (float)config.LetterboxBlackThreshold, (float)config.LetterboxConfidence);
            
            return cropped;
        }

        // MARK: RemoveLetterbox
        private SKBitmap RemoveLetterbox(SKBitmap bitmap, VideoMetadata metadata, float blackThreshold, float confidence)
        {
            int top = 0, bottom = bitmap.Height - 1;

            // Scan from top
            for (int y = 0; y < bitmap.Height; y++)
            {
                if (!IsRowMostlyBlack(bitmap, y, blackThreshold, confidence)) break;
                top = y;
            }

            // Scan from bottom
            for (int y = bitmap.Height - 1; y >= 0; y--)
            {
                if (!IsRowMostlyBlack(bitmap, y, blackThreshold, confidence)) break;
                bottom = y;
            }

            int newHeight = bottom - top + 1;
            if (newHeight <= 0) return bitmap; // fallback if something went wrong

            SKBitmap cropped = new SKBitmap(bitmap.Width, newHeight);
            using var canvas = new SKCanvas(cropped);
            canvas.DrawBitmap(bitmap, new SKRect(0, 0, bitmap.Width, newHeight), new SKRect(0, top, bitmap.Width, bottom + 1));

            // Update metadata
            metadata.VideoHeight = newHeight;
            metadata.VideoWidth = bitmap.Width;

            _logger.LogDebug("Letterbox removed: new dimensions {Width}x{Height}", bitmap.Width, newHeight);

            return cropped;
        }

        // MARK: IsRowMostlyBlack
        private bool IsRowMostlyBlack(SKBitmap bitmap, int y, float blackThreshold, float confidence)
        {
            int blackPixels = 0;
            for (int x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                double brightness = (0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue) / 255.0;
                if (brightness <= blackThreshold) blackPixels++;
            }

            double rowConfidence = blackPixels / (double)bitmap.Width;
            return rowConfidence >= confidence;
        }

        // MARK: ApplyPosterFill
        private SKBitmap ApplyPosterFill(SKBitmap bitmap, float posterRatio, PosterFill fill)
        {
            int targetWidth = bitmap.Width;
            int targetHeight = bitmap.Height;

            float currentRatio = (float)bitmap.Width / bitmap.Height;

            if (fill == PosterFill.Original)
                return bitmap;

            if ((fill == PosterFill.Fill && currentRatio != posterRatio) ||
                (fill == PosterFill.Fit && currentRatio != posterRatio))
            {
                // Determine new size
                if (fill == PosterFill.Fill)
                {
                    // Scale to fill poster ratio (may crop edges)
                    if (currentRatio < posterRatio)
                    {
                        targetWidth = (int)(bitmap.Height * posterRatio);
                        targetHeight = bitmap.Height;
                    }
                    else
                    {
                        targetHeight = (int)(bitmap.Width / posterRatio);
                        targetWidth = bitmap.Width;
                    }
                }
                else if (fill == PosterFill.Fit)
                {
                    // Scale to fit poster ratio (may add empty edges)
                    if (currentRatio > posterRatio)
                    {
                        targetWidth = (int)(bitmap.Height * posterRatio);
                        targetHeight = bitmap.Height;
                    }
                    else
                    {
                        targetHeight = (int)(bitmap.Width / posterRatio);
                        targetWidth = bitmap.Width;
                    }
                }

                SKBitmap result = new SKBitmap(targetWidth, targetHeight);
                using var canvas = new SKCanvas(result);
                canvas.Clear(SKColors.Transparent);

                // Calculate source rect to crop
                float scaleX = (float)bitmap.Width / targetWidth;
                float scaleY = (float)bitmap.Height / targetHeight;
                float scale = Math.Min(scaleX, scaleY);

                int srcWidth = (int)(targetWidth * scale);
                int srcHeight = (int)(targetHeight * scale);
                int srcX = (bitmap.Width - srcWidth) / 2;
                int srcY = (bitmap.Height - srcHeight) / 2;

                canvas.DrawBitmap(bitmap, new SKRect(srcX, srcY, srcX + srcWidth, srcY + srcHeight),
                    new SKRect(0, 0, targetWidth, targetHeight));

                return result;
            }

            return bitmap;
        }
    }
}