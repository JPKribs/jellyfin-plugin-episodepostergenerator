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
            HardwareAccelerationType hwAccel)
        {
            if (!options.EnableTonemapping || !video.VideoHdrType.IsHDR())
                return string.Empty;

            return hwAccel switch
            {
                HardwareAccelerationType.videotoolbox 
                    => GetSoftwareToneMapFilter(options),
                
                HardwareAccelerationType.qsv when options.EnableVppTonemapping 
                    => GetVppToneMapFilter(options),
                
                HardwareAccelerationType.nvenc or HardwareAccelerationType.amf 
                    => GetOpenCLToneMapFilter(options),
                
                HardwareAccelerationType.vaapi 
                    => GetVaapiToneMapFilter(options),
                
                HardwareAccelerationType.v4l2m2m or HardwareAccelerationType.rkmpp or HardwareAccelerationType.none or _
                    => GetSoftwareToneMapFilter(options)
            };
        }

        // MARK: GetVppToneMapFilter
        private static string GetVppToneMapFilter(EncodingOptions options)
        {
            var algorithm = GetToneMappingAlgorithm(options);
            return $"scale_qsv=format=nv12,vpp_qsv=tonemap=1:tonemap_mode={algorithm}";
        }

        // MARK: GetOpenCLToneMapFilter
        private static string GetOpenCLToneMapFilter(EncodingOptions options)
        {
            var algorithm = GetToneMappingAlgorithm(options);
            var peak = options.TonemappingPeak > 0 ? options.TonemappingPeak : 100;
            return $"hwupload,tonemap_opencl=tonemap={algorithm}:peak={peak}:desat=0,hwdownload,format=nv12";
        }

        // MARK: GetVaapiToneMapFilter
        private static string GetVaapiToneMapFilter(EncodingOptions options)
        {
            var algorithm = GetToneMappingAlgorithm(options);
            var peak = options.TonemappingPeak > 0 ? options.TonemappingPeak : 100;
            return $"scale_vaapi=format=p010,tonemap_vaapi=tonemap={algorithm}:peak={peak}:desat=0,scale_vaapi=format=nv12";
        }

        // MARK: GetSoftwareToneMapFilter
        private static string GetSoftwareToneMapFilter(EncodingOptions options)
        {
            var algorithm = GetToneMappingAlgorithm(options);
            double npl = options.TonemappingPeak > 0 ? options.TonemappingPeak : 100;
            
            // Return clean filter string without -vf prefix and without extra quotes
            return $"zscale=transfer=linear:primaries=bt2020:matrix=bt2020nc:range=pc,tonemap=tonemap={algorithm}:peak={npl}:desat=0,zscale=transfer=bt709:primaries=bt709:matrix=bt709:range=pc,format=yuv420p";
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