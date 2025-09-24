using System;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Detects and crops black letterbox borders from poster images.
    /// Analyzes rows and columns of pixels to identify consistent black borders
    /// that can be safely removed to focus on actual content.
    /// </summary>
    public static class LetterboxDetectionService
    {
        /// <summary>
        /// Detects letterbox borders and returns cropping rectangle.
        /// Returns full image bounds if letterbox detection is disabled.
        /// </summary>
        // MARK: DetectLetterboxBounds
        public static SKRect DetectLetterboxBounds(SKBitmap bitmap, PluginConfiguration config)
        {
            if (!config.EnableLetterboxDetection)
            {
                return SKRect.Create(0, 0, bitmap.Width, bitmap.Height);
            }

            var width = bitmap.Width;
            var height = bitmap.Height;
            
            var topBound = DetectTopLetterbox(bitmap, config);
            var bottomBound = DetectBottomLetterbox(bitmap, config);
            var leftBound = DetectLeftLetterbox(bitmap, config);
            var rightBound = DetectRightLetterbox(bitmap, config);

            var x = leftBound;
            var y = topBound;
            var cropWidth = width - leftBound - rightBound;
            var cropHeight = height - topBound - bottomBound;

            return SKRect.Create(x, y, Math.Max(1, cropWidth), Math.Max(1, cropHeight));
        }

        /// <summary>
        /// Scans from top of image downward to find first non-letterbox row.
        /// Stops when percentage of black pixels drops below confidence threshold.
        /// </summary>
        // MARK: DetectTopLetterbox
        private static int DetectTopLetterbox(SKBitmap bitmap, PluginConfiguration config)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var confidenceThreshold = config.LetterboxConfidence / 100.0f;
            
            for (int y = 0; y < height / 2; y++)
            {
                int blackPixels = 0;
                for (int x = 0; x < width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (IsBlackPixel(pixel, config.LetterboxBlackThreshold))
                        blackPixels++;
                }
                
                float blackRatio = (float)blackPixels / width;
                if (blackRatio < confidenceThreshold)
                    return y;
            }
            
            return 0;
        }

        /// <summary>
        /// Scans from bottom of image upward to find first non-letterbox row.
        /// Returns distance from bottom edge to content.
        /// </summary>
        // MARK: DetectBottomLetterbox
        private static int DetectBottomLetterbox(SKBitmap bitmap, PluginConfiguration config)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var confidenceThreshold = config.LetterboxConfidence / 100.0f;
            
            for (int y = height - 1; y >= height / 2; y--)
            {
                int blackPixels = 0;
                for (int x = 0; x < width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (IsBlackPixel(pixel, config.LetterboxBlackThreshold))
                        blackPixels++;
                }
                
                float blackRatio = (float)blackPixels / width;
                if (blackRatio < confidenceThreshold)
                    return height - 1 - y;
            }
            
            return 0;
        }

        /// <summary>
        /// Scans from left edge rightward to find first non-letterbox column.
        /// Detects pillarboxing (vertical black bars on sides).
        /// </summary>
        // MARK: DetectLeftLetterbox
        private static int DetectLeftLetterbox(SKBitmap bitmap, PluginConfiguration config)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var confidenceThreshold = config.LetterboxConfidence / 100.0f;
            
            for (int x = 0; x < width / 2; x++)
            {
                int blackPixels = 0;
                for (int y = 0; y < height; y++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (IsBlackPixel(pixel, config.LetterboxBlackThreshold))
                        blackPixels++;
                }
                
                float blackRatio = (float)blackPixels / height;
                if (blackRatio < confidenceThreshold)
                    return x;
            }
            
            return 0;
        }

        /// <summary>
        /// Scans from right edge leftward to find first non-letterbox column.
        /// Returns distance from right edge to content.
        /// </summary>
        // MARK: DetectRightLetterbox
        private static int DetectRightLetterbox(SKBitmap bitmap, PluginConfiguration config)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var confidenceThreshold = config.LetterboxConfidence / 100.0f;
            
            for (int x = width - 1; x >= width / 2; x--)
            {
                int blackPixels = 0;
                for (int y = 0; y < height; y++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (IsBlackPixel(pixel, config.LetterboxBlackThreshold))
                        blackPixels++;
                }
                
                float blackRatio = (float)blackPixels / height;
                if (blackRatio < confidenceThreshold)
                    return width - 1 - x;
            }
            
            return 0;
        }

        /// <summary>
        /// Determines if a pixel is considered "black" based on brightness threshold.
        /// Uses average of RGB values for brightness calculation.
        /// </summary>
        // MARK: IsBlackPixel
        private static bool IsBlackPixel(SKColor pixel, int threshold)
        {
            var brightness = (pixel.Red + pixel.Green + pixel.Blue) / 3;
            return brightness <= threshold;
        }
    }
}