using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Extensions;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    public class FFmpegService
    {
        private const int MaxRetries = 30;
        private const double BrightnessThreshold = 0.05;
        private const double SharpnessThreshold = 100.0;

        private readonly ILogger<FFmpegService> _logger;
        private readonly IServerConfigurationManager _configurationManager;
        private readonly IFFmpegService _hardwareService;
        private readonly IFFmpegService _softwareService;
        private readonly BrightnessService _brightnessService;
        private readonly HardwareValidationService _validationService;

        // MARK: Constructor
        public FFmpegService(
            ILogger<FFmpegService> logger,
            IServerConfigurationManager configurationManager,
            HardwareFFmpegService hardwareService,
            SoftwareFFmpegService softwareService,
            BrightnessService brightnessService,
            HardwareValidationService validationService)
        {
            _logger = logger;
            _configurationManager = configurationManager;
            _hardwareService = hardwareService;
            _softwareService = softwareService;
            _brightnessService = brightnessService;
            _validationService = validationService;
        }

        // MARK: ExtractSceneAsync
        public async Task<string?> ExtractSceneAsync(
            EpisodeMetadata metadata,
            PosterSettings config,
            CancellationToken cancellationToken = default)
        {
            if (metadata?.VideoMetadata == null)
            {
                _logger.LogError("Invalid metadata provided to FFmpegService");
                return null;
            }

            var encodingOptions = _configurationManager.GetEncodingOptions();
            var videoDurationSeconds = metadata.VideoMetadata.VideoLengthTicks / (double)TimeSpan.TicksPerSecond;
            if (videoDurationSeconds <= 0) videoDurationSeconds = 3600;

            var isHDR = metadata.VideoMetadata.VideoHdrType.IsHDR();
            var is10Bit = metadata.VideoMetadata.VideoColorBits >= 10;
            var needsToneMapping = isHDR || is10Bit;
            var brightnessThreshold = isHDR ? BrightnessThreshold * 0.5 : BrightnessThreshold;

            _logger.LogInformation("Video analysis: HDR={HDR}, 10-bit={TenBit}, Tone mapping needed={NeedsToneMapping}",
                isHDR, is10Bit, needsToneMapping);

            bool hardwareValidated = false;
            if (config.EnableHWA)
            {
                _logger.LogInformation("Validating hardware acceleration availability for {HwAccel}", encodingOptions.HardwareAccelerationType);
                hardwareValidated = await _validationService.ValidateHardwareAcceleration(
                    encodingOptions.HardwareAccelerationType,
                    encodingOptions,
                    cancellationToken).ConfigureAwait(false);

                if (!hardwareValidated)
                {
                    _logger.LogWarning("Hardware validation failed - hardware acceleration unavailable or misconfigured. Falling back to software decoding.");
                }
            }

            string? bestFramePath = null;

            if (config.EnableHWA && hardwareValidated && encodingOptions.EnableTonemapping && needsToneMapping)
            {
                _logger.LogInformation("Attempting to use HWA with tone mapping");
                bestFramePath = await TryExtractionStrategy(
                    metadata, config, encodingOptions, videoDurationSeconds, brightnessThreshold,
                    useHardware: true, useToneMapping: true, "HWA + Tone Mapping", cancellationToken).ConfigureAwait(false);

                if (bestFramePath != null)
                {
                    _logger.LogInformation("HWA with tone mapping succeeded");
                    return await ApplyFinalProcessing(bestFramePath, config, isHDR).ConfigureAwait(false);
                }
                _logger.LogWarning("HWA with tone mapping failed, falling back to HWA without tone mapping");
            }

            if (config.EnableHWA && hardwareValidated)
            {
                if (needsToneMapping)
                {
                    _logger.LogInformation("Falling back to HWA without tone mapping");
                }
                else
                {
                    _logger.LogInformation("Attempting to use HWA");
                }

                bestFramePath = await TryExtractionStrategy(
                    metadata, config, encodingOptions, videoDurationSeconds, brightnessThreshold,
                    useHardware: true, useToneMapping: false, "HWA", cancellationToken).ConfigureAwait(false);

                if (bestFramePath != null)
                {
                    _logger.LogInformation("HWA succeeded");
                    return await ApplyFinalProcessing(bestFramePath, config, isHDR).ConfigureAwait(false);
                }
                _logger.LogWarning("HWA failed, falling back to software");
            }
            else if (config.EnableHWA && !hardwareValidated)
            {
                _logger.LogInformation("Hardware acceleration disabled due to failed validation, using software");
            }
            else
            {
                _logger.LogInformation("Hardware acceleration disabled, using software");
            }

            if (encodingOptions.EnableTonemapping && needsToneMapping)
            {
                _logger.LogInformation("Attempting software with tone mapping");
                bestFramePath = await TryExtractionStrategy(
                    metadata, config, encodingOptions, videoDurationSeconds, brightnessThreshold,
                    useHardware: false, useToneMapping: true, "Software + Tone Mapping", cancellationToken).ConfigureAwait(false);

                if (bestFramePath != null)
                {
                    _logger.LogInformation("Software with tone mapping succeeded");
                    return await ApplyFinalProcessing(bestFramePath, config, isHDR).ConfigureAwait(false);
                }
                _logger.LogWarning("Software with tone mapping failed, falling back to software without tone mapping");
            }

            if (needsToneMapping)
            {
                _logger.LogInformation("Final fallback to software without tone mapping");
            }
            else
            {
                _logger.LogInformation("Using software");
            }

            bestFramePath = await TryExtractionStrategy(
                metadata, config, encodingOptions, videoDurationSeconds, brightnessThreshold,
                useHardware: false, useToneMapping: false, "Software", cancellationToken).ConfigureAwait(false);

            if (bestFramePath != null)
            {
                _logger.LogInformation("Software extraction succeeded");
                return await ApplyFinalProcessing(bestFramePath, config, isHDR).ConfigureAwait(false);
            }

            _logger.LogError("All extraction methods failed - no usable frames found");
            return null;
        }

        // MARK: TryExtractionStrategy
        private async Task<string?> TryExtractionStrategy(
            EpisodeMetadata metadata,
            PosterSettings config,
            EncodingOptions encodingOptions,
            double videoDurationSeconds,
            double brightnessThreshold,
            bool useHardware,
            bool useToneMapping,
            string methodName,
            CancellationToken cancellationToken)
        {
            IFFmpegService service = useHardware ? _hardwareService : _softwareService;

            if (useHardware && !service.CanProcess(metadata, encodingOptions))
            {
                _logger.LogWarning("{Method}: Hardware service cannot process this content - codec not supported or HWA unavailable", methodName);
                return null;
            }

            string? bestFramePath = null;
            double bestQualityScore = 0.0;
            double bestBrightness = 0.0;
            double bestSharpness = 0.0;
            int maxAttemptsForStrategy = useHardware ? 15 : MaxRetries;
            var isHDR = metadata.VideoMetadata.VideoHdrType.IsHDR();

            for (int attempt = 0; attempt < maxAttemptsForStrategy; attempt++)
            {
                var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
                var seekTime = GenerateSeekTime(videoDurationSeconds, attempt, config);

                string? args = service.BuildFFmpegArgs(outputPath, metadata, encodingOptions, seekTime, !useToneMapping);
                if (string.IsNullOrWhiteSpace(args))
                {
                    _logger.LogError("{Method}: Could not build FFmpeg arguments - service returned null", methodName);
                    break;
                }

                var ffmpegPath = string.IsNullOrWhiteSpace(encodingOptions.EncoderAppPath) ? "ffmpeg" : encodingOptions.EncoderAppPath;

                if (attempt == 0)
                {
                    _logger.LogInformation("{Method}: Using FFmpeg command: {Path} {Args}", methodName, ffmpegPath, args);
                }

                bool success = await RunFFmpegAsync(ffmpegPath, args, outputPath, cancellationToken).ConfigureAwait(false);

                if (!success)
                {
                    if (File.Exists(outputPath)) File.Delete(outputPath);

                    if (useHardware && attempt < 3)
                    {
                        _logger.LogWarning("{Method}: Failed on attempt {Attempt} - hardware likely not working, giving up", methodName, attempt + 1);
                        break;
                    }

                    if (attempt == 0)
                    {
                        _logger.LogWarning("{Method}: FFmpeg execution failed on first attempt", methodName);
                    }
                    continue;
                }

                var actualBrightness = GetFrameBrightness(outputPath);
                var actualSharpness = GetFrameSharpness(outputPath);
                var qualityScore = CalculateFrameQualityScore(actualBrightness, actualSharpness, isHDR);
                
                var brightnessOk = _brightnessService.IsFrameBrightEnough(outputPath, brightnessThreshold);
                var sharpnessOk = actualSharpness >= SharpnessThreshold;
                var qualityOk = brightnessOk && sharpnessOk;

                if (attempt < 5 || qualityOk)
                {
                    _logger.LogDebug("{Method} attempt {Attempt}: Brightness {Brightness:F3} (OK: {BrightOk}), Sharpness {Sharpness:F1} (OK: {SharpOk}), Score {Score:F3}",
                        methodName, attempt + 1, actualBrightness, brightnessOk, actualSharpness, sharpnessOk, qualityScore);
                }

                if (qualityOk)
                {
                    if (bestFramePath != null && File.Exists(bestFramePath))
                        File.Delete(bestFramePath);
                    _logger.LogInformation("{Method}: Found high-quality frame on attempt {Attempt} - Brightness: {Brightness:F3}, Sharpness: {Sharpness:F1}, Score: {Score:F3}",
                        methodName, attempt + 1, actualBrightness, actualSharpness, qualityScore);
                    return outputPath;
                }

                if (qualityScore > bestQualityScore)
                {
                    if (bestFramePath != null && File.Exists(bestFramePath))
                        File.Delete(bestFramePath);
                    bestFramePath = outputPath;
                    bestQualityScore = qualityScore;
                    bestBrightness = actualBrightness;
                    bestSharpness = actualSharpness;
                }
                else
                {
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                }

                if (isHDR && qualityScore > 0.6 && attempt > 5)
                {
                    _logger.LogInformation("{Method}: Found reasonably good HDR frame after {Attempts} attempts (score: {Score:F3}), stopping search",
                        methodName, attempt + 1, qualityScore);
                    break;
                }
            }

            if (bestFramePath != null)
            {
                _logger.LogInformation("{Method}: Using best available frame - Brightness: {Brightness:F3}, Sharpness: {Sharpness:F1}, Quality Score: {Score:F3}",
                    methodName, bestBrightness, bestSharpness, bestQualityScore);
                return bestFramePath;
            }

            _logger.LogWarning("{Method}: Failed to extract any usable frames after {Attempts} attempts", methodName, maxAttemptsForStrategy);
            return null;
        }

        // MARK: ApplyFinalProcessing
        private Task<string> ApplyFinalProcessing(string framePath, PosterSettings config, bool isHDR)
        {
            if (isHDR && config.BrightenHDR > 0)
            {
                _logger.LogDebug("Applying HDR brightness adjustment: {Adjustment}", config.BrightenHDR);
                _brightnessService.Brighten(framePath, config.BrightenHDR, config.PosterFileType);
            }

            return Task.FromResult(framePath);
        }

        // MARK: GenerateSeekTime
        private int GenerateSeekTime(double videoDurationSeconds, int attempt, PosterSettings config)
        {
            var random = new Random();
            var startPercent = config.ExtractWindowStart / 100.0;
            var endPercent = config.ExtractWindowEnd / 100.0;

            if (startPercent >= endPercent)
            {
                _logger.LogWarning("Invalid extraction window: start {Start}% >= end {End}%, using default 20%-80%",
                    config.ExtractWindowStart, config.ExtractWindowEnd);
                startPercent = 0.2;
                endPercent = 0.8;
            }

            var startTime = videoDurationSeconds * startPercent;
            var endTime = videoDurationSeconds * endPercent;
            return (int)(random.NextDouble() * (endTime - startTime) + startTime);
        }

        // MARK: GetFrameBrightness
        private double GetFrameBrightness(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var bitmap = SKBitmap.Decode(stream);
                if (bitmap == null) return 0.0;

                double totalBrightness = 0;
                int sampleCount = 0;
                int stepSize = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 100);

                for (int y = 0; y < bitmap.Height; y += stepSize)
                {
                    for (int x = 0; x < bitmap.Width; x += stepSize)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        var brightness = (0.2126 * pixel.Red + 0.7152 * pixel.Green + 0.0722 * pixel.Blue) / 255.0;
                        totalBrightness += brightness;
                        sampleCount++;
                    }
                }

                return sampleCount > 0 ? totalBrightness / sampleCount : 0.0;
            }
            catch
            {
                return 0.0;
            }
        }

        // MARK: GetFrameSharpness
        private double GetFrameSharpness(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var bitmap = SKBitmap.Decode(stream);
                if (bitmap == null) return 0.0;

                int width = bitmap.Width;
                int height = bitmap.Height;
                double sumLaplacian = 0;
                int count = 0;

                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        var center = bitmap.GetPixel(x, y);
                        var top = bitmap.GetPixel(x, y - 1);
                        var bottom = bitmap.GetPixel(x, y + 1);
                        var left = bitmap.GetPixel(x - 1, y);
                        var right = bitmap.GetPixel(x + 1, y);

                        double centerGray = 0.2126 * center.Red + 0.7152 * center.Green + 0.0722 * center.Blue;
                        double topGray = 0.2126 * top.Red + 0.7152 * top.Green + 0.0722 * top.Blue;
                        double bottomGray = 0.2126 * bottom.Red + 0.7152 * bottom.Green + 0.0722 * bottom.Blue;
                        double leftGray = 0.2126 * left.Red + 0.7152 * left.Green + 0.0722 * left.Blue;
                        double rightGray = 0.2126 * right.Red + 0.7152 * right.Green + 0.0722 * right.Blue;

                        double laplacian = Math.Abs(4 * centerGray - topGray - bottomGray - leftGray - rightGray);
                        sumLaplacian += laplacian * laplacian;
                        count++;
                    }
                }

                return count > 0 ? sumLaplacian / count : 0.0;
            }
            catch
            {
                return 0.0;
            }
        }

        // MARK: CalculateFrameQualityScore
        private double CalculateFrameQualityScore(double brightness, double sharpness, bool isHDR)
        {
            double normalizedBrightness = Math.Min(brightness / BrightnessThreshold, 1.0);
            double normalizedSharpness = Math.Min(sharpness / SharpnessThreshold, 1.0);
            
            double brightnessWeight = isHDR ? 0.6 : 0.4;
            double sharpnessWeight = 1.0 - brightnessWeight;
            
            return (normalizedBrightness * brightnessWeight) + (normalizedSharpness * sharpnessWeight);
        }

        // MARK: RunFFmpegAsync
        private async Task<bool> RunFFmpegAsync(string ffmpegPath, string arguments, string outputPath, CancellationToken cancellationToken)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                };

                process.Start();
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    var errorType = AnalyzeFFmpegError(error);
                    _logger.LogWarning("FFmpeg failed with exit code {ExitCode}, Error type: {ErrorType}, Details: {Error}",
                        process.ExitCode, errorType, error);
                    return false;
                }

                if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                {
                    _logger.LogWarning("FFmpeg completed but output file is missing or empty: {OutputPath}", outputPath);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running FFmpeg process");
                return false;
            }
        }

        // MARK: AnalyzeFFmpegError
        private string AnalyzeFFmpegError(string stderr)
        {
            if (string.IsNullOrWhiteSpace(stderr))
                return "Unknown";
            
            var lower = stderr.ToLowerInvariant();
            
            if (lower.Contains("cannot load opencl", StringComparison.Ordinal) || lower.Contains("failed to set value 'opencl", StringComparison.Ordinal))
                return "OpenCL unavailable";
            if (lower.Contains("cannot load cuda", StringComparison.Ordinal) || lower.Contains("cuda", StringComparison.Ordinal))
                return "CUDA unavailable";
            if (lower.Contains("cannot load qsv", StringComparison.Ordinal) || lower.Contains("qsv", StringComparison.Ordinal))
                return "QSV unavailable";
            if (lower.Contains("cannot load vaapi", StringComparison.Ordinal) || lower.Contains("vaapi", StringComparison.Ordinal))
                return "VAAPI unavailable";
            if (lower.Contains("hwaccel", StringComparison.Ordinal) && lower.Contains("not found", StringComparison.Ordinal))
                return "Hardware decoder not found";
            if (lower.Contains("hwupload", StringComparison.Ordinal) || lower.Contains("hwdownload", StringComparison.Ordinal))
                return "Hardware upload/download failed";
            if (lower.Contains("tonemap", StringComparison.Ordinal))
                return "Tone mapping filter failed";
            if (lower.Contains("cannot open", StringComparison.Ordinal) || lower.Contains("no such file", StringComparison.Ordinal))
                return "File access error";
            if (lower.Contains("invalid", StringComparison.Ordinal) && lower.Contains("codec", StringComparison.Ordinal))
                return "Unsupported codec";
            
            return "Unknown FFmpeg error";
        }
    }
}