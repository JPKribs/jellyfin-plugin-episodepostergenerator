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
        private readonly HardwareValidationService _validationService;

        // HardwareFFmpegService
        // Initializes the hardware FFmpeg service with required dependencies.
        public HardwareFFmpegService(
            ILogger<HardwareFFmpegService> logger,
            HardwareValidationService validationService)
        {
            _logger = logger;
            _validationService = validationService;
        }

        // BuildFFmpegArgs
        // Constructs FFmpeg command-line arguments for hardware-accelerated frame extraction.
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

            var hwInitArgs = GetHardwareInitArgs(hwAccel);
            if (!string.IsNullOrEmpty(hwInitArgs))
                args += $" {hwInitArgs}";

            var hwAccelArgs = GetHardwareAccelArgs(hwAccel);
            if (!string.IsNullOrEmpty(hwAccelArgs))
                args += $" {hwAccelArgs}";

            if (hwAccel == HardwareAccelerationType.videotoolbox)
                args += " -hwaccel_output_format videotoolbox_vld";

            args += $" -ss {actualSeekSeconds} -i \"{inputPath}\"";

            string? filterChain = null;

            if (!skipToneMapping && encodingOptions.EnableTonemapping && isHDR)
            {
                filterChain = ToneMapFilterService.GetToneMapFilter(encodingOptions, video, hwAccel, _logger);
            }

            // hwdownload converts GPU frames to CPU memory for PNG encoding since hardware decoders output to GPU surfaces
            if (hwAccel != HardwareAccelerationType.none)
            {
                string downloadFilter = hwAccel == HardwareAccelerationType.videotoolbox
                    ? "hwdownload,format=nv12"
                    : "hwdownload,format=yuv420p";

                filterChain = string.IsNullOrEmpty(filterChain)
                    ? downloadFilter
                    : $"{filterChain},{downloadFilter}";
            }

            if (!string.IsNullOrEmpty(filterChain))
            {
                args += $" -vf \"{filterChain}\"";
                _logger.LogDebug("Applied filter chain: {Filter}", filterChain);
            }

            args += $" -frames:v 1 -q:v 1 -pix_fmt rgb24 \"{outputPath}\"";

            return args;
        }

        // GetHardwareInitArgs
        // Returns FFmpeg hardware device initialization arguments for the specified acceleration type.
        private string GetHardwareInitArgs(HardwareAccelerationType hwAccel)
        {
            return hwAccel switch
            {
                HardwareAccelerationType.qsv => "-init_hw_device vaapi=va:/dev/dri/renderD128 -init_hw_device qsv=qs@va",
                HardwareAccelerationType.nvenc => "-init_hw_device cuda=cu:0",
                HardwareAccelerationType.amf => "-init_hw_device opencl=ocl",
                HardwareAccelerationType.vaapi => "-init_hw_device vaapi=va:/dev/dri/renderD128",
                HardwareAccelerationType.videotoolbox => "-init_hw_device videotoolbox=vt",
                _ => string.Empty
            };
        }

        // GetHardwareAccelArgs
        // Returns FFmpeg hardware acceleration selection arguments for the specified type.
        private string GetHardwareAccelArgs(HardwareAccelerationType hwAccel)
        {
            return hwAccel switch
            {
                HardwareAccelerationType.qsv => "-hwaccel vaapi -hwaccel_output_format vaapi",
                HardwareAccelerationType.nvenc => "-hwaccel cuda",
                HardwareAccelerationType.amf => "-hwaccel vaapi",
                HardwareAccelerationType.vaapi => "-hwaccel vaapi",
                HardwareAccelerationType.videotoolbox => "-hwaccel videotoolbox",
                _ => string.Empty
            };
        }

        // CanProcess
        // Determines if this service can handle the given video based on codec support and HDR capabilities.
        public bool CanProcess(EpisodeMetadata metadata, EncodingOptions encodingOptions)
        {
            var video = metadata.VideoMetadata;
            var hwAccel = encodingOptions.HardwareAccelerationType;

            if (hwAccel == HardwareAccelerationType.none) return false;

            var codecSupported = encodingOptions.HardwareDecodingCodecs
                .Any(c => string.Equals(c, video.VideoCodec.ToString(), StringComparison.OrdinalIgnoreCase));

            if (!codecSupported) return false;

            // HDR content requires tone mapping support from the hardware acceleration type
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

        // ExtractSceneAsync
        // Returns null because this service only builds arguments; execution is handled externally.
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
