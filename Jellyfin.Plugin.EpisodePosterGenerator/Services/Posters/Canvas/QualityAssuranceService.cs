using System;
using System.IO;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Ensures extracted frames are not too dark.
    /// Uses SkiaSharp to calculate average perceived brightness.
    /// </summary>
    public class QualityAssuranceService
    {
        private readonly ILogger<QualityAssuranceService> _logger;
        private readonly double _darkThreshold;

        /// <summary>
        /// Initialize with a brightness threshold.
        /// 0.0 = pure black, 1.0 = pure white.
        /// Frames with average brightness below this are considered too dark.
        /// </summary>
        public QualityAssuranceService(ILogger<QualityAssuranceService> logger, double darkThreshold = 0.05)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _darkThreshold = Math.Clamp(darkThreshold, 0.0, 1.0);
        }

        /// <summary>
        /// Returns true if the frame passes QA (not too dark).
        /// </summary>
        public bool IsFrameBrightEnough(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("QA check failed: file does not exist at {Path}", filePath);
                return false;
            }

            try
            {
                using var input = File.OpenRead(filePath);
                using var bitmap = SKBitmap.Decode(input);

                if (bitmap == null)
                {
                    _logger.LogError("Failed to decode image for QA check: {Path}", filePath);
                    return false;
                }

                double totalBrightness = 0;
                int pixelCount = 0;

                // Sample every 2 pixels to improve speed
                for (int y = 0; y < bitmap.Height; y += 2)
                {
                    for (int x = 0; x < bitmap.Width; x += 2)
                    {
                        var color = bitmap.GetPixel(x, y);
                        // Perceived brightness formula (luminance)
                        double brightness = (0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue) / 255.0;
                        totalBrightness += brightness;
                        pixelCount++;
                    }
                }

                double avgBrightness = totalBrightness / pixelCount;
                _logger.LogDebug("Average frame brightness: {Brightness}", avgBrightness);

                return avgBrightness >= _darkThreshold;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed QA check on frame {Path}", filePath);
                return false;
            }
        }
    }
}