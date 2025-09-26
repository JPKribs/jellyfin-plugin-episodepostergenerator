using System;
using System.IO;
using SkiaSharp;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Handles brightness analysis and HDR brightening operations on images.
    /// </summary>
    public class BrightnessService
    {
        private readonly ILogger<BrightnessService> _logger;
        private const double DefaultBrightnessThreshold = 0.05; // 5% brightness threshold

        // MARK: Constructor
        public BrightnessService(ILogger<BrightnessService> logger)
        {
            _logger = logger;
        }

        // MARK: IsFrameBrightEnough (SKBitmap overload)
        public bool IsFrameBrightEnough(SKBitmap bitmap, double threshold = DefaultBrightnessThreshold)
        {
            if (bitmap == null) return false;

            try
            {
                var brightness = CalculateAverageBrightness(bitmap);
                var isBrightEnough = brightness > threshold;

                _logger.LogDebug("Frame brightness: {Brightness:F3}, threshold: {Threshold:F3}, sufficient: {IsBright}", 
                    brightness, threshold, isBrightEnough);

                return isBrightEnough;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze frame brightness");
                return false;
            }
        }

        // MARK: IsFrameBrightEnough (file path overload)  
        public bool IsFrameBrightEnough(string filePath, double threshold = DefaultBrightnessThreshold)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                using var stream = File.OpenRead(filePath);
                using var bitmap = SKBitmap.Decode(stream);
                return IsFrameBrightEnough(bitmap, threshold);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze brightness for file: {FilePath}", filePath);
                return false;
            }
        }

        // MARK: BrightenBitmap
        public void BrightenBitmap(SKBitmap bitmap, double brightnessIncrease)
        {
            if (bitmap == null) return;
            if (brightnessIncrease <= 0) return;

            try
            {
                _logger.LogDebug("Brightening bitmap by {Increase}%", brightnessIncrease);

                var multiplier = 1.0f + (float)(brightnessIncrease / 100.0);

                // Process pixels in-place
                var pixels = bitmap.Pixels;
                for (int i = 0; i < pixels.Length; i++)
                {
                    var pixel = pixels[i];
                    
                    var newRed = Math.Min(255, (int)(pixel.Red * multiplier));
                    var newGreen = Math.Min(255, (int)(pixel.Green * multiplier));
                    var newBlue = Math.Min(255, (int)(pixel.Blue * multiplier));

                    pixels[i] = new SKColor((byte)newRed, (byte)newGreen, (byte)newBlue, pixel.Alpha);
                }

                bitmap.Pixels = pixels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to brighten bitmap");
            }
        }

        // MARK: Brighten (file path overload)
        public void Brighten(string filePath, double brightnessIncrease, PosterFileType fileType)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            if (brightnessIncrease <= 0)
                return;

            try
            {
                using var bitmap = SKBitmap.Decode(filePath);
                if (bitmap == null)
                {
                    _logger.LogWarning("Failed to decode image for brightening: {FilePath}", filePath);
                    return;
                }

                BrightenBitmap(bitmap, brightnessIncrease);

                // Save back to the same file
                SaveBitmap(bitmap, filePath, fileType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to brighten file: {FilePath}", filePath);
            }
        }

        // MARK: CalculateAverageBrightness
        private double CalculateAverageBrightness(SKBitmap bitmap)
        {
            if (bitmap == null) return 0;

            double totalBrightness = 0;
            int sampleCount = 0;

            // Sample every 4th pixel for performance
            var stepSize = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 100);

            for (int y = 0; y < bitmap.Height; y += stepSize)
            {
                for (int x = 0; x < bitmap.Width; x += stepSize)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    // Use ITU-R BT.709 luma coefficients
                    var brightness = (0.2126 * pixel.Red + 0.7152 * pixel.Green + 0.0722 * pixel.Blue) / 255.0;
                    totalBrightness += brightness;
                    sampleCount++;
                }
            }

            return sampleCount > 0 ? totalBrightness / sampleCount : 0;
        }

        // MARK: SaveBitmap
        private void SaveBitmap(SKBitmap bitmap, string filePath, PosterFileType fileType)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            
            if (data != null)
            {
                File.WriteAllBytes(filePath, data.ToArray());
            }
        }
    }
}