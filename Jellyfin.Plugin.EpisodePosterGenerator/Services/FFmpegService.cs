using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services;

/// <summary>
/// Video processing service for FFmpeg operations including frame extraction and black scene detection
/// </summary>
public class FFmpegService : IDisposable
{
    /// <summary>
    /// Logger for video processing monitoring
    /// </summary>
    private readonly ILogger<FFmpegService> _logger;

    /// <summary>
    /// Jellyfin's media encoder service for FFmpeg/FFprobe access
    /// </summary>
    private readonly IMediaEncoder _mediaEncoder;

    /// <summary>
    /// Cache for black scene detection results
    /// </summary>
    private static readonly Dictionary<string, (DateTime Created, List<BlackInterval> Intervals)> _blackIntervalCache = new();

    /// <summary>
    /// Cache expiration time (24 hours)
    /// </summary>
    private static readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(24);

    /// <summary>
    /// Thread-safe random number generator
    /// </summary>
    private static readonly Random _random = new();

    /// <summary>
    /// Cache of codecs that failed hardware acceleration
    /// </summary>
    private static readonly HashSet<string> _failedHwaccelCodecs = new();

    /// <summary>
    /// Thread sync for failed codec cache
    /// </summary>
    private static readonly object _failedCodecCacheLock = new object();

    /// <summary>
    /// Hardware acceleration arguments determined at startup
    /// </summary>
    private readonly string _hardwareAccelerationArgs;

    // MARK: Threading Configuration Properties
    /// <summary>
    /// Maximum number of concurrent FFmpeg operations
    /// </summary>
    private readonly int _maxConcurrentOperations;

    /// <summary>
    /// Semaphore to control concurrent operations
    /// </summary>
    private readonly SemaphoreSlim _operationSemaphore;

    /// <summary>
    /// Number of threads for FFmpeg to use
    /// </summary>
    private readonly int _ffmpegThreads;

    /// <summary>
    /// Threading arguments for FFmpeg commands
    /// </summary>
    private readonly string _threadingArgs;

    /// <summary>
    /// Flag to track disposal state
    /// </summary>
    private bool _disposed;

    // MARK: Constructor
    public FFmpegService(ILogger<FFmpegService> logger, IMediaEncoder mediaEncoder)
    {
        _logger = logger;
        _mediaEncoder = mediaEncoder;
        _hardwareAccelerationArgs = DetermineHardwareAcceleration();
        
        // Configure threading based on system capabilities
        _maxConcurrentOperations = Math.Max(1, Environment.ProcessorCount / 2);
        _operationSemaphore = new SemaphoreSlim(_maxConcurrentOperations, _maxConcurrentOperations);
        _ffmpegThreads = Math.Max(1, Environment.ProcessorCount / 4);
        _threadingArgs = $"-threads {_ffmpegThreads}";
        
        _logger.LogInformation("FFmpeg threading configured: {MaxConcurrent} concurrent ops, {Threads} threads per operation", 
            _maxConcurrentOperations, _ffmpegThreads);
    }

    // MARK: GetFFmpegPath
    private string GetFFmpegPath()
    {
        var path = _mediaEncoder.EncoderPath;
        if (string.IsNullOrEmpty(path))
        {
            return "ffmpeg";
        }
        return path;
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

    // MARK: GetFFprobePath
    private string GetFFprobePath()
    {
        var path = _mediaEncoder.ProbePath;
        if (string.IsNullOrEmpty(path))
        {
            return "ffprobe";
        }
        return path;
    }

    // MARK: DetermineHardwareAcceleration
    private string DetermineHardwareAcceleration()
    {
        try
        {
            string hwaccelMethod = string.Empty;
            string hwaccelArgs = string.Empty;
            
            if (OperatingSystem.IsMacOS())
            {
                hwaccelMethod = "VideoToolbox";
                hwaccelArgs = "-hwaccel videotoolbox";
            }
            else if (OperatingSystem.IsWindows())
            {
                var cudaSupported = _mediaEncoder.SupportsHwaccel("cuda");
                var d3d11vaSupported = _mediaEncoder.SupportsHwaccel("d3d11va");
                
                if (cudaSupported)
                {
                    hwaccelMethod = "CUDA";
                    hwaccelArgs = "-hwaccel cuda";
                }
                else if (d3d11vaSupported)
                {
                    hwaccelMethod = "D3D11VA";
                    hwaccelArgs = "-hwaccel d3d11va";
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                var vaapiSupported = _mediaEncoder.SupportsHwaccel("vaapi");
                var qsvSupported = _mediaEncoder.SupportsHwaccel("qsv");
                var cudaSupported = _mediaEncoder.SupportsHwaccel("cuda");
                
                if (vaapiSupported)
                {
                    hwaccelMethod = "VAAPI";
                    hwaccelArgs = "-hwaccel vaapi";
                }
                else if (qsvSupported)
                {
                    hwaccelMethod = "QSV";
                    hwaccelArgs = "-hwaccel qsv";
                }
                else if (cudaSupported)
                {
                    hwaccelMethod = "CUDA";
                    hwaccelArgs = "-hwaccel cuda";
                }
            }

            if (!string.IsNullOrEmpty(hwaccelMethod))
            {
                _logger.LogInformation("Hardware Acceleration: {HwaccelMethod}", hwaccelMethod);
            }
            else
            {
                _logger.LogInformation("Hardware Acceleration: Disabled, using software decoding");
            }

            return hwaccelArgs;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not determine hardware acceleration, using software decoding");
            return string.Empty;
        }
    }

    // MARK: IsHDRContent
    private bool IsHDRContent(string colorSpace, string transferCharacteristic, string pixelFormat)
    {
        if (string.IsNullOrEmpty(transferCharacteristic) && string.IsNullOrEmpty(colorSpace) && string.IsNullOrEmpty(pixelFormat))
        {
            return false;
        }
        
        var hdrTransferCharacteristics = new[]
        {
            "smpte2084", "arib-std-b67", "smpte428", "iec61966-2-1", "bt2020-10", "bt2020-12"
        };
        
        var hdrColorSpaces = new[]
        {
            "bt2020nc", "bt2020c", "smpte431", "smpte432", "jedec-p22"
        };
        
        var hdr10BitFormats = new[]
        {
            "yuv420p10le", "yuv422p10le", "yuv444p10le", "yuv420p12le", "yuv422p12le", "yuv444p12le"
        };
        
        return hdrTransferCharacteristics.Contains(transferCharacteristic, StringComparer.OrdinalIgnoreCase) ||
               hdrColorSpaces.Contains(colorSpace, StringComparer.OrdinalIgnoreCase) ||
               hdr10BitFormats.Contains(pixelFormat, StringComparer.OrdinalIgnoreCase);
    }

    // MARK: BuildVideoFilter
    private string BuildVideoFilter(string colorSpace, string colorTransfer, string pixelFormat)
    {
        var is10Bit = pixelFormat.Contains("10le", StringComparison.Ordinal) || pixelFormat.Contains("12le", StringComparison.Ordinal);
        var isHDR = IsHDRContent(colorSpace, colorTransfer, pixelFormat);
        
        if (is10Bit || isHDR)
        {
            return "-vf \"scale=iw*sar:ih,format=yuv420p\"";
        }
        
        return "";
    }

    // MARK: GetVideoDurationAsync
    public async Task<TimeSpan?> GetVideoDurationAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        var arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";
        
        try
        {
            var result = await ExecuteFFprobeAsync(arguments, cancellationToken).ConfigureAwait(false);
            
            if (double.TryParse(result.Trim(), out var seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get video duration for {VideoPath}", videoPath);
        }

        return null;
    }

    // MARK: DetectBlackScenesAsync
    public async Task<List<BlackInterval>> DetectBlackScenesAsync(string videoPath, TimeSpan totalDuration, double pixelThreshold = 0.1, double durationThreshold = 0.1, CancellationToken cancellationToken = default)
    {
        var cachedIntervals = GetCachedBlackIntervals(videoPath);
        if (cachedIntervals != null)
        {
            return cachedIntervals;
        }

        var blackIntervals = new List<BlackInterval>();
        
        if (totalDuration.TotalMinutes < 2)
        {
            return blackIntervals;
        }
        
        var sampleSegments = GetSampleSegments(totalDuration);
        
        foreach (var segment in sampleSegments)
        {
            var segmentBlackIntervals = await DetectBlackInSegmentAsync(videoPath, segment.Start, segment.Duration, pixelThreshold, durationThreshold, cancellationToken).ConfigureAwait(false);
            blackIntervals.AddRange(segmentBlackIntervals);
        }

        CacheBlackIntervals(videoPath, blackIntervals);
        return blackIntervals;
    }

    // MARK: DetectBlackScenesParallelAsync
    public async Task<List<BlackInterval>> DetectBlackScenesParallelAsync(string videoPath, TimeSpan totalDuration, double pixelThreshold = 0.1, double durationThreshold = 0.1, CancellationToken cancellationToken = default)
    {
        var cachedIntervals = GetCachedBlackIntervals(videoPath);
        if (cachedIntervals != null)
        {
            return cachedIntervals;
        }

        var blackIntervals = new List<BlackInterval>();

        if (totalDuration.TotalMinutes < 2)
        {
            return blackIntervals;
        }

        var sampleSegments = GetSampleSegments(totalDuration);

        // Process segments in parallel
        var tasks = sampleSegments.Select(segment =>
            DetectBlackInSegmentAsync(videoPath, segment.Start, segment.Duration, pixelThreshold, durationThreshold, cancellationToken)
        ).ToArray();

        var segmentResults = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Combine results
        foreach (var segmentIntervals in segmentResults)
        {
            blackIntervals.AddRange(segmentIntervals);
        }

        CacheBlackIntervals(videoPath, blackIntervals);
        return blackIntervals;
    }

    // MARK: DetectBlackInSegmentAsync
    private async Task<List<BlackInterval>> DetectBlackInSegmentAsync(string videoPath, TimeSpan startTime, TimeSpan duration, double pixelThreshold, double durationThreshold, CancellationToken cancellationToken)
    {
        var blackIntervals = new List<BlackInterval>();
        var startSeconds = startTime.TotalSeconds;
        var durationSeconds = duration.TotalSeconds;

        var codec = await GetVideoCodecAsync(videoPath, cancellationToken).ConfigureAwait(false);

        bool shouldUseSoftware = false;
        lock (_failedCodecCacheLock)
        {
            shouldUseSoftware = _failedHwaccelCodecs.Contains(codec);
        }

        if (shouldUseSoftware || string.IsNullOrEmpty(_hardwareAccelerationArgs))
        {
            var softwareArguments = $"-ss {startSeconds:F2} -t {durationSeconds:F2} -i \"{videoPath}\" -vf \"scale=320:240,blackdetect=d={durationThreshold}:pix_th={pixelThreshold}\" -an -f null - -v info {_threadingArgs}";

            try
            {
                var output = await ExecuteFFmpegAsync(softwareArguments, cancellationToken).ConfigureAwait(false);
                var segmentIntervals = ParseBlackDetectOutput(output);

                foreach (var interval in segmentIntervals)
                {
                    blackIntervals.Add(new BlackInterval
                    {
                        Start = interval.Start.Add(startTime),
                        End = interval.End.Add(startTime),
                        Duration = interval.Duration
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Software black scene detection failed for segment {Start}-{End} in {VideoPath}", startTime, startTime.Add(duration), videoPath);
            }

            return blackIntervals;
        }

        var arguments = $"-ss {startSeconds:F2} -t {durationSeconds:F2} {_hardwareAccelerationArgs} -i \"{videoPath}\" -vf \"scale=320:240,blackdetect=d={durationThreshold}:pix_th={pixelThreshold}\" -an -f null - -v info {_threadingArgs}";

        try
        {
            var output = await ExecuteFFmpegAsync(arguments, cancellationToken).ConfigureAwait(false);
            var segmentIntervals = ParseBlackDetectOutput(output);

            foreach (var interval in segmentIntervals)
            {
                blackIntervals.Add(new BlackInterval
                {
                    Start = interval.Start.Add(startTime),
                    End = interval.End.Add(startTime),
                    Duration = interval.Duration
                });
            }

            return blackIntervals;
        }
        catch (Exception ex)
        {
            bool shouldLogWarning = false;
            lock (_failedCodecCacheLock)
            {
                shouldLogWarning = _failedHwaccelCodecs.Add(codec);
            }

            if (shouldLogWarning)
            {
                _logger.LogWarning(ex, "Hardware acceleration failed for {Codec} codec during black detection, falling back to software", codec);
            }
        }

        var fallbackArguments = $"-ss {startSeconds:F2} -t {durationSeconds:F2} -i \"{videoPath}\" -vf \"scale=320:240,blackdetect=d={durationThreshold}:pix_th={pixelThreshold}\" -an -f null - -v info {_threadingArgs}";

        try
        {
            var output = await ExecuteFFmpegAsync(fallbackArguments, cancellationToken).ConfigureAwait(false);
            var segmentIntervals = ParseBlackDetectOutput(output);

            foreach (var interval in segmentIntervals)
            {
                blackIntervals.Add(new BlackInterval
                {
                    Start = interval.Start.Add(startTime),
                    End = interval.End.Add(startTime),
                    Duration = interval.Duration
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Software fallback black scene detection also failed for segment {Start}-{End} in {VideoPath}", startTime, startTime.Add(duration), videoPath);
        }

        return blackIntervals;
    }

    // MARK: GetSampleSegments
    private List<(TimeSpan Start, TimeSpan Duration)> GetSampleSegments(TimeSpan totalDuration)
    {
        var segments = new List<(TimeSpan Start, TimeSpan Duration)>();
        var sampleDuration = TimeSpan.FromSeconds(30);
        
        var positions = new[] { 0.05, 0.25, 0.5, 0.75, 0.9 };
        
        foreach (var position in positions)
        {
            var startTime = TimeSpan.FromSeconds(totalDuration.TotalSeconds * position);
            
            var remainingTime = totalDuration.Subtract(startTime);
            var segmentDuration = remainingTime < sampleDuration ? remainingTime : sampleDuration;
            
            if (segmentDuration.TotalSeconds > 5)
            {
                segments.Add((startTime, segmentDuration));
            }
        }
        
        return segments;
    }

    // MARK: ExtractFrameAsync
    public async Task<string?> ExtractFrameAsync(string videoPath, TimeSpan timestamp, string outputPath, CancellationToken cancellationToken = default)
    {
        await _operationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        
        try
        {
            var timestampStr = $"{timestamp.Hours:D2}:{timestamp.Minutes:D2}:{timestamp.Seconds:D2}.{timestamp.Milliseconds:D3}";
            
            var (colorSpace, colorTransfer, pixelFormat) = await GetVideoColorPropertiesAsync(videoPath, cancellationToken).ConfigureAwait(false);
            var codec = await GetVideoCodecAsync(videoPath, cancellationToken).ConfigureAwait(false);
            
            var filterChain = BuildVideoFilter(colorSpace, colorTransfer, pixelFormat);
            
            bool shouldUseSoftware = false;
            lock (_failedCodecCacheLock)
            {
                shouldUseSoftware = _failedHwaccelCodecs.Contains(codec);
            }
            
            if (shouldUseSoftware || string.IsNullOrEmpty(_hardwareAccelerationArgs))
            {
                var softwareArguments = $"-ss {timestampStr} -i \"{videoPath}\" -frames:v 1 {filterChain} -q:v 1 {_threadingArgs} \"{outputPath}\"";
                
                try
                {
                    await ExecuteFFmpegAsync(softwareArguments, cancellationToken).ConfigureAwait(false);
                    
                    if (File.Exists(outputPath))
                    {
                        return outputPath;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Software frame extraction failed at {Timestamp} from {VideoPath}", timestamp, videoPath);
                }
                
                return null;
            }
            
            var arguments = $"-ss {timestampStr} {_hardwareAccelerationArgs} -i \"{videoPath}\" -frames:v 1 {filterChain} -q:v 1 {_threadingArgs} \"{outputPath}\"";

            try
            {
                await ExecuteFFmpegAsync(arguments, cancellationToken).ConfigureAwait(false);
                
                if (File.Exists(outputPath))
                {
                    return outputPath;
                }
            }
            catch (Exception ex)
            {
                bool shouldLogWarning = false;
                lock (_failedCodecCacheLock)
                {
                    shouldLogWarning = _failedHwaccelCodecs.Add(codec);
                }
                
                if (shouldLogWarning)
                {
                    _logger.LogWarning(ex, "Hardware acceleration failed for {Codec} codec, falling back to software for this codec", codec);
                }
            }

            var fallbackArguments = $"-ss {timestampStr} -i \"{videoPath}\" -frames:v 1 {filterChain} -q:v 1 {_threadingArgs} \"{outputPath}\"";
            
            try
            {
                await ExecuteFFmpegAsync(fallbackArguments, cancellationToken).ConfigureAwait(false);
                
                if (File.Exists(outputPath))
                {
                    return outputPath;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Software fallback frame extraction also failed at {Timestamp} from {VideoPath}", timestamp, videoPath);
            }

            return null;
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    // MARK: SelectRandomTimestamp
    public TimeSpan SelectRandomTimestamp(TimeSpan duration, IReadOnlyList<BlackInterval> blackIntervals)
    {
        var minTime = TimeSpan.FromSeconds(duration.TotalSeconds * 0.1);
        var maxTime = TimeSpan.FromSeconds(duration.TotalSeconds * 0.9);
        
        var maxAttempts = 50;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var randomSeconds = _random.NextDouble() * (maxTime.TotalSeconds - minTime.TotalSeconds) + minTime.TotalSeconds;
            var randomTimestamp = TimeSpan.FromSeconds(randomSeconds);
            
            if (!IsInBlackInterval(randomTimestamp, blackIntervals))
            {
                return randomTimestamp;
            }
        }
        
        var gapTimestamp = FindLargestGap(duration, blackIntervals);
        if (gapTimestamp.HasValue)
        {
            var gapStart = gapTimestamp.Value;
            var gapEnd = FindGapEnd(gapStart, duration, blackIntervals);
            var gapDuration = gapEnd - gapStart;
            
            if (gapDuration.TotalSeconds > 10)
            {
                var randomOffsetSeconds = _random.NextDouble() * gapDuration.TotalSeconds;
                return gapStart.Add(TimeSpan.FromSeconds(randomOffsetSeconds));
            }
        }
        
        return TimeSpan.FromSeconds(duration.TotalSeconds * (0.2 + _random.NextDouble() * 0.6));
    }

    // MARK: GetCachedBlackIntervals
    private List<BlackInterval>? GetCachedBlackIntervals(string videoPath)
    {
        var fileInfo = new FileInfo(videoPath);
        var cacheKey = $"{videoPath}_{fileInfo.Length}_{fileInfo.LastWriteTime.Ticks}";
        
        if (_blackIntervalCache.TryGetValue(cacheKey, out var cached))
        {
            if (DateTime.UtcNow - cached.Created < _cacheExpiry)
            {
                return cached.Intervals;
            }
            
            _blackIntervalCache.Remove(cacheKey);
        }
        
        return null;
    }

    // MARK: CacheBlackIntervals
    private void CacheBlackIntervals(string videoPath, List<BlackInterval> intervals)
    {
        var fileInfo = new FileInfo(videoPath);
        var cacheKey = $"{videoPath}_{fileInfo.Length}_{fileInfo.LastWriteTime.Ticks}";
        
        _blackIntervalCache[cacheKey] = (DateTime.UtcNow, intervals);
        
        var expiredKeys = _blackIntervalCache
            .Where(kvp => DateTime.UtcNow - kvp.Value.Created > _cacheExpiry)
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var key in expiredKeys)
        {
            _blackIntervalCache.Remove(key);
        }
    }

    // MARK: IsInBlackInterval
    private bool IsInBlackInterval(TimeSpan timestamp, IReadOnlyList<BlackInterval> blackIntervals)
    {
        return blackIntervals.Any(interval => 
            timestamp >= interval.Start && timestamp <= interval.End);
    }

    // MARK: FindLargestGap
    private TimeSpan? FindLargestGap(TimeSpan duration, IReadOnlyList<BlackInterval> blackIntervals)
    {
        if (blackIntervals.Count == 0)
        {
            return null;
        }

        var sortedIntervals = blackIntervals.OrderBy(i => i.Start).ToList();
        var largestGap = TimeSpan.Zero;
        var largestGapStart = TimeSpan.Zero;

        if (sortedIntervals[0].Start > TimeSpan.FromSeconds(10))
        {
            largestGap = sortedIntervals[0].Start;
            largestGapStart = TimeSpan.Zero;
        }

        for (int i = 0; i < sortedIntervals.Count - 1; i++)
        {
            var gapStart = sortedIntervals[i].End;
            var gapEnd = sortedIntervals[i + 1].Start;
            var gapDuration = gapEnd - gapStart;

            if (gapDuration > largestGap)
            {
                largestGap = gapDuration;
                largestGapStart = gapStart;
            }
        }

        var lastInterval = sortedIntervals.Last();
        var endGap = duration - lastInterval.End;
        if (endGap > largestGap && endGap > TimeSpan.FromSeconds(10))
        {
            largestGap = endGap;
            largestGapStart = lastInterval.End;
        }

        return largestGapStart;
    }

    // MARK: FindGapEnd
    private TimeSpan FindGapEnd(TimeSpan gapStart, TimeSpan duration, IReadOnlyList<BlackInterval> blackIntervals)
    {
        var nextInterval = blackIntervals
            .Where(interval => interval.Start > gapStart)
            .OrderBy(interval => interval.Start)
            .FirstOrDefault();
            
        return nextInterval?.Start ?? duration;
    }

    // MARK: ExecuteFFmpegAsync
    private async Task<string> ExecuteFFmpegAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return await ExecuteProcessAsync(GetFFmpegPath(), arguments, cancellationToken).ConfigureAwait(false);
    }

    // MARK: ExecuteFFprobeAsync
    private async Task<string> ExecuteFFprobeAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return await ExecuteProcessAsync(GetFFprobePath(), arguments, cancellationToken).ConfigureAwait(false);
    }

    // MARK: ExecuteProcessAsync
    private async Task<string> ExecuteProcessAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        var output = string.Empty;
        var error = string.Empty;

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                output += e.Data + Environment.NewLine;
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                error += e.Data + Environment.NewLine;
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("Process {FileName} exited with code {ExitCode}. Error: {Error}", fileName, process.ExitCode, error);
        }

        return output + error;
    }

    // MARK: ParseBlackDetectOutput
    private List<BlackInterval> ParseBlackDetectOutput(string output)
    {
        var intervals = new List<BlackInterval>();
        
        var regex = new Regex(@"black_start:(\d+\.?\d*) black_end:(\d+\.?\d*) black_duration:(\d+\.?\d*)");

        foreach (Match match in regex.Matches(output))
        {
            if (match.Groups.Count >= 4 &&
                double.TryParse(match.Groups[1].Value, out var start) &&
                double.TryParse(match.Groups[2].Value, out var end) &&
                double.TryParse(match.Groups[3].Value, out var duration))
            {
                intervals.Add(new BlackInterval
                {
                    Start = TimeSpan.FromSeconds(start),
                    End = TimeSpan.FromSeconds(end),
                    Duration = TimeSpan.FromSeconds(duration)
                });
            }
        }

        return intervals;
    }

    // MARK: Dispose
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    // MARK: Dispose
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _operationSemaphore?.Dispose();
            _disposed = true;
        }
    }
}