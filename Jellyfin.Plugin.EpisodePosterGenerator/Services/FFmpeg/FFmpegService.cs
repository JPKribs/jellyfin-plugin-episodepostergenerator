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
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    public class FFmpegService
    {
        private const int MaxRetries = 30;
        private const int MaxHardwareAttempts = 15;
        private const int EarlyExitAttemptThreshold = 5;
        private const double BrightnessThreshold = 0.05;
        private const double HdrBrightnessMultiplier = 0.5;
        private const double SharpnessThreshold = 100.0;
        private const double HdrEarlyExitScoreThreshold = 0.6;
        private const double DefaultDurationSeconds = 3600;
        private const double DefaultSeekStartPercent = 0.2;
        private const double DefaultSeekEndPercent = 0.8;
        private const double HdrBrightnessWeight = 0.6;
        private const double SdrBrightnessWeight = 0.4;

        private readonly ILogger<FFmpegService> _logger;
        private readonly IServerConfigurationManager _configurationManager;
        private readonly IMediaEncoder _mediaEncoder;
        private readonly IFFmpegService _hardwareService;
        private readonly IFFmpegService _softwareService;
        private readonly BrightnessService _brightnessService;
        private readonly HardwareValidationService _validationService;

        // Constructor
        // Initializes FFmpeg service with hardware and software extraction strategies.
        public FFmpegService(
            ILogger<FFmpegService> logger,
            IServerConfigurationManager configurationManager,
            IMediaEncoder mediaEncoder,
            HardwareFFmpegService hardwareService,
            SoftwareFFmpegService softwareService,
            BrightnessService brightnessService,
            HardwareValidationService validationService)
        {
            _logger = logger;
            _configurationManager = configurationManager;
            _mediaEncoder = mediaEncoder;
            _hardwareService = hardwareService;
            _softwareService = softwareService;
            _brightnessService = brightnessService;
            _validationService = validationService;
        }

        // ExtractSceneAsync
        // Extracts a high-quality frame from video using a fallback strategy chain.
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
            if (videoDurationSeconds <= 0) videoDurationSeconds = DefaultDurationSeconds;

            var isHDR = metadata.VideoMetadata.VideoHdrType.IsHDR();
            var is10Bit = metadata.VideoMetadata.VideoColorBits >= 10;
            var needsToneMapping = isHDR || is10Bit;
            var brightnessThreshold = isHDR ? BrightnessThreshold * HdrBrightnessMultiplier : BrightnessThreshold;

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

            // Strategy 1: HWA + Tone Mapping (best quality for HDR content)
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

            // Strategy 2: HWA only (fast, no tone mapping)
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
            else if (config.EnableHWA)
            {
                // hardwareValidated must be false here since we're in the else branch
                _logger.LogInformation("Hardware acceleration disabled due to failed validation, using software");
            }
            else
            {
                _logger.LogInformation("Hardware acceleration disabled, using software");
            }

            // Strategy 3: Software + Tone Mapping (slower but reliable for HDR)
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

            // Strategy 4: Software only (final fallback)
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

        // TryExtractionStrategy
        // Attempts frame extraction with specified hardware/tone mapping settings, tracking best quality frame.
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
            int maxAttemptsForStrategy = useHardware ? MaxHardwareAttempts : MaxRetries;
            var isHDR = metadata.VideoMetadata.VideoHdrType.IsHDR();

            for (int attempt = 0; attempt < maxAttemptsForStrategy; attempt++)
            {
                var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
                bool keepOutputFile = false;

                try
                {
                    var seekTime = GenerateSeekTime(videoDurationSeconds, attempt, config);

                    string? args = service.BuildFFmpegArgs(outputPath, metadata, encodingOptions, seekTime, !useToneMapping);
                    if (string.IsNullOrWhiteSpace(args))
                    {
                        _logger.LogError("{Method}: Could not build FFmpeg arguments - service returned null", methodName);
                        break;
                    }

                    var ffmpegPath = _mediaEncoder.EncoderPath;

                    if (string.IsNullOrWhiteSpace(ffmpegPath))
                    {
                        _logger.LogWarning("MediaEncoder.EncoderPath is null or empty, falling back to system ffmpeg");
                        ffmpegPath = "ffmpeg";
                    }

                    if (attempt == 0)
                    {
                        _logger.LogInformation("{Method}: Using FFmpeg command: {Path} {Args}", methodName, ffmpegPath, args);
                    }

                    bool success = await RunFFmpegAsync(ffmpegPath, args, outputPath, cancellationToken).ConfigureAwait(false);

                    if (!success)
                    {
                        // Hardware failures are usually systemic - give up early
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

                    // Load bitmap once for all quality checks (eliminates triple file read)
                    using var stream = File.OpenRead(outputPath);
                    using var frameBitmap = SKBitmap.Decode(stream);
                    if (frameBitmap == null)
                    {
                        continue;
                    }

                    // Create single downscaled bitmap for all quality analysis (eliminates duplicate downscaling)
                    using var analysisBitmap = CreateAnalysisBitmap(frameBitmap);
                    var actualBrightness = analysisBitmap != null ? GetFrameBrightnessFromAnalysis(analysisBitmap) : 0.0;
                    var actualSharpness = analysisBitmap != null ? GetFrameSharpnessFromAnalysis(analysisBitmap) : 0.0;
                    var qualityScore = CalculateFrameQualityScore(actualBrightness, actualSharpness, isHDR);

                    var brightnessOk = _brightnessService.IsFrameBrightEnough(frameBitmap, brightnessThreshold);
                    var sharpnessOk = actualSharpness >= SharpnessThreshold;
                    var qualityOk = brightnessOk && sharpnessOk;

                    if (attempt < EarlyExitAttemptThreshold || qualityOk)
                    {
                        _logger.LogDebug("{Method} attempt {Attempt}: Brightness {Brightness:F3} (OK: {BrightOk}), Sharpness {Sharpness:F1} (OK: {SharpOk}), Score {Score:F3}",
                            methodName, attempt + 1, actualBrightness, brightnessOk, actualSharpness, sharpnessOk, qualityScore);
                    }

                    // Found a frame meeting both brightness and sharpness thresholds
                    if (qualityOk)
                    {
                        TryDeleteFile(bestFramePath);
                        keepOutputFile = true;
                        _logger.LogInformation("{Method}: Found high-quality frame on attempt {Attempt} - Brightness: {Brightness:F3}, Sharpness: {Sharpness:F1}, Score: {Score:F3}",
                            methodName, attempt + 1, actualBrightness, actualSharpness, qualityScore);
                        return outputPath;
                    }

                    // Track best frame seen so far
                    if (qualityScore > bestQualityScore)
                    {
                        TryDeleteFile(bestFramePath);
                        bestFramePath = outputPath;
                        keepOutputFile = true;
                        bestQualityScore = qualityScore;
                        bestBrightness = actualBrightness;
                        bestSharpness = actualSharpness;
                    }

                    // Early exit for HDR content if we have a reasonably good frame
                    if (isHDR && qualityScore > HdrEarlyExitScoreThreshold && attempt > EarlyExitAttemptThreshold)
                    {
                        _logger.LogInformation("{Method}: Found reasonably good HDR frame after {Attempts} attempts (score: {Score:F3}), stopping search",
                            methodName, attempt + 1, qualityScore);
                        break;
                    }
                }
                finally
                {
                    // Always clean up the temp file unless it's being kept as the best/returned frame
                    if (!keepOutputFile)
                    {
                        TryDeleteFile(outputPath);
                    }
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

        // ApplyFinalProcessing
        // Returns the frame path for further processing by CanvasService.
        private Task<string> ApplyFinalProcessing(string framePath, PosterSettings config, bool isHDR)
        {
            // HDR brightness is applied by CanvasService.BrightenBitmap on the in-memory bitmap
            return Task.FromResult(framePath);
        }

        // GenerateSeekTime
        // Generates a random seek time within the configured extraction window.
        private int GenerateSeekTime(double videoDurationSeconds, int attempt, PosterSettings config)
        {
            var startPercent = config.ExtractWindowStart / 100.0;
            var endPercent = config.ExtractWindowEnd / 100.0;

            if (startPercent >= endPercent)
            {
                _logger.LogWarning("Invalid extraction window: start {Start}% >= end {End}%, using default 20%-80%",
                    config.ExtractWindowStart, config.ExtractWindowEnd);
                startPercent = DefaultSeekStartPercent;
                endPercent = DefaultSeekEndPercent;
            }

            var startTime = videoDurationSeconds * startPercent;
            var endTime = videoDurationSeconds * endPercent;
            return (int)(Random.Shared.NextDouble() * (endTime - startTime) + startTime);
        }

        // AnalysisSize
        // Target size for downscaled analysis (200x200 is sufficient for quality metrics).
        private const int AnalysisSize = 200;

        // CreateAnalysisBitmap
        // Creates a small downscaled copy of a bitmap for fast quality analysis.
        private SKBitmap? CreateAnalysisBitmap(SKBitmap source)
        {
            if (source == null) return null;

            // Calculate dimensions maintaining aspect ratio
            float scale = Math.Min((float)AnalysisSize / source.Width, (float)AnalysisSize / source.Height);
            int newWidth = Math.Max(1, (int)(source.Width * scale));
            int newHeight = Math.Max(1, (int)(source.Height * scale));

            var resized = new SKBitmap(newWidth, newHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(resized);
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.Low };
            canvas.DrawBitmap(source, SKRect.Create(newWidth, newHeight), paint);

            return resized;
        }

        // GetFrameBrightnessFromAnalysis
        // Calculates average brightness from a pre-downscaled analysis bitmap.
        private static double GetFrameBrightnessFromAnalysis(SKBitmap analysis)
        {
            var pixels = analysis.Pixels;
            if (pixels == null || pixels.Length == 0)
                return 0.0;

            double total = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                total += (0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue) / 255.0;
            }

            return total / pixels.Length;
        }

        // GetFrameSharpnessFromAnalysis
        // Calculates sharpness using Laplacian variance from a pre-downscaled analysis bitmap.
        private static double GetFrameSharpnessFromAnalysis(SKBitmap analysis)
        {
            int width = analysis.Width;
            int height = analysis.Height;
            var pixels = analysis.Pixels;
            if (pixels == null || pixels.Length == 0)
                return 0.0;

            double sum = 0;
            int count = 0;

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int c = y * width + x;
                    int t = (y - 1) * width + x;
                    int b = (y + 1) * width + x;
                    int l = y * width + (x - 1);
                    int r = y * width + (x + 1);

                    double center = 0.2126 * pixels[c].Red + 0.7152 * pixels[c].Green + 0.0722 * pixels[c].Blue;
                    double top    = 0.2126 * pixels[t].Red + 0.7152 * pixels[t].Green + 0.0722 * pixels[t].Blue;
                    double bottom = 0.2126 * pixels[b].Red + 0.7152 * pixels[b].Green + 0.0722 * pixels[b].Blue;
                    double left   = 0.2126 * pixels[l].Red + 0.7152 * pixels[l].Green + 0.0722 * pixels[l].Blue;
                    double right  = 0.2126 * pixels[r].Red + 0.7152 * pixels[r].Green + 0.0722 * pixels[r].Blue;

                    double lap = 4 * center - top - bottom - left - right;
                    sum += lap * lap;
                    count++;
                }
            }

            return count > 0 ? sum / count : 0.0;
        }

        // CalculateFrameQualityScore
        // Combines brightness and sharpness into a weighted quality score.
        private double CalculateFrameQualityScore(double brightness, double sharpness, bool isHDR)
        {
            double normalizedBrightness = Math.Min(brightness / BrightnessThreshold, 1.0);
            double normalizedSharpness = Math.Min(sharpness / SharpnessThreshold, 1.0);

            // HDR content prioritizes brightness; SDR prioritizes sharpness
            double brightnessWeight = isHDR ? HdrBrightnessWeight : SdrBrightnessWeight;
            double sharpnessWeight = 1.0 - brightnessWeight;

            return (normalizedBrightness * brightnessWeight) + (normalizedSharpness * sharpnessWeight);
        }

        // FFmpegProcessTimeout
        // Maximum time to wait for a single FFmpeg frame extraction before killing the process.
        private static readonly TimeSpan FFmpegProcessTimeout = TimeSpan.FromSeconds(60);

        // RunFFmpegAsync
        // Executes FFmpeg process with timeout and cancellation support.
        // Ensures the process is killed if it exceeds the timeout or cancellation is requested.
        private async Task<bool> RunFFmpegAsync(string ffmpegPath, string arguments, string outputPath, CancellationToken cancellationToken)
        {
            Process? process = null;
            try
            {
                process = new Process
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

                // Combine caller cancellation with a per-process timeout to prevent hangs
                using var timeoutCts = new CancellationTokenSource(FFmpegProcessTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                try
                {
                    await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Kill the process if cancelled or timed out
                    KillProcess(process);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("FFmpeg process killed due to task cancellation");
                    }
                    else
                    {
                        _logger.LogWarning("FFmpeg process killed after {Timeout}s timeout", FFmpegProcessTimeout.TotalSeconds);
                    }

                    return false;
                }

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
            catch (OperationCanceledException)
            {
                KillProcess(process);
                _logger.LogWarning("FFmpeg process killed due to cancellation");
                return false;
            }
            catch (Exception ex)
            {
                KillProcess(process);
                _logger.LogError(ex, "Error running FFmpeg process");
                return false;
            }
            finally
            {
                process?.Dispose();
            }
        }

        // KillProcess
        // Forcefully terminates an FFmpeg process to prevent zombie processes.
        private void KillProcess(Process? process)
        {
            if (process == null) return;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    _logger.LogDebug("FFmpeg process {Pid} killed", process.Id);
                }
            }
            catch (InvalidOperationException) { }
            catch (SystemException) { }
        }

        // TryDeleteFile
        // Safely deletes a file without throwing on race conditions or missing files.
        private static void TryDeleteFile(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                File.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        // AnalyzeFFmpegError
        // Categorizes FFmpeg stderr output into known error types for better diagnostics.
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
            if (lower.Contains("libplacebo", StringComparison.Ordinal))
                return "libplacebo filter unavailable (FFmpeg not compiled with Vulkan/libplacebo support)";
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
