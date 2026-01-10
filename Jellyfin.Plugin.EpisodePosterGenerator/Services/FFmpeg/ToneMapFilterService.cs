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

        // MARK: Constructor
        public ToneMapFilterService(ILogger<ToneMapFilterService> logger)
        {
            _logger = logger;
        }

        // MARK: GetToneMapFilter
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

            // Detect HDR if unknown but characteristics suggest HDR
            if (video.VideoHdrType == VideoRangeType.Unknown && IsLikelyHDR(video))
            {
                logger.LogWarning("HDR detected by video characteristics (Unknown VideoRangeType but 10-bit/BT.2020), inferring HDR10");
                video.VideoHdrType = VideoRangeType.HDR10;
            }

            // Check if tone mapping is needed based on VideoRangeType
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

        // MARK: RequiresToneMapping
        private static bool RequiresToneMapping(VideoRangeType rangeType)
        {
            return rangeType switch
            {
                // SDR content - NO tone mapping needed
                VideoRangeType.SDR => false,
                VideoRangeType.Unknown => false,

                // Pure HDR content - tone mapping REQUIRED
                VideoRangeType.HDR10 => true,
                VideoRangeType.HDR10Plus => true,
                VideoRangeType.HLG => true,

                // Dolby Vision variants - tone mapping REQUIRED
                VideoRangeType.DOVI => true,
                VideoRangeType.DOVIWithHDR10 => true,
                VideoRangeType.DOVIWithHDR10Plus => true,
                VideoRangeType.DOVIWithHLG => true,
                VideoRangeType.DOVIWithEL => true,
                VideoRangeType.DOVIWithELHDR10Plus => true,

                // Dolby Vision with SDR fallback - NO tone mapping needed (already has SDR layer)
                VideoRangeType.DOVIWithSDR => false,

                // Invalid DV configuration - treat as HDR10, tone mapping REQUIRED
                VideoRangeType.DOVIInvalid => true,

                _ => false
            };
        }

        // MARK: IsLikelyHDR
        private static bool IsLikelyHDR(VideoMetadata video)
        {
            return video.VideoColorBits >= 10 || 
                video.VideoColorSpace?.Contains("2020", StringComparison.OrdinalIgnoreCase) == true ||
                video.VideoColorSpace?.Contains("2100", StringComparison.OrdinalIgnoreCase) == true;
        }

        // MARK: GetVppToneMapFilter
        private static string GetVppToneMapFilter(EncodingOptions options, VideoRangeType rangeType, ILogger logger)
        {
            // QSV/VAAPI hardware tone mapping
            var transferFunction = GetTransferFunction(rangeType);
            logger.LogDebug("Using VPP tone mapping with transfer function: {Transfer}", transferFunction);

            return $"setparams=color_primaries=bt2020:color_trc={transferFunction}:colorspace=bt2020nc," +
                   "tonemap_vaapi=format=nv12:p=bt709:t=bt709:m=bt709";
        }

        // MARK: GetVideoToolboxToneMapFilter
        private static string GetVideoToolboxToneMapFilter(EncodingOptions options, VideoRangeType rangeType, ILogger logger)
        {
            // VideoToolbox handles tone mapping through scale_vt
            logger.LogDebug("Using VideoToolbox tone mapping for {RangeType}", rangeType);

            return "scale_vt=format=nv12:color_matrix=bt709:color_primaries=bt709:color_transfer=bt709";
        }

        // MARK: GetSoftwareToneMapFilter
        private static string GetSoftwareToneMapFilter(EncodingOptions options, VideoRangeType rangeType, ILogger logger)
        {
            var algorithm = GetToneMappingAlgorithm(options);
            var npl = options.TonemappingPeak > 0 ? options.TonemappingPeak : 100;

            // Use proper zscale conversion chain before tonemapx (matches Jellyfin mainline)
            // This is critical for proper color space conversion from HDR to SDR

            // For Dolby Vision content, we need to explicitly set input parameters
            if (IsDolbyVision(rangeType))
            {
                logger.LogDebug("Using Dolby Vision-aware tone mapping with tonemapx for {RangeType}", rangeType);

                // For DV content, we need to explicitly specify input color parameters
                // to ensure proper interpretation of the BL (base layer) which is HDR10-compatible
                // tin=smpte2084 explicitly sets input transfer to PQ
                // pin=bt2020 explicitly sets input primaries
                // min=bt2020nc explicitly sets input matrix
                return $"zscale=tin=smpte2084:pin=bt2020:min=bt2020nc:t=linear:npl={npl}," +
                       $"format=gbrpf32le,zscale=p=bt709," +
                       $"tonemapx=tonemap={algorithm}:desat=0:peak={npl}:t=bt709:m=bt709:p=bt709:format=yuv420p";
            }
            else if (rangeType == VideoRangeType.HLG)
            {
                // HLG uses different transfer function (arib-std-b67)
                logger.LogDebug("Using HLG-specific tone mapping");

                return $"zscale=tin=arib-std-b67:pin=bt2020:min=bt2020nc:t=linear:npl={npl}," +
                       $"format=gbrpf32le,zscale=p=bt709," +
                       $"tonemapx=tonemap={algorithm}:desat=0:peak={npl}:t=bt709:m=bt709:p=bt709:format=yuv420p";
            }
            else
            {
                // Standard HDR10/HDR10+ tone mapping
                logger.LogDebug("Using standard HDR10 tone mapping with zscale chain for {RangeType}", rangeType);

                return $"zscale=tin=smpte2084:pin=bt2020:min=bt2020nc:t=linear:npl={npl}," +
                       $"format=gbrpf32le,zscale=p=bt709," +
                       $"tonemapx=tonemap={algorithm}:desat=0:peak={npl}:t=bt709:m=bt709:p=bt709:format=yuv420p";
            }
        }

        // MARK: GetTransferFunction
        private static string GetTransferFunction(VideoRangeType rangeType)
        {
            return rangeType switch
            {
                VideoRangeType.HLG or VideoRangeType.DOVIWithHLG => "arib-std-b67",
                _ => "smpte2084"
            };
        }

        // MARK: IsDolbyVision
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

        // MARK: GetToneMappingAlgorithm
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