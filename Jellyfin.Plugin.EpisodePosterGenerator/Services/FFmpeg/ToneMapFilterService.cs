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
            if (!options.EnableTonemapping || !video.VideoHdrType.IsHDR())
                return string.Empty;

            if (video.VideoHdrType == VideoRangeType.Unknown && IsLikelyHDR(video))
            {
                logger.LogWarning("HDR detected by video characteristics, applying tone mapping");
                video.VideoHdrType = VideoRangeType.HDR10;
            }

            return hwAccel switch
            {
                HardwareAccelerationType.qsv => GetVppToneMapFilter(options),
                HardwareAccelerationType.videotoolbox => GetVideoToolboxToneMapFilter(options),
                _ => GetSoftwareToneMapFilter(options)
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
        private static string GetVppToneMapFilter(EncodingOptions options)
        {
            return "setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc,tonemap_vaapi=format=nv12:p=bt709:t=bt709:m=bt709";
        }

        // MARK: GetVideoToolboxToneMapFilter
        private static string GetVideoToolboxToneMapFilter(EncodingOptions options)
        {
            return "scale_vt=format=nv12:color_matrix=bt709:color_primaries=bt709:color_transfer=bt709";
        }

        // MARK: GetSoftwareToneMapFilter
        private static string GetSoftwareToneMapFilter(EncodingOptions options)
        {
            var algorithm = GetToneMappingAlgorithm(options);
            var npl = options.TonemappingPeak > 0 ? options.TonemappingPeak : 100;
            
            // Use tonemapx (Jellyfin's custom filter) instead of tonemap
            return $"setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc," +
                $"tonemapx=tonemap={algorithm}:desat=0:peak={npl}:t=bt709:m=bt709:p=bt709:format=yuv420p";
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