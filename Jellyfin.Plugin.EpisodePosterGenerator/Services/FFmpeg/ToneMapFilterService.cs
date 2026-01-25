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

            logger.LogInformation("Applying tone mapping for {RangeType} content using {HwAccel}",
                video.VideoHdrType, hwAccel);

            return hwAccel switch
            {
                HardwareAccelerationType.qsv => GetVppToneMapFilter(options, video.VideoHdrType, logger),
                HardwareAccelerationType.videotoolbox => GetVideoToolboxToneMapFilter(options, video.VideoHdrType, logger),
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

        // GetSoftwareToneMapFilter
        // Builds software tone mapping filter chain using zscale and tonemapx.
        private static string GetSoftwareToneMapFilter(EncodingOptions options, VideoRangeType rangeType, ILogger logger)
        {
            var algorithm = GetToneMappingAlgorithm(options);
            var npl = options.TonemappingPeak > 0 ? options.TonemappingPeak : 100;

            // zscale chain: convert color space, then tonemapx applies the tone curve
            // tin=transfer input, pin=primaries input, min=matrix input
            // t=linear converts to linear light for proper tone mapping math

            if (IsDolbyVision(rangeType))
            {
                logger.LogDebug("Using Dolby Vision-aware tone mapping with tonemapx for {RangeType}", rangeType);

                // DV base layer is HDR10-compatible, explicitly set PQ transfer
                return $"zscale=tin=smpte2084:pin=bt2020:min=bt2020nc:t=linear:npl={npl}," +
                       $"format=gbrpf32le,zscale=p=bt709," +
                       $"tonemapx=tonemap={algorithm}:desat=0:peak={npl}:t=bt709:m=bt709:p=bt709:format=yuv420p";
            }
            else if (rangeType == VideoRangeType.HLG)
            {
                logger.LogDebug("Using HLG-specific tone mapping");

                // HLG uses arib-std-b67 transfer function instead of smpte2084 (PQ)
                return $"zscale=tin=arib-std-b67:pin=bt2020:min=bt2020nc:t=linear:npl={npl}," +
                       $"format=gbrpf32le,zscale=p=bt709," +
                       $"tonemapx=tonemap={algorithm}:desat=0:peak={npl}:t=bt709:m=bt709:p=bt709:format=yuv420p";
            }
            else
            {
                logger.LogDebug("Using standard HDR10 tone mapping with zscale chain for {RangeType}", rangeType);

                // Standard HDR10/HDR10+ uses PQ (smpte2084) transfer
                return $"zscale=tin=smpte2084:pin=bt2020:min=bt2020nc:t=linear:npl={npl}," +
                       $"format=gbrpf32le,zscale=p=bt709," +
                       $"tonemapx=tonemap={algorithm}:desat=0:peak={npl}:t=bt709:m=bt709:p=bt709:format=yuv420p";
            }
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

        // IsDolbyVision
        // Checks if the VideoRangeType is any Dolby Vision variant.
        private static bool IsDolbyVision(VideoRangeType rangeType)
        {
            return rangeType switch
            {
                VideoRangeType.DOVI or
                VideoRangeType.DOVIWithHDR10 or
                VideoRangeType.DOVIWithHDR10Plus or
                VideoRangeType.DOVIWithHLG or
                VideoRangeType.DOVIWithEL or
                VideoRangeType.DOVIWithELHDR10Plus or
                VideoRangeType.DOVIInvalid => true,
                _ => false
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
