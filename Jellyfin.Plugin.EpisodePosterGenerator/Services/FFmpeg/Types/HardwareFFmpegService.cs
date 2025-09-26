using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Configuration;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Extensions;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    public class HardwareFFmpegService : IFFmpegService
    {
        private readonly ILogger<HardwareFFmpegService> _logger;

        // MARK: Constructor
        public HardwareFFmpegService(ILogger<HardwareFFmpegService> logger)
        {
            _logger = logger;
        }

        // MARK: BuildFFmpegArgs
        public string? BuildFFmpegArgs(
            string outputPath,
            EpisodeMetadata metadata,
            EncodingOptions encodingOptions,
            int? seekSeconds = null,
            bool skipToneMapping = false)
        {
            var video = metadata.VideoMetadata;
            if (video?.EpisodeFilePath == null) return null;

            var inputPath = video.EpisodeFilePath;
            var durationSeconds = video.VideoLengthTicks / (double)TimeSpan.TicksPerSecond;
            if (durationSeconds <= 0) durationSeconds = 3600;

            var actualSeekSeconds = seekSeconds ?? new Random().Next((int)(durationSeconds * 0.2), (int)(durationSeconds * 0.8));
            var hwAccel = encodingOptions.HardwareAccelerationType;
            var isHDR = video.VideoHdrType.IsHDR();

            var args = "-y";

            // Hardware device initialization comes first
            var hwInitArgs = GetHardwareInitArgs(hwAccel);
            if (!string.IsNullOrEmpty(hwInitArgs))
            {
                args += $" {hwInitArgs}";
            }

            // Hardware acceleration args
            var hwAccelArgs = GetHardwareAccelArgs(hwAccel);
            if (!string.IsNullOrEmpty(hwAccelArgs))
            {
                args += $" {hwAccelArgs}";
            }

            // Input specification
            args += $" -ss {actualSeekSeconds} -i \"{inputPath}\"";

            // Video filter chain using ToneMapFilterService
            if (!skipToneMapping && encodingOptions.EnableTonemapping && isHDR)
            {
                var filterChain = ToneMapFilterService.GetToneMapFilter(encodingOptions, video, hwAccel);
                if (!string.IsNullOrEmpty(filterChain))
                {
                    args += $" -vf {filterChain}";
                    _logger.LogDebug("Applied hardware tone mapping filter for HDR content");
                }
                else
                {
                    _logger.LogWarning("Hardware tone mapping filter was empty for HDR content");
                }
            }

            // Output encoding parameters
            args += GetHardwareEncodingArgs(hwAccel);
            args += $" -frames:v 1 -q:v 2 \"{outputPath}\"";

            return args;
        }

        // MARK: GetHardwareInitArgs
        private string GetHardwareInitArgs(HardwareAccelerationType hwAccel)
        {
            return hwAccel switch
            {
                HardwareAccelerationType.qsv => "-init_hw_device qsv=hw",
                HardwareAccelerationType.nvenc => "-init_hw_device cuda=cu:0",
                HardwareAccelerationType.amf => "-init_hw_device opencl=ocl",
                HardwareAccelerationType.vaapi => "-init_hw_device vaapi=va:/dev/dri/renderD128",
                HardwareAccelerationType.videotoolbox => "-init_hw_device videotoolbox=vt",
                _ => string.Empty
            };
        }

        // MARK: GetHardwareAccelArgs
        private string GetHardwareAccelArgs(HardwareAccelerationType hwAccel)
        {
            return hwAccel switch
            {
                HardwareAccelerationType.qsv => "-hwaccel qsv -hwaccel_output_format qsv",
                HardwareAccelerationType.nvenc => "-hwaccel cuda -hwaccel_output_format cuda",
                HardwareAccelerationType.amf => "-hwaccel vaapi -hwaccel_output_format vaapi",
                HardwareAccelerationType.vaapi => "-hwaccel vaapi -hwaccel_output_format vaapi",
                HardwareAccelerationType.videotoolbox => "-hwaccel videotoolbox -hwaccel_output_format videotoolbox_vld",
                _ => string.Empty
            };
        }

        // MARK: GetHardwareEncodingArgs
        private string GetHardwareEncodingArgs(HardwareAccelerationType hwAccel)
        {
            return hwAccel switch
            {
                HardwareAccelerationType.qsv => " -c:v mjpeg_qsv",
                HardwareAccelerationType.nvenc => " -c:v mjpeg",
                HardwareAccelerationType.amf => " -c:v mjpeg",
                HardwareAccelerationType.vaapi => " -c:v mjpeg_vaapi",
                HardwareAccelerationType.videotoolbox => " -c:v mjpeg",
                _ => string.Empty
            };
        }

        // MARK: CanProcess
        public bool CanProcess(EpisodeMetadata metadata, EncodingOptions encodingOptions)
        {
            var video = metadata.VideoMetadata;
            var hwAccel = encodingOptions.HardwareAccelerationType;

            if (hwAccel == HardwareAccelerationType.none) return false;

            var codecSupported = encodingOptions.HardwareDecodingCodecs
                .Any(c => string.Equals(c, video.VideoCodec.ToString(), StringComparison.OrdinalIgnoreCase));

            if (!codecSupported) return false;

            if (video.VideoHdrType.IsHDR() && encodingOptions.EnableTonemapping)
            {
                return hwAccel switch
                {
                    HardwareAccelerationType.qsv => encodingOptions.EnableVppTonemapping,
                    HardwareAccelerationType.nvenc => true,
                    HardwareAccelerationType.amf => true,
                    HardwareAccelerationType.vaapi => true,
                    HardwareAccelerationType.videotoolbox => encodingOptions.EnableVideoToolboxTonemapping,
                    _ => false
                };
            }

            return true;
        }

        // MARK: ExtractSceneAsync
        public Task<string?> ExtractSceneAsync(
            string outputPath,
            EpisodeMetadata metadata,
            EncodingOptions encodingOptions,
            CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("HardwareFFmpegService does not execute FFmpeg directly. Use BuildFFmpegArgs + FFmpegService.RunFFmpegAsync.");
            return Task.FromResult<string?>(null);
        }
    }
}