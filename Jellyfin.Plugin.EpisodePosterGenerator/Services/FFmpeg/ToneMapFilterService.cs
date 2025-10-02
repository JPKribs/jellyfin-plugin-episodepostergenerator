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

            return GetSoftwareToneMapFilter(options);
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
            return "vpp_qsv=tonemap=1,scale_qsv=format=nv12";
        }

        // MARK: GetNvencToneMapFilter
        private static string GetNvencToneMapFilter(EncodingOptions options)
        {
            var algorithm = GetToneMappingAlgorithm(options);
            var peak = options.TonemappingPeak > 0 ? options.TonemappingPeak : 100;

            return "format=p010,hwupload," +
                   $"tonemap_opencl=tonemap={algorithm}:format=nv12:" +
                   $"primaries=bt709:transfer=bt709:matrix=bt709:" +
                   $"peak={peak}:desat=0," +
                   "hwdownload,format=nv12";
        }

        // MARK: GetOpenCLToneMapFilter
        private static string GetOpenCLToneMapFilter(EncodingOptions options)
        {
            var algorithm = GetToneMappingAlgorithm(options);
            var peak = options.TonemappingPeak > 0 ? options.TonemappingPeak : 100;

            return $"hwmap,tonemap_opencl=tonemap={algorithm}:format=nv12:" +
                $"primaries=bt709:transfer=bt709:matrix=bt709:" +
                $"peak={peak}:desat=0:threshold=0.8," +
                "hwmap=derive_device=qsv:reverse=1";
        }

        // MARK: GetVaapiToneMapFilter
        private static string GetVaapiToneMapFilter(EncodingOptions options)
        {
            return "tonemap_vaapi=format=nv12:primaries=bt709:transfer=bt709";
        }

        // MARK: GetSoftwareToneMapFilter
        private static string GetSoftwareToneMapFilter(EncodingOptions options)
        {
            var algorithm = GetToneMappingAlgorithm(options);
            var npl = options.TonemappingPeak > 0 ? options.TonemappingPeak : 100;
            
            return $"zscale=t=linear:npl={npl}," +
                "format=gbrpf32le," +
                "zscale=p=bt709," +
                $"tonemap=tonemap={algorithm}:desat=0," +
                "zscale=t=bt709:m=bt709:r=tv," +
                "format=yuv420p";
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