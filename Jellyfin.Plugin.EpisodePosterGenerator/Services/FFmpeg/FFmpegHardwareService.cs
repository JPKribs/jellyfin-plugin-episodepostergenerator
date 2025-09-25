using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Media
{
    /// <summary>
    /// Hardware-accelerated FFmpeg operations with intelligent fallback detection
    /// </summary>
    public class FFmpegHardwareService
    {
        private readonly ILogger<FFmpegHardwareService> _logger;
        private readonly IMediaEncoder _mediaEncoder;
        private readonly IServerConfigurationManager _configurationManager;

        /// <summary>
        /// Hardware acceleration arguments determined at startup
        /// </summary>
        private readonly string _hardwareAccelerationArgs;

        /// <summary>
        /// Cache of codecs that failed hardware acceleration
        /// </summary>
        private static readonly HashSet<string> _failedHwaccelCodecs = new();

        /// <summary>
        /// Thread sync for failed codec cache
        /// </summary>
        private static readonly object _failedCodecCacheLock = new object();

        /// <summary>
        /// Number of threads for FFmpeg to use
        /// </summary>
        private readonly int _ffmpegThreads;

        /// <summary>
        /// Threading arguments for FFmpeg commands
        /// </summary>
        private readonly string _threadingArgs;

        // MARK: Constructor
        public FFmpegHardwareService(
            ILogger<FFmpegHardwareService> logger,
            IMediaEncoder mediaEncoder,
            IServerConfigurationManager configurationManager)
        {
            _logger = logger;
            _mediaEncoder = mediaEncoder;
            _configurationManager = configurationManager;
            _hardwareAccelerationArgs = DetermineHardwareAcceleration();
            
            _ffmpegThreads = Math.Max(1, Environment.ProcessorCount / 4);
            _threadingArgs = $"-threads {_ffmpegThreads}";
            
            LogHardwareAccelerationStatus();
        }

        // MARK: ShouldUseHardwareAccelerationAsync
        public async Task<bool> ShouldUseHardwareAccelerationAsync(string videoPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_hardwareAccelerationArgs))
            {
                return false;
            }

            try
            {
                var codec = await GetVideoCodecAsync(videoPath, cancellationToken).ConfigureAwait(false);
                
                lock (_failedCodecCacheLock)
                {
                    if (_failedHwaccelCodecs.Contains(codec))
                    {
                        return false;
                    }
                }

                // Check if codec is supported by hardware acceleration
                return !string.IsNullOrEmpty(GetCodecSpecificHwAccelArgs(codec, _hardwareAccelerationArgs));
            }
            catch
            {
                return false;
            }
        }

        // MARK: ExtractFrameAsync
        public async Task<string?> ExtractFrameAsync(
            string videoPath,
            TimeSpan timestamp,
            CancellationToken cancellationToken = default)
        {
            var outputPath = Path.GetTempFileName() + ".jpg";
            var timestampStr = $"{(int)timestamp.TotalHours:D2}:{timestamp.Minutes:D2}:{timestamp.Seconds:D2}.{timestamp.Milliseconds:D3}";
            
            try
            {
                var (colorSpace, colorTransfer, pixelFormat) = await GetVideoColorPropertiesAsync(videoPath, cancellationToken).ConfigureAwait(false);
                var codec = await GetVideoCodecAsync(videoPath, cancellationToken).ConfigureAwait(false);
                
                var hwAccelArgs = GetCodecSpecificHwAccelArgs(codec, _hardwareAccelerationArgs);
                var filterChain = BuildHardwareVideoFilter(colorSpace, colorTransfer, pixelFormat, hwAccelArgs);
                
                var arguments = $"-ss {timestampStr} {hwAccelArgs} -i \"{videoPath}\" -frames:v 1 {filterChain} -q:v 1 {_threadingArgs} \"{outputPath}\"";
                
                await ExecuteFFmpegAsync(arguments, cancellationToken).ConfigureAwait(false);
                
                if (File.Exists(outputPath))
                {
                    _logger.LogDebug("Hardware accelerated frame extraction successful at {Timestamp} using {Codec} decoder", timestamp, codec);
                    return outputPath;
                }
            }
            catch (Exception ex)
            {
                var codec = await GetVideoCodecAsync(videoPath, cancellationToken).ConfigureAwait(false);
                bool shouldLogWarning = false;
                lock (_failedCodecCacheLock)
                {
                    shouldLogWarning = _failedHwaccelCodecs.Add(codec);
                }
                
                if (shouldLogWarning)
                {
                    _logger.LogWarning(ex, "Hardware acceleration failed for {Codec} codec", codec);
                }
                
                // Clean up failed attempt
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
            
            return null;
        }

        // MARK: DetermineHardwareAcceleration
        private string DetermineHardwareAcceleration()
        {
            try
            {
                var encodingOptions = _configurationManager.GetEncodingOptions();
                var hwAccelType = encodingOptions.HardwareAccelerationType;

                if (string.IsNullOrEmpty(hwAccelType) || hwAccelType.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    return "";
                }

                return hwAccelType.ToLowerInvariant() switch
                {
                    "vaapi" => "-hwaccel vaapi -hwaccel_output_format vaapi",
                    "qsv" => "-hwaccel qsv -hwaccel_output_format qsv",
                    "nvenc" or "cuda" => "-hwaccel cuda -hwaccel_output_format cuda",
                    "videotoolbox" => "-hwaccel videotoolbox",
                    "d3d11va" => "-hwaccel d3d11va -hwaccel_output_format d3d11",
                    "dxva2" => "-hwaccel dxva2",
                    _ => ""
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to determine hardware acceleration settings");
                return "";
            }
        }

        // MARK: GetCodecSpecificHwAccelArgs
        private string GetCodecSpecificHwAccelArgs(string codec, string baseHwAccelArgs)
        {
            if (string.IsNullOrEmpty(baseHwAccelArgs) || string.IsNullOrEmpty(codec))
                return "";

            return codec.ToLowerInvariant() switch
            {
                "h264" or "avc" => baseHwAccelArgs,
                "h265" or "hevc" => baseHwAccelArgs,
                "vp9" => baseHwAccelArgs.Contains("vaapi") || baseHwAccelArgs.Contains("cuda") ? baseHwAccelArgs : "",
                "av1" => baseHwAccelArgs.Contains("vaapi") || baseHwAccelArgs.Contains("cuda") ? baseHwAccelArgs : "",
                _ => ""
            };
        }

        // MARK: BuildHardwareVideoFilter
        private string BuildHardwareVideoFilter(string colorSpace, string colorTransfer, string pixelFormat, string hwAccelArgs)
        {
            if (string.IsNullOrEmpty(hwAccelArgs))
                return "";

            var filters = new List<string>();

            if (hwAccelArgs.Contains("vaapi"))
            {
                filters.Add("scale_vaapi=600:900:force_original_aspect_ratio=increase");
                filters.Add("crop=600:900");
            }
            else if (hwAccelArgs.Contains("cuda"))
            {
                filters.Add("scale_cuda=600:900:force_original_aspect_ratio=increase");
                filters.Add("crop=600:900");
            }
            else if (hwAccelArgs.Contains("qsv"))
            {
                filters.Add("scale_qsv=600:900");
            }
            else if (hwAccelArgs.Contains("videotoolbox"))
            {
                filters.Add("scale=600:900:force_original_aspect_ratio=increase");
                filters.Add("crop=600:900");
            }

            return filters.Count > 0 ? $"-vf \"{string.Join(",", filters)}\"" : "";
        }

        // MARK: GetVideoCodecAsync
        private async Task<string> GetVideoCodecAsync(string videoPath, CancellationToken cancellationToken = default)
        {
            var arguments = $"-v error -select_streams v:0 -show_entries stream=codec_name -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";
            
            try
            {
                var result = await ExecuteFFprobeAsync(arguments, cancellationToken).ConfigureAwait(false);
                var codec = result.Trim();
                
                return !string.IsNullOrEmpty(codec) ? codec : "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        // MARK: GetVideoColorPropertiesAsync
        private async Task<(string colorSpace, string colorTransfer, string pixelFormat)> GetVideoColorPropertiesAsync(string videoPath, CancellationToken cancellationToken = default)
        {
            var arguments = $"-v error -select_streams v:0 -show_entries stream=color_space,color_transfer,pix_fmt -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";
            
            try
            {
                var result = await ExecuteFFprobeAsync(arguments, cancellationToken).ConfigureAwait(false);
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                var colorSpace = lines.Length > 0 ? lines[0].Trim() : "";
                var colorTransfer = lines.Length > 1 ? lines[1].Trim() : "";
                var pixelFormat = lines.Length > 2 ? lines[2].Trim() : "";
                
                return (colorSpace, colorTransfer, pixelFormat);
            }
            catch
            {
                return ("", "", "");
            }
        }

        // MARK: ExecuteFFmpegAsync
        private async Task ExecuteFFmpegAsync(string arguments, CancellationToken cancellationToken)
        {
            var ffmpegPath = GetFFmpegPath();
            
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            _logger.LogDebug("Executing FFmpeg: {FileName} {Arguments}", ffmpegPath, arguments);

            process.Start();
            
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"FFmpeg failed with exit code {process.ExitCode}: {error}");
            }
        }

        // MARK: ExecuteFFprobeAsync
        private async Task<string> ExecuteFFprobeAsync(string arguments, CancellationToken cancellationToken)
        {
            var ffprobePath = GetFFprobePath();
            
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"FFprobe failed with exit code {process.ExitCode}: {error}");
            }

            return output;
        }

        // MARK: GetFFmpegPath
        private string GetFFmpegPath()
        {
            var path = _mediaEncoder.EncoderPath;
            return string.IsNullOrEmpty(path) ? "ffmpeg" : path;
        }

        // MARK: GetFFprobePath
        private string GetFFprobePath()
        {
            var path = _mediaEncoder.ProbePath;
            return string.IsNullOrEmpty(path) ? "ffprobe" : path;
        }

        // MARK: LogHardwareAccelerationStatus
        private void LogHardwareAccelerationStatus()
        {
            if (!string.IsNullOrEmpty(_hardwareAccelerationArgs))
            {
                _logger.LogInformation("Hardware Acceleration: ENABLED - {HwArgs}", _hardwareAccelerationArgs);
            }
            else
            {
                _logger.LogInformation("Hardware Acceleration: DISABLED - using software decoding");
            }
        }
    }
}