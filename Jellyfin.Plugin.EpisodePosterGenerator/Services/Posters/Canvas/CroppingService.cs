using System;
using SkiaSharp;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    // CroppingService
    // Handles cropping and resizing of extracted episode frames for posters.
    public class CroppingService
    {
        private readonly ILogger<CroppingService> _logger;

        // CroppingService
        // Initializes a new instance with the logger dependency.
        public CroppingService(ILogger<CroppingService> logger)
        {
            _logger = logger;
        }

        // CropPoster
        // Crops a source bitmap by removing letterbox/pillarbox and applying poster fill settings.
        public SKBitmap CropPoster(SKBitmap source, VideoMetadata metadata, PosterSettings settings)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentNullException.ThrowIfNull(settings);

            var result = source;
            var originalSize = $"{source.Width}x{source.Height}";

            // Branch: Apply letterbox detection if enabled
            if (settings.EnableLetterboxDetection)
            {
                var cropped = RemoveLetterboxAndPillarbox(result, settings);
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

            // Branch: Apply poster fill unless Original mode is selected
            if (settings.PosterFill != PosterFill.Original)
            {
                var posterRatio = ParseAspectRatio(settings.PosterDimensionRatio);
                var filled = ApplyPosterFill(result, posterRatio, settings.PosterFill);

                if (filled != result)
                {
                    _logger.LogInformation("Poster fill applied: {Fill} ratio {Ratio}",
                        settings.PosterFill, settings.PosterDimensionRatio);

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

        // ParseAspectRatio
        // Parses an aspect ratio string like "16:9" into a float value.
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

        // RemoveLetterboxAndPillarbox
        // Detects and removes black bars from the edges of an image using pixel analysis.
        private SKBitmap RemoveLetterboxAndPillarbox(SKBitmap source, PosterSettings settings)
        {
            var blackThreshold = settings.LetterboxBlackThreshold;
            var confidence = settings.LetterboxConfidence / 100.0f;

            _logger.LogDebug("Letterbox detection: threshold={Threshold}, confidence={Confidence:F3}",
                blackThreshold, confidence);

            // Detect black bars on all four edges by scanning rows/columns for black pixel ratio
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

            // Skip cropping if no significant bars detected or invalid dimensions
            if (newWidth <= 0 || newHeight <= 0 ||
                newWidth > source.Width || newHeight > source.Height ||
                (left == 0 && right == 0 && top == 0 && bottom == 0))
            {
                _logger.LogDebug("No significant letterbox/pillarbox detected");
                return source;
            }

            // Safety check: prevent overly aggressive cropping (minimum 25% of original)
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

            using var paint = new SKPaint
            {
                FilterQuality = SKFilterQuality.High,
                IsAntialias = true
            };

            canvas.DrawBitmap(source, sourceRect, destRect, paint);

            return cropped;
        }

        // DetectTopLetterbox
        // Scans from the top of the image to find where content begins.
        private int DetectTopLetterbox(SKBitmap bitmap, int blackThreshold, float confidence)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;

            // Scan each row from top, counting black pixels until a row has enough non-black pixels
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

        // DetectBottomLetterbox
        // Scans from the bottom of the image to find where content ends.
        private int DetectBottomLetterbox(SKBitmap bitmap, int blackThreshold, float confidence)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;

            // Scan each row from bottom, counting black pixels until a row has enough non-black pixels
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

        // DetectLeftPillarbox
        // Scans from the left of the image to find where content begins.
        private int DetectLeftPillarbox(SKBitmap bitmap, int blackThreshold, float confidence)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;

            // Scan each column from left, counting black pixels until a column has enough non-black pixels
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

        // DetectRightPillarbox
        // Scans from the right of the image to find where content ends.
        private int DetectRightPillarbox(SKBitmap bitmap, int blackThreshold, float confidence)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;

            // Scan each column from right, counting black pixels until a column has enough non-black pixels
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

        // IsBlackPixel
        // Determines if a pixel is considered black based on a brightness threshold.
        private bool IsBlackPixel(SKColor pixel, int threshold)
        {
            var brightness = (pixel.Red + pixel.Green + pixel.Blue) / 3;
            return brightness <= threshold;
        }

        // ApplyPosterFill
        // Adjusts the image dimensions to match the target aspect ratio using the specified fill mode.
        private SKBitmap ApplyPosterFill(SKBitmap source, float targetRatio, PosterFill fillMode)
        {
            var currentRatio = (float)source.Width / source.Height;

            if (Math.Abs(currentRatio - targetRatio) < 0.01f)
                return source;

            int targetWidth, targetHeight;

            switch (fillMode)
            {
                // Branch: Fit mode crops to target ratio (zoom effect)
                case PosterFill.Fit:
                    if (currentRatio > targetRatio)
                    {
                        // Source wider than target - crop width
                        targetHeight = source.Height;
                        targetWidth = (int)(targetHeight * targetRatio);
                    }
                    else
                    {
                        // Source taller than target - crop height
                        targetWidth = source.Width;
                        targetHeight = (int)(targetWidth / targetRatio);
                    }
                    break;

                // Branch: Fill mode stretches to target ratio (may distort)
                case PosterFill.Fill:
                    var posterDimensions = CalculateTargetDimensions(source.Width, source.Height, targetRatio);
                    targetWidth = posterDimensions.width;
                    targetHeight = posterDimensions.height;
                    break;

                case PosterFill.Original:
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
                var srcX = Math.Max(0, (source.Width - targetWidth) / 2);
                var srcY = Math.Max(0, (source.Height - targetHeight) / 2);

                var sourceRect = new SKRect(srcX, srcY, srcX + targetWidth, srcY + targetHeight);
                var destRect = new SKRect(0, 0, targetWidth, targetHeight);

                using var paint = new SKPaint
                {
                    FilterQuality = SKFilterQuality.High,
                    IsAntialias = true
                };

                canvas.DrawBitmap(source, sourceRect, destRect, paint);
            }
            else if (fillMode == PosterFill.Fill)
            {
                var sourceRect = new SKRect(0, 0, source.Width, source.Height);
                var destRect = new SKRect(0, 0, targetWidth, targetHeight);

                using var paint = new SKPaint
                {
                    FilterQuality = SKFilterQuality.High,
                    IsAntialias = true
                };

                canvas.DrawBitmap(source, sourceRect, destRect, paint);
            }

            return result;
        }

        // CalculateTargetDimensions
        // Calculates target width and height based on source dimensions and target aspect ratio.
        private (int width, int height) CalculateTargetDimensions(int sourceWidth, int sourceHeight, float targetRatio)
        {
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
