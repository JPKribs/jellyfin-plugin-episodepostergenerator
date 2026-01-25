using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils
{
    // RenderConstants
    // Centralized constants for consistent rendering across all poster types.
    public static class RenderConstants
    {
        // Text rendering constants
        public const float LineHeightMultiplier = 1.2f;
        public const float ShadowOffset = 2f;
        public const float ShadowBlurSigma = 1.5f;
        public const byte ShadowAlpha = 180;
        public const float TextWidthMultiplier = 0.9f;

        // Spacing constants
        public const float DefaultSpacingRatio = 0.02f;

        // Separator line constants
        public const float SeparatorStrokeWidth = 2f;
        public const float SeparatorLineHeight = 4f;

        // Shadow color (black with standard alpha)
        public static SKColor ShadowColor => SKColors.Black.WithAlpha(ShadowAlpha);
    }
}
