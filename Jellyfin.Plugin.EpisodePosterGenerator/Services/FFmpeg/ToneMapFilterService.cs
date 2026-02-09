using System;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Extensions;
using Jellyfin.Data.Enums;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Configuration;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    public class ToneMapFilterService
    {
        private readonly ILogger<ToneMapFilterService> _logger;

        public ToneMapFilterService(ILogger<ToneMapFilterService> logger)
        {
            _logger = logger;
        }

        // GetToneMapFilter
        // Returns the appropriate FFmpeg tone mapping filter chain for the video's HDR type and hardware.
        public static string GetToneMapFilter(
            EncodingOptions options,
            VideoMetadata video,
            HardwareAccelerationType hwAccel,
            ILogger logger)
        {
            if (!options.EnableTonemapping)
            {
                logger.LogDebug("Tone mapping is disabled in encoding options");
                return string.Empty;
            }

            // Infer HDR10 from video characteristics when VideoRangeType is Unknown
            if (video.VideoHdrType == VideoRangeType.Unknown && IsLikelyHDR(video))
            {
                logger.LogWarning("HDR detected by video characteristics (Unknown VideoRangeType but 10-bit/BT.2020), inferring HDR10");
                video.VideoHdrType = VideoRangeType.HDR10;
            }

            if (!RequiresToneMapping(video.VideoHdrType))
            {
                logger.LogDebug("VideoRangeType {RangeType} does not require tone mapping", video.VideoHdrType);
                return string.Empty;
            }

            var isDV = video.VideoHdrType.IsDolbyVision();

            logger.LogInformation("Applying tone mapping for {RangeType} content using {HwAccel} (DolbyVision={IsDV})",
                video.VideoHdrType, hwAccel, isDV);

            // VideoToolbox on macOS handles DV natively via its own tone mapping pipeline
            if (hwAccel == HardwareAccelerationType.videotoolbox)
            {
                return GetVideoToolboxToneMapFilter(options, video.VideoHdrType, logger);
            }

            // For Dolby Vision content on non-VT hardware or software:
            // Use libplacebo which reads DV RPU metadata for proper dynamic tone mapping.
            // Pure DV (Profile 5) has no HDR10 fallback — static PQ tone mapping produces
            // washed-out or dark results because the RPU curves are not applied.
            if (isDV)
            {
                return GetLibplaceboToneMapFilter(options, video.VideoHdrType, hwAccel, logger);
            }

            return hwAccel switch
            {
                HardwareAccelerationType.qsv => GetVppToneMapFilter(options, video.VideoHdrType, logger),
                _ => GetSoftwareToneMapFilter(options, video.VideoHdrType, logger)
            };
        }

        // RequiresToneMapping
        // Determines if a VideoRangeType needs HDR-to-SDR tone mapping.
        private static bool RequiresToneMapping(VideoRangeType rangeType)
        {
            return rangeType switch
            {
                // SDR content - no tone mapping
                VideoRangeType.SDR => false,
                VideoRangeType.Unknown => false,

                // Pure HDR - requires tone mapping
                VideoRangeType.HDR10 => true,
                VideoRangeType.HDR10Plus => true,
                VideoRangeType.HLG => true,

                // Dolby Vision variants - requires tone mapping
                VideoRangeType.DOVI => true,
                VideoRangeType.DOVIWithHDR10 => true,
                VideoRangeType.DOVIWithHDR10Plus => true,
                VideoRangeType.DOVIWithHLG => true,
                VideoRangeType.DOVIWithEL => true,
                VideoRangeType.DOVIWithELHDR10Plus => true,

                // DV with SDR fallback already has SDR layer
                VideoRangeType.DOVIWithSDR => false,

                // Invalid DV - treat as HDR10
                VideoRangeType.DOVIInvalid => true,

                _ => false
            };
        }

        // IsLikelyHDR
        // Detects HDR by checking bit depth and color space when VideoRangeType is unknown.
        private static bool IsLikelyHDR(VideoMetadata video)
        {
            return video.VideoColorBits >= 10 ||
                video.VideoColorSpace?.Contains("2020", StringComparison.OrdinalIgnoreCase) == true ||
                video.VideoColorSpace?.Contains("2100", StringComparison.OrdinalIgnoreCase) == true;
        }

        // GetVppToneMapFilter
        // Builds QSV/VAAPI hardware tone mapping filter using tonemap_vaapi.
        private static string GetVppToneMapFilter(EncodingOptions options, VideoRangeType rangeType, ILogger logger)
        {
            var transferFunction = GetTransferFunction(rangeType);
            logger.LogDebug("Using VPP tone mapping with transfer function: {Transfer}", transferFunction);

            // setparams sets input color characteristics, tonemap_vaapi converts to BT.709 SDR
            return $"setparams=color_primaries=bt2020:color_trc={transferFunction}:colorspace=bt2020nc," +
                   "tonemap_vaapi=format=nv12:p=bt709:t=bt709:m=bt709";
        }

        // GetVideoToolboxToneMapFilter
        // Builds Apple VideoToolbox tone mapping filter using scale_vt.
        private static string GetVideoToolboxToneMapFilter(EncodingOptions options, VideoRangeType rangeType, ILogger logger)
        {
            logger.LogDebug("Using VideoToolbox tone mapping for {RangeType}", rangeType);

            // scale_vt handles tone mapping and color space conversion on Apple hardware
            return "scale_vt=format=nv12:color_matrix=bt709:color_primaries=bt709:color_transfer=bt709";
        }

        // GetLibplaceboToneMapFilter
        // Builds libplacebo tone mapping filter for Dolby Vision content.
        // libplacebo reads DV RPU metadata for proper dynamic tone mapping, which is essential
        // for pure DV (Profile 5) content that has no HDR10 fallback layer.
        // For hardware-accelerated pipelines, hwdownload is prepended to bring GPU frames to CPU.
        private static string GetLibplaceboToneMapFilter(EncodingOptions options, VideoRangeType rangeType, HardwareAccelerationType hwAccel, ILogger logger)
        {
            logger.LogInformation("Using libplacebo tone mapping for Dolby Vision {RangeType}", rangeType);

            var prefix = string.Empty;

            // GPU frames must be downloaded to system memory before libplacebo can process them
            if (hwAccel != HardwareAccelerationType.none)
            {
                var downloadFormat = hwAccel == HardwareAccelerationType.videotoolbox ? "nv12" : "yuv420p";
                prefix = $"hwdownload,format={downloadFormat},";
            }

            // libplacebo handles DV RPU parsing, gamut mapping, and tone mapping in one filter.
            // colorspace=bt709 and color_primaries=bt709 convert to SDR color space.
            // color_trc=bt709 sets the output transfer function to SDR gamma.
            // tonemapping=bt2390 is a good default that preserves detail in highlights and shadows.
            return prefix +
                   "libplacebo=tonemapping=bt2390:colorspace=bt709:color_primaries=bt709:color_trc=bt709:format=yuv420p";
        }

        // GetSoftwareToneMapFilter
        // Builds software tone mapping filter chain using zscale and tonemapx.
        // Uses GetTransferFunction to select the correct transfer for HLG vs PQ-based formats.
        private static string GetSoftwareToneMapFilter(EncodingOptions options, VideoRangeType rangeType, ILogger logger)
        {
            var algorithm = GetToneMappingAlgorithm(options);
            var npl = options.TonemappingPeak > 0 ? options.TonemappingPeak : 100;
            var transferFunction = GetTransferFunction(rangeType);

            logger.LogDebug("Using software tone mapping for {RangeType} with transfer={Transfer}", rangeType, transferFunction);

            // zscale chain: convert color space, then tonemapx applies the tone curve
            // tin=transfer input, pin=primaries input, min=matrix input
            // t=linear converts to linear light for proper tone mapping math
            return $"zscale=tin={transferFunction}:pin=bt2020:min=bt2020nc:t=linear:npl={npl}," +
                   $"format=gbrpf32le,zscale=p=bt709," +
                   $"tonemapx=tonemap={algorithm}:desat=0:peak={npl}:t=bt709:m=bt709:p=bt709:format=yuv420p";
        }

        // GetTransferFunction
        // Returns the FFmpeg transfer function name for the HDR type.
        private static string GetTransferFunction(VideoRangeType rangeType)
        {
            return rangeType switch
            {
                VideoRangeType.HLG or VideoRangeType.DOVIWithHLG => "arib-std-b67",
                _ => "smpte2084"
            };
        }

        // GetToneMappingAlgorithm
        // Converts Jellyfin's TonemappingAlgorithm enum to FFmpeg filter name.
        private static string GetToneMappingAlgorithm(EncodingOptions options)
        {
            return options.TonemappingAlgorithm switch
            {
                TonemappingAlgorithm.hable => "hable",
                TonemappingAlgorithm.reinhard => "reinhard",
                TonemappingAlgorithm.mobius => "mobius",
                TonemappingAlgorithm.bt2390 => "bt2390",
                _ => "hable"
            };
        }
    }
}
