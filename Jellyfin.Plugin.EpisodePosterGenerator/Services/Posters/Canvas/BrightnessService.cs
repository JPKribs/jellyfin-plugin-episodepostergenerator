using System;
using System.IO;
using SkiaSharp;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    // BrightnessService
    // Handles brightness analysis and brightening operations on images.
    public class BrightnessService
    {
        private readonly ILogger<BrightnessService> _logger;
        private const double DefaultBrightnessThreshold = 0.05;

        // BrightnessService
        // Initializes the brightness service with a logger.
        public BrightnessService(ILogger<BrightnessService> logger)
        {
            _logger = logger;
        }

        // IsFrameBrightEnough
        // Determines if a bitmap meets the minimum brightness threshold.
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

        // IsFrameBrightEnough
        // Determines if an image file meets the minimum brightness threshold.
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

        // BrightenBitmap
        // Increases the brightness of a bitmap by the specified percentage.
        public void BrightenBitmap(SKBitmap bitmap, double brightnessIncrease)
        {
            if (bitmap == null) return;
            if (brightnessIncrease <= 0) return;

            try
            {
                _logger.LogDebug("Brightening bitmap by {Increase}%", brightnessIncrease);

                var multiplier = 1.0f + (float)(brightnessIncrease / 100.0);

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

        // Brighten
        // Loads an image file, increases its brightness, and saves it back.
        public void Brighten(string filePath, double brightnessIncrease)
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

                SaveBitmap(bitmap, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to brighten file: {FilePath}", filePath);
            }
        }

        // AnalysisSize
        // Target size for downscaled analysis (200x200 is sufficient for brightness metrics).
        private const int AnalysisSize = 200;

        // CalculateAverageBrightness
        // Computes the average brightness using downscaled bitmap with SKColor pixel access.
        private double CalculateAverageBrightness(SKBitmap bitmap)
        {
            if (bitmap == null) return 0;

            try
            {
                // Downscale for faster analysis
                float scale = Math.Min((float)AnalysisSize / bitmap.Width, (float)AnalysisSize / bitmap.Height);
                int newWidth = Math.Max(1, (int)(bitmap.Width * scale));
                int newHeight = Math.Max(1, (int)(bitmap.Height * scale));

                using var analysis = new SKBitmap(newWidth, newHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
                using var canvas = new SKCanvas(analysis);
                using var paint = new SKPaint { FilterQuality = SKFilterQuality.Low };
                canvas.DrawBitmap(bitmap, SKRect.Create(newWidth, newHeight), paint);

                var pixels = analysis.Pixels;
                if (pixels == null || pixels.Length == 0) return 0;

                double totalBrightness = 0;

                for (int i = 0; i < pixels.Length; i++)
                {
                    var c = pixels[i];
                    // ITU-R BT.709 luma: Y = 0.2126*R + 0.7152*G + 0.0722*B
                    totalBrightness += (0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue) / 255.0;
                }

                return totalBrightness / pixels.Length;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate average brightness, returning 0");
                return 0;
            }
        }

        // SaveBitmap
        // Saves a bitmap to a file in PNG format.
        private void SaveBitmap(SKBitmap bitmap, string filePath)
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
