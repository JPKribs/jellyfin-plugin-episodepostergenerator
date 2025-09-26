using System;
using SkiaSharp;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Handles cropping and resizing of extracted episode frames for posters.
    /// Supports letterbox/pillarbox detection and PosterFill options.
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
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentNullException.ThrowIfNull(config);

            var result = source;
            var originalSize = $"{source.Width}x{source.Height}";

            // Step 1: Remove letterbox/pillarbox if enabled
            if (config.EnableLetterboxDetection)
            {
                var cropped = RemoveLetterboxAndPillarbox(result, config);
                if (cropped != result)
                {
                    _logger.LogInformation("Letterbox/pillarbox removed: {Original} -> {New}", 
                        originalSize, $"{cropped.Width}x{cropped.Height}");
                    
                    if (result != source) result.Dispose();
                    result = cropped;
                    
                    metadata.VideoWidth = cropped.Width;
                    metadata.VideoHeight = cropped.Height;
                }
            }

            // Step 2: Apply poster fill to achieve target dimensions (unless Original mode)
            if (config.PosterFill != PosterFill.Original)
            {
                var posterRatio = ParseAspectRatio(config.PosterDimensionRatio);
                var filled = ApplyPosterFill(result, posterRatio, config.PosterFill);
                
                if (filled != result)
                {
                    _logger.LogInformation("Poster fill applied: {Fill} ratio {Ratio}", 
                        config.PosterFill, config.PosterDimensionRatio);
                        
                    if (result != source) result.Dispose();
                    result = filled;
                }
            }
            else
            {
                _logger.LogDebug("PosterFill.Original selected - using image as-is after letterbox removal");
            }

            return result;
        }

        // MARK: ParseAspectRatio
        private float ParseAspectRatio(string ratio)
        {
            if (string.IsNullOrWhiteSpace(ratio)) 
                return 16f / 9f;

            var parts = ratio.Split(':');
            if (parts.Length == 2 &&
                float.TryParse(parts[0], out float w) &&
                float.TryParse(parts[1], out float h) &&
                h > 0)
            {
                return w / h;
            }

            _logger.LogWarning("Invalid aspect ratio format: {Ratio}, using 16:9", ratio);
            return 16f / 9f;
        }

        // MARK: RemoveLetterboxAndPillarbox
        private SKBitmap RemoveLetterboxAndPillarbox(SKBitmap source, PluginConfiguration config)
        {
            var blackThreshold = config.LetterboxBlackThreshold;
            var confidence = config.LetterboxConfidence / 100.0f;

            _logger.LogDebug("Letterbox detection: threshold={Threshold}, confidence={Confidence:F3}", 
                blackThreshold, confidence);

            var top = DetectTopLetterbox(source, blackThreshold, confidence);
            var bottom = DetectBottomLetterbox(source, blackThreshold, confidence);
            var left = DetectLeftPillarbox(source, blackThreshold, confidence);
            var right = DetectRightPillarbox(source, blackThreshold, confidence);

            var newWidth = source.Width - left - right;
            var newHeight = source.Height - top - bottom;

            _logger.LogDebug("Detected bounds: left={Left}, top={Top}, right={Right}, bottom={Bottom}", 
                left, top, right, bottom);
            _logger.LogDebug("Original: {OriginalWidth}x{OriginalHeight}, Detected: {NewWidth}x{NewHeight}", 
                source.Width, source.Height, newWidth, newHeight);

            if (newWidth <= 0 || newHeight <= 0 || 
                newWidth > source.Width || newHeight > source.Height ||
                (left == 0 && right == 0 && top == 0 && bottom == 0))
            {
                _logger.LogDebug("No significant letterbox/pillarbox detected");
                return source;
            }

            var minWidth = source.Width / 4;
            var minHeight = source.Height / 4;
            
            if (newWidth < minWidth || newHeight < minHeight)
            {
                _logger.LogWarning("Detected crop too aggressive, skipping: {NewWidth}x{NewHeight} vs minimum {MinWidth}x{MinHeight}",
                    newWidth, newHeight, minWidth, minHeight);
                return source;
            }

            var cropped = new SKBitmap(newWidth, newHeight, source.ColorType, source.AlphaType);
            using var canvas = new SKCanvas(cropped);

            var sourceRect = new SKRect(left, top, left + newWidth, top + newHeight);
            var destRect = new SKRect(0, 0, newWidth, newHeight);
            
            canvas.DrawBitmap(source, sourceRect, destRect);

            return cropped;
        }

        // MARK: DetectTopLetterbox
        private int DetectTopLetterbox(SKBitmap bitmap, int blackThreshold, float confidence)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            
            for (int y = 0; y < height / 2; y++)
            {
                int blackPixels = 0;
                for (int x = 0; x < width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (IsBlackPixel(pixel, blackThreshold))
                        blackPixels++;
                }
                
                float blackRatio = (float)blackPixels / width;
                if (blackRatio < confidence)
                    return y;
            }
            
            return 0;
        }

        // MARK: DetectBottomLetterbox
        private int DetectBottomLetterbox(SKBitmap bitmap, int blackThreshold, float confidence)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            
            for (int y = height - 1; y >= height / 2; y--)
            {
                int blackPixels = 0;
                for (int x = 0; x < width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (IsBlackPixel(pixel, blackThreshold))
                        blackPixels++;
                }
                
                float blackRatio = (float)blackPixels / width;
                if (blackRatio < confidence)
                    return height - 1 - y;
            }
            
            return 0;
        }

        // MARK: DetectLeftPillarbox
        private int DetectLeftPillarbox(SKBitmap bitmap, int blackThreshold, float confidence)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            
            for (int x = 0; x < width / 2; x++)
            {
                int blackPixels = 0;
                for (int y = 0; y < height; y++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (IsBlackPixel(pixel, blackThreshold))
                        blackPixels++;
                }
                
                float blackRatio = (float)blackPixels / height;
                if (blackRatio < confidence)
                    return x;
            }
            
            return 0;
        }

        // MARK: DetectRightPillarbox
        private int DetectRightPillarbox(SKBitmap bitmap, int blackThreshold, float confidence)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            
            for (int x = width - 1; x >= width / 2; x--)
            {
                int blackPixels = 0;
                for (int y = 0; y < height; y++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (IsBlackPixel(pixel, blackThreshold))
                        blackPixels++;
                }
                
                float blackRatio = (float)blackPixels / height;
                if (blackRatio < confidence)
                    return width - 1 - x;
            }
            
            return 0;
        }

        // MARK: IsBlackPixel
        private bool IsBlackPixel(SKColor pixel, int threshold)
        {
            var brightness = (pixel.Red + pixel.Green + pixel.Blue) / 3;
            return brightness <= threshold;
        }

        // MARK: ApplyPosterFill
        private SKBitmap ApplyPosterFill(SKBitmap source, float targetRatio, PosterFill fillMode)
        {
            var currentRatio = (float)source.Width / source.Height;
            
            // Skip if ratios are close enough (within 1%)
            if (Math.Abs(currentRatio - targetRatio) < 0.01f)
                return source;

            int targetWidth, targetHeight;

            switch (fillMode)
            {
                case PosterFill.Fit:
                    // Crop to fit target ratio (creates "zoomed" effect)
                    if (currentRatio > targetRatio)
                    {
                        // Source is wider than target - crop width (zoom in)
                        targetHeight = source.Height;
                        targetWidth = (int)(targetHeight * targetRatio);
                    }
                    else
                    {
                        // Source is taller than target - crop height (zoom in)
                        targetWidth = source.Width;
                        targetHeight = (int)(targetWidth / targetRatio);
                    }
                    break;

                case PosterFill.Fill:
                    // Stretch to fill target ratio (may distort aspect ratio)
                    var posterDimensions = CalculateTargetDimensions(source.Width, source.Height, targetRatio);
                    targetWidth = posterDimensions.width;
                    targetHeight = posterDimensions.height;
                    break;

                case PosterFill.Original:
                    // This case should not reach here due to check in CropPoster
                    return source;

                default:
                    return source;
            }

            if (targetWidth <= 0 || targetHeight <= 0)
                return source;

            var result = new SKBitmap(targetWidth, targetHeight, source.ColorType, source.AlphaType);
            using var canvas = new SKCanvas(result);

            if (fillMode == PosterFill.Fit)
            {
                // Center crop for "zoomed" effect
                var srcX = Math.Max(0, (source.Width - targetWidth) / 2);
                var srcY = Math.Max(0, (source.Height - targetHeight) / 2);
                
                var sourceRect = new SKRect(srcX, srcY, srcX + targetWidth, srcY + targetHeight);
                var destRect = new SKRect(0, 0, targetWidth, targetHeight);
                
                canvas.DrawBitmap(source, sourceRect, destRect);
            }
            else if (fillMode == PosterFill.Fill)
            {
                // Stretch to fill dimensions
                var sourceRect = new SKRect(0, 0, source.Width, source.Height);
                var destRect = new SKRect(0, 0, targetWidth, targetHeight);
                
                canvas.DrawBitmap(source, sourceRect, destRect);
            }

            return result;
        }

        // MARK: CalculateTargetDimensions
        private (int width, int height) CalculateTargetDimensions(int sourceWidth, int sourceHeight, float targetRatio)
        {
            // For fill mode, we want consistent poster dimensions based on the target ratio
            // Use the larger dimension as base to maintain quality
            if (sourceWidth > sourceHeight)
            {
                int width = sourceWidth;
                int height = (int)(width / targetRatio);
                return (width, height);
            }
            else
            {
                int height = sourceHeight;
                int width = (int)(height * targetRatio);
                return (width, height);
            }
        }
    }
}