using System;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Data.Enums;
using Microsoft.Extensions.Logging;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Configuration;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Builds a safe FFmpeg tone mapping filter depending on HDR type and HW/SW extraction.
    /// </summary>
    public class ToneMapFilterService
    {
        /// <summary>Logger for this service</summary>
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
            // Skip tone mapping if disabled or HDR type unknown
            if (!options.EnableTonemapping || video.VideoHdrType == VideoRangeType.Unknown)
                return string.Empty;

            // Skip tone mapping if hardware can already handle it
            if (hwAccel != HardwareAccelerationType.none &&
                hwAccel != HardwareAccelerationType.videotoolbox)
            {
                return string.Empty;
            }

            // Choose algorithm string
            var algorithm = options.TonemappingAlgorithm switch
            {
                TonemappingAlgorithm.hable => "hable",
                TonemappingAlgorithm.reinhard => "reinhard",
                TonemappingAlgorithm.mobius => "mobius",
                TonemappingAlgorithm.bt2390 => "bt2390",
                _ => "hable"
            };

            // Set nominal peak luminance
            double npl = options.TonemappingPeak > 0 ? options.TonemappingPeak : 100;

            // Return FFmpeg filter string
            return $"-vf \"zscale=t=linear:npl={npl},tonemap={algorithm},format=yuv420p\"";
        }
    }
}