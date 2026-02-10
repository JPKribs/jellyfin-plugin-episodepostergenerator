using System;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Applies brightness adjustments to bitmaps, primarily for boosting
    /// HDR content that appears dim after tone mapping.
    /// </summary>
    public class BrightnessService
    {
        private readonly ILogger<BrightnessService> _logger;

        public BrightnessService(ILogger<BrightnessService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Increases the brightness of a bitmap in-place by the specified percentage.
        /// </summary>
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
                    var p = pixels[i];
                    pixels[i] = new SKColor(
                        (byte)Math.Min(255, (int)(p.Red * multiplier)),
                        (byte)Math.Min(255, (int)(p.Green * multiplier)),
                        (byte)Math.Min(255, (int)(p.Blue * multiplier)),
                        p.Alpha);
                }

                bitmap.Pixels = pixels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to brighten bitmap");
            }
        }
    }
}
