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
                    
                    // Update metadata with new dimensions
                    metadata.VideoWidth = cropped.Width;
                    metadata.VideoHeight = cropped.Height;
                }
            }

            // Step 2: Apply poster fill transformations
            var posterRatio = ParseAspectRatio(config.PosterDimensionRatio);
            var filled = ApplyPosterFill(result, posterRatio, config.PosterFill);
            
            if (filled != result)
            {
                _logger.LogInformation("Poster fill applied: {Fill} ratio {Ratio}", 
                    config.PosterFill, config.PosterDimensionRatio);
                    
                if (result != source) result.Dispose();
                result = filled;
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
            // Convert config values to proper thresholds
            var blackThreshold = config.LetterboxBlackThreshold / 255.0f; // Convert 0-255 to 0.0-1.0
            var confidence = config.LetterboxConfidence / 100.0f; // Convert 0-100 to 0.0-1.0

            _logger.LogDebug("Letterbox detection: threshold={Threshold:F3}, confidence={Confidence:F3}", 
                blackThreshold, confidence);

            // Detect letterbox (horizontal black bars)
            var (top, bottom) = DetectHorizontalBars(source, blackThreshold, confidence);
            
            // Detect pillarbox (vertical black bars)  
            var (left, right) = DetectVerticalBars(source, blackThreshold, confidence);

            // Calculate new dimensions
            var newWidth = right - left + 1;
            var newHeight = bottom - top + 1;

            _logger.LogDebug("Detected bounds: left={Left}, top={Top}, right={Right}, bottom={Bottom}", 
                left, top, right, bottom);
            _logger.LogDebug("Original: {OriginalWidth}x{OriginalHeight}, Detected: {NewWidth}x{NewHeight}", 
                source.Width, source.Height, newWidth, newHeight);

            // Check if cropping is needed and valid
            if (newWidth <= 0 || newHeight <= 0 || 
                newWidth > source.Width || newHeight > source.Height ||
                (left == 0 && right == source.Width - 1 && top == 0 && bottom == source.Height - 1))
            {
                _logger.LogDebug("No significant letterbox/pillarbox detected");
                return source;
            }

            // Ensure minimum size (don't crop to less than 25% of original)
            var minWidth = source.Width / 4;
            var minHeight = source.Height / 4;
            
            if (newWidth < minWidth || newHeight < minHeight)
            {
                _logger.LogWarning("Detected crop too aggressive, skipping: {NewWidth}x{NewHeight} vs minimum {MinWidth}x{MinHeight}",
                    newWidth, newHeight, minWidth, minHeight);
                return source;
            }

            // Create cropped bitmap
            var cropped = new SKBitmap(newWidth, newHeight, source.ColorType, source.AlphaType);
            using var canvas = new SKCanvas(cropped);

            var sourceRect = new SKRect(left, top, right + 1, bottom + 1);
            var destRect = new SKRect(0, 0, newWidth, newHeight);
            
            canvas.DrawBitmap(source, sourceRect, destRect);

            return cropped;
        }

        // MARK: DetectHorizontalBars
        private (int top, int bottom) DetectHorizontalBars(SKBitmap bitmap, float blackThreshold, float confidence)
        {
            var height = bitmap.Height;
            var width = bitmap.Width;
            var maxScanLines = height / 4;
            
            int top = 0;
            int bottom = height - 1;

            // Scan from top
            for (int y = 0; y < maxScanLines && y < height; y++)
            {
                if (!IsRowMostlyBlack(bitmap, y, blackThreshold, confidence))
                {
                    break;
                }
                top = y + 1;
            }

            // Scan from bottom  
            for (int y = height - 1; y >= height - maxScanLines && y >= 0; y--)
            {
                if (!IsRowMostlyBlack(bitmap, y, blackThreshold, confidence))
                {
                    break;
                }
                bottom = y - 1;
            }

            return (top, bottom);
        }

        // MARK: DetectVerticalBars
        private (int left, int right) DetectVerticalBars(SKBitmap bitmap, float blackThreshold, float confidence)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var maxScanColumns = width / 4;
            
            int left = 0;
            int right = width - 1;

            // Scan from left
            for (int x = 0; x < maxScanColumns && x < width; x++)
            {
                if (!IsColumnMostlyBlack(bitmap, x, blackThreshold, confidence))
                {
                    break;
                }
                left = x + 1;
            }

            // Scan from right
            for (int x = width - 1; x >= width - maxScanColumns && x >= 0; x--)
            {
                if (!IsColumnMostlyBlack(bitmap, x, blackThreshold, confidence))
                {
                    break;
                }
                right = x - 1;
            }

            return (left, right);
        }

        // MARK: IsRowMostlyBlack
        private bool IsRowMostlyBlack(SKBitmap bitmap, int y, float blackThreshold, float confidence)
        {
            var width = bitmap.Width;
            var sampleStep = Math.Max(1, width / 100); // Sample more points for better accuracy
            int blackPixels = 0;
            int totalSamples = 0;

            // Also sample some pixels in the middle to avoid edge artifacts
            var startX = Math.Max(0, width / 10); // Skip first 10%
            var endX = Math.Min(width, width - width / 10); // Skip last 10%

            for (int x = startX; x < endX; x += sampleStep)
            {
                var pixel = bitmap.GetPixel(x, y);
                var brightness = CalculateBrightness(pixel);
                
                if (brightness <= blackThreshold)
                    blackPixels++;
                
                totalSamples++;
            }

            var actualConfidence = totalSamples > 0 ? (float)blackPixels / totalSamples : 0f;
            var isBlack = actualConfidence >= confidence;

            if (y < 5 || y >= bitmap.Height - 5) // Log first/last few rows for debugging
            {
                _logger.LogDebug("Row {Y}: {BlackPixels}/{TotalSamples} ({Confidence:P1}) >= {RequiredConfidence:P1} = {IsBlack}", 
                    y, blackPixels, totalSamples, actualConfidence, confidence, isBlack);
            }

            return isBlack;
        }

        // MARK: IsColumnMostlyBlack
        private bool IsColumnMostlyBlack(SKBitmap bitmap, int x, float blackThreshold, float confidence)
        {
            var height = bitmap.Height;
            var sampleStep = Math.Max(1, height / 100); // Sample more points for better accuracy
            int blackPixels = 0;
            int totalSamples = 0;

            // Also sample some pixels in the middle to avoid edge artifacts
            var startY = Math.Max(0, height / 10); // Skip first 10%
            var endY = Math.Min(height, height - height / 10); // Skip last 10%

            for (int y = startY; y < endY; y += sampleStep)
            {
                var pixel = bitmap.GetPixel(x, y);
                var brightness = CalculateBrightness(pixel);
                
                if (brightness <= blackThreshold)
                    blackPixels++;
                
                totalSamples++;
            }

            var actualConfidence = totalSamples > 0 ? (float)blackPixels / totalSamples : 0f;
            var isBlack = actualConfidence >= confidence;

            if (x < 5 || x >= bitmap.Width - 5) // Log first/last few columns for debugging
            {
                _logger.LogDebug("Column {X}: {BlackPixels}/{TotalSamples} ({Confidence:P1}) >= {RequiredConfidence:P1} = {IsBlack}", 
                    x, blackPixels, totalSamples, actualConfidence, confidence, isBlack);
            }

            return isBlack;
        }

        // MARK: CalculateBrightness
        private float CalculateBrightness(SKColor color)
        {
            // Use ITU-R BT.709 luma coefficients for better perceptual accuracy
            return (0.2126f * color.Red + 0.7152f * color.Green + 0.0722f * color.Blue) / 255.0f;
        }

        // MARK: ApplyPosterFill
        private SKBitmap ApplyPosterFill(SKBitmap source, float targetRatio, PosterFill fillMode)
        {
            if (fillMode == PosterFill.Original)
                return source;

            var currentRatio = (float)source.Width / source.Height;
            
            // Skip if ratios are close enough (within 1%)
            if (Math.Abs(currentRatio - targetRatio) < 0.01f)
                return source;

            int targetWidth, targetHeight;

            switch (fillMode)
            {
                case PosterFill.Fill:
                    // Scale to fill target ratio, cropping excess
                    if (currentRatio > targetRatio)
                    {
                        // Source is wider than target - crop width
                        targetHeight = source.Height;
                        targetWidth = (int)(targetHeight * targetRatio);
                    }
                    else
                    {
                        // Source is taller than target - crop height  
                        targetWidth = source.Width;
                        targetHeight = (int)(targetWidth / targetRatio);
                    }
                    break;

                case PosterFill.Fit:
                    // Scale to fit within target ratio, adding letterbox/pillarbox if needed
                    if (currentRatio > targetRatio)
                    {
                        // Source is wider - fit to width
                        targetWidth = source.Width;
                        targetHeight = (int)(targetWidth / targetRatio);
                    }
                    else
                    {
                        // Source is taller - fit to height
                        targetHeight = source.Height;
                        targetWidth = (int)(targetHeight * targetRatio);
                    }
                    break;

                default:
                    return source;
            }

            // Ensure dimensions are valid
            if (targetWidth <= 0 || targetHeight <= 0)
                return source;

            var result = new SKBitmap(targetWidth, targetHeight, source.ColorType, source.AlphaType);
            using var canvas = new SKCanvas(result);

            if (fillMode == PosterFill.Fill)
            {
                // Center the crop
                var srcX = Math.Max(0, (source.Width - targetWidth) / 2);
                var srcY = Math.Max(0, (source.Height - targetHeight) / 2);
                
                var sourceRect = new SKRect(srcX, srcY, srcX + targetWidth, srcY + targetHeight);
                var destRect = new SKRect(0, 0, targetWidth, targetHeight);
                
                canvas.DrawBitmap(source, sourceRect, destRect);
            }
            else // PosterFill.Fit
            {
                // Scale and center with letterboxing/pillarboxing
                var scaleX = (float)targetWidth / source.Width;
                var scaleY = (float)targetHeight / source.Height;
                var scale = Math.Min(scaleX, scaleY);
                
                var scaledWidth = source.Width * scale;
                var scaledHeight = source.Height * scale;
                
                var destX = (targetWidth - scaledWidth) / 2;
                var destY = (targetHeight - scaledHeight) / 2;
                
                var sourceRect = new SKRect(0, 0, source.Width, source.Height);
                var destRect = new SKRect(destX, destY, destX + scaledWidth, destY + scaledHeight);
                
                canvas.Clear(SKColors.Black); // Fill background with black
                canvas.DrawBitmap(source, sourceRect, destRect);
            }

            return result;
        }
    }
}