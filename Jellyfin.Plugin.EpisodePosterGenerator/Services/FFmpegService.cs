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
/// Provides methods to interact with FFmpeg and FFprobe for video processing tasks.
/// </summary>
public class FFmpegService
{
    private readonly ILogger<FFmpegService> _logger;
    private readonly IMediaEncoder _mediaEncoder;
    private static readonly Dictionary<string, (DateTime Created, List<BlackInterval> Intervals)> _blackIntervalCache = new();
    private static readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(24);
    private static readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FFmpegService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for logging events.</param>
    /// <param name="mediaEncoder">Jellyfin's media encoder service.</param>
    public FFmpegService(ILogger<FFmpegService> logger, IMediaEncoder mediaEncoder)
    {
        _logger = logger;
        _mediaEncoder = mediaEncoder;
    }

    // MARK: GetFFmpegPath
    /// <summary>
    /// Gets the path to the FFmpeg executable from Jellyfin's media encoder.
    /// </summary>
    private string GetFFmpegPath()
    {
        var path = _mediaEncoder.EncoderPath;
        if (string.IsNullOrEmpty(path))
        {
            _logger.LogError("FFmpeg path not available from media encoder");
            return "ffmpeg";
        }

        _logger.LogDebug("Using FFmpeg path: {FFmpegPath}", path);
        return path;
    }

    // MARK: GetFFprobePath
    /// <summary>
    /// Gets the path to the FFprobe executable from Jellyfin's media encoder.
    /// </summary>
    private string GetFFprobePath()
    {
        var path = _mediaEncoder.ProbePath;
        if (string.IsNullOrEmpty(path))
        {
            _logger.LogError("FFprobe path not available from media encoder");
            return "ffprobe";
        }

        return path;
    }

    // MARK: GetVideoDurationAsync
    /// <summary>
    /// Asynchronously retrieves the duration of a video file using FFprobe.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The duration as a <see cref="TimeSpan"/> or null if failed.</returns>
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
    /// <summary>
    /// Detects black scenes in a video asynchronously using optimized segmented analysis.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <param name="totalDuration">Total duration of the video.</param>
    /// <param name="pixelThreshold">Pixel luminance threshold to detect black frames.</param>
    /// <param name="durationThreshold">Minimum duration in seconds for black scenes.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A list of black scene intervals detected in the video.</returns>
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
            _logger.LogDebug("Skipping black detection for short video: {Duration}", totalDuration);
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

    // MARK: DetectBlackInSegmentAsync
    /// <summary>
    /// Detects black scenes within a specific segment of a video.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <param name="startTime">Start time of the segment to analyze.</param>
    /// <param name="duration">Duration of the segment to analyze.</param>
    /// <param name="pixelThreshold">Pixel luminance threshold to detect black frames.</param>
    /// <param name="durationThreshold">Minimum duration in seconds for black scenes.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A list of black scene intervals detected in the segment.</returns>
    private async Task<List<BlackInterval>> DetectBlackInSegmentAsync(string videoPath, TimeSpan startTime, TimeSpan duration, double pixelThreshold, double durationThreshold, CancellationToken cancellationToken)
    {
        var blackIntervals = new List<BlackInterval>();
        var startSeconds = startTime.TotalSeconds;
        var durationSeconds = duration.TotalSeconds;
        
        var hwaccelArgs = GetHardwareAccelerationArgs();
        var arguments = $"-ss {startSeconds:F2} -t {durationSeconds:F2} {hwaccelArgs} -i \"{videoPath}\" -vf \"scale=320:240,blackdetect=d={durationThreshold}:pix_th={pixelThreshold}\" -an -f null - -v info";

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect black scenes in segment {Start}-{End} for {VideoPath}", startTime, startTime.Add(duration), videoPath);
        }

        return blackIntervals;
    }

    // MARK: GetSampleSegments
    /// <summary>
    /// Gets strategic sample segments for black scene detection analysis.
    /// </summary>
    /// <param name="totalDuration">Total duration of the video.</param>
    /// <returns>A list of sample segments with start time and duration.</returns>
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
    /// <summary>
    /// Extracts a single video frame at a specified timestamp asynchronously with full source quality.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <param name="timestamp">Timestamp to extract the frame from.</param>
    /// <param name="outputPath">Path to save the extracted frame image.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The output path if extraction succeeded; otherwise, null.</returns>
    public async Task<string?> ExtractFrameAsync(string videoPath, TimeSpan timestamp, string outputPath, CancellationToken cancellationToken = default)
    {
        var timestampStr = $"{timestamp.Hours:D2}:{timestamp.Minutes:D2}:{timestamp.Seconds:D2}.{timestamp.Milliseconds:D3}";
        
        var hwaccelArgs = GetHardwareAccelerationArgs();
        var arguments = $"-ss {timestampStr} {hwaccelArgs} -i \"{videoPath}\" -frames:v 1 -q:v 1 \"{outputPath}\"";

        try
        {
            await ExecuteFFmpegAsync(arguments, cancellationToken).ConfigureAwait(false);
            
            if (File.Exists(outputPath))
            {
                _logger.LogDebug("Successfully extracted frame to: {OutputPath}", outputPath);
                return outputPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract frame at {Timestamp} from {VideoPath}", timestamp, videoPath);
        }

        return null;
    }

    // MARK: GetHardwareAccelerationArgs
    /// <summary>
    /// Gets hardware acceleration arguments using Jellyfin's media encoder capabilities.
    /// </summary>
    /// <returns>Hardware acceleration arguments string or empty if not supported.</returns>
    private string GetHardwareAccelerationArgs()
    {
        try
        {
            if (_mediaEncoder.SupportsHwaccel("vaapi"))
            {
                return "-hwaccel vaapi";
            }
            
            if (_mediaEncoder.SupportsHwaccel("qsv"))
            {
                return "-hwaccel qsv";
            }
            
            if (_mediaEncoder.SupportsHwaccel("cuda"))
            {
                return "-hwaccel cuda";
            }
            
            if (_mediaEncoder.SupportsHwaccel("d3d11va"))
            {
                return "-hwaccel d3d11va";
            }
            
            if (_mediaEncoder.SupportsHwaccel("videotoolbox"))
            {
                return "-hwaccel videotoolbox";
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not determine hardware acceleration support, using software decoding");
            return string.Empty;
        }
    }

    // MARK: SelectRandomTimestamp
    /// <summary>
    /// Selects a random timestamp for frame extraction, avoiding black intervals and credits.
    /// </summary>
    /// <param name="duration">Total duration of the video.</param>
    /// <param name="blackIntervals">List of detected black scene intervals.</param>
    /// <returns>A random timestamp suitable for frame extraction.</returns>
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
    /// <summary>
    /// Retrieves cached black intervals for a video file if available and not expired.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <returns>Cached black intervals or null if not available or expired.</returns>
    private List<BlackInterval>? GetCachedBlackIntervals(string videoPath)
    {
        var fileInfo = new FileInfo(videoPath);
        var cacheKey = $"{videoPath}_{fileInfo.Length}_{fileInfo.LastWriteTime.Ticks}";
        
        if (_blackIntervalCache.TryGetValue(cacheKey, out var cached))
        {
            if (DateTime.UtcNow - cached.Created < _cacheExpiry)
            {
                _logger.LogDebug("Using cached black intervals for: {VideoPath}", videoPath);
                return cached.Intervals;
            }
            
            _blackIntervalCache.Remove(cacheKey);
        }
        
        return null;
    }

    // MARK: CacheBlackIntervals
    /// <summary>
    /// Caches black intervals for a video file and cleans up expired entries.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <param name="intervals">Black intervals to cache.</param>
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
    /// <summary>
    /// Determines if a timestamp falls within any black interval.
    /// </summary>
    /// <param name="timestamp">The timestamp to check.</param>
    /// <param name="blackIntervals">List of black intervals to check against.</param>
    /// <returns>True if the timestamp is within a black interval; otherwise, false.</returns>
    private bool IsInBlackInterval(TimeSpan timestamp, IReadOnlyList<BlackInterval> blackIntervals)
    {
        return blackIntervals.Any(interval => 
            timestamp >= interval.Start && timestamp <= interval.End);
    }

    // MARK: FindLargestGap
    /// <summary>
    /// Finds the start of the largest gap between black intervals.
    /// </summary>
    /// <param name="duration">Total duration of the video.</param>
    /// <param name="blackIntervals">List of black intervals.</param>
    /// <returns>The start timestamp of the largest gap, or null if no suitable gap is found.</returns>
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
    /// <summary>
    /// Finds the end of a gap starting from a given timestamp.
    /// </summary>
    /// <param name="gapStart">Start of the gap.</param>
    /// <param name="duration">Total duration of the video.</param>
    /// <param name="blackIntervals">List of black intervals.</param>
    /// <returns>The end timestamp of the gap.</returns>
    private TimeSpan FindGapEnd(TimeSpan gapStart, TimeSpan duration, IReadOnlyList<BlackInterval> blackIntervals)
    {
        var nextInterval = blackIntervals
            .Where(interval => interval.Start > gapStart)
            .OrderBy(interval => interval.Start)
            .FirstOrDefault();
            
        return nextInterval?.Start ?? duration;
    }

    // MARK: ExecuteFFmpegAsync
    /// <summary>
    /// Executes an FFmpeg command asynchronously.
    /// </summary>
    /// <param name="arguments">Command-line arguments for FFmpeg.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Combined standard output and error from the process.</returns>
    private async Task<string> ExecuteFFmpegAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return await ExecuteProcessAsync(GetFFmpegPath(), arguments, cancellationToken).ConfigureAwait(false);
    }

    // MARK: ExecuteFFprobeAsync
    /// <summary>
    /// Executes an FFprobe command asynchronously.
    /// </summary>
    /// <param name="arguments">Command-line arguments for FFprobe.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Combined standard output and error from the process.</returns>
    private async Task<string> ExecuteFFprobeAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return await ExecuteProcessAsync(GetFFprobePath(), arguments, cancellationToken).ConfigureAwait(false);
    }

    // MARK: ExecuteProcessAsync
    /// <summary>
    /// Runs a process and collects its output asynchronously.
    /// </summary>
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
    /// <summary>
    /// Parses blackdetect filter output into black interval objects.
    /// </summary>
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
}