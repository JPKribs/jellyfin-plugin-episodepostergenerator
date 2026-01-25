using System;
using Jellyfin.Data.Enums;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    public static class VideoRangeTypeExtensions
    {
        // IsHDR
        // Determines if the video range type represents HDR content.
        public static bool IsHDR(this VideoRangeType videoRangeType)
        {
            return videoRangeType switch
            {
                VideoRangeType.SDR or
                VideoRangeType.Unknown => false,
                _ => true
            };
        }

        // IsDolbyVision
        // Determines if the video range type is a Dolby Vision variant.
        public static bool IsDolbyVision(this VideoRangeType videoRangeType)
        {
            return videoRangeType switch
            {
                VideoRangeType.DOVI or
                VideoRangeType.DOVIWithHDR10 or
                VideoRangeType.DOVIWithHLG or
                VideoRangeType.DOVIWithEL or
                VideoRangeType.DOVIWithELHDR10Plus or
                VideoRangeType.DOVIWithHDR10Plus or
                VideoRangeType.DOVIInvalid => true,
                _ => false
            };
        }

        // FromColorTransferAndPrimaries
        // Infers the video range type from color transfer and primaries metadata.
        public static VideoRangeType FromColorTransferAndPrimaries(string? colorTransfer, string? colorPrimaries)
        {
            if (string.IsNullOrEmpty(colorTransfer) && string.IsNullOrEmpty(colorPrimaries))
                return VideoRangeType.Unknown;

            var transfer = colorTransfer?.ToLowerInvariant() ?? string.Empty;
            var primaries = colorPrimaries?.ToLowerInvariant() ?? string.Empty;

            if (transfer.Contains("dovi", StringComparison.OrdinalIgnoreCase) || primaries.Contains("dovi", StringComparison.OrdinalIgnoreCase))
                return VideoRangeType.DOVI;

            if (transfer.Contains("smpte428", StringComparison.OrdinalIgnoreCase) || transfer.Contains("hdr10+", StringComparison.OrdinalIgnoreCase))
                return VideoRangeType.HDR10Plus;

            if (transfer.Contains("smpte2084", StringComparison.OrdinalIgnoreCase) || transfer.Contains("pq", StringComparison.OrdinalIgnoreCase) || primaries.Contains("bt2020", StringComparison.OrdinalIgnoreCase))
                return VideoRangeType.HDR10;

            if (transfer.Contains("arib-std-b67", StringComparison.OrdinalIgnoreCase) || transfer.Contains("hlg", StringComparison.OrdinalIgnoreCase))
                return VideoRangeType.HLG;

            return VideoRangeType.SDR;
        }
    }
}
