using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services;

/// <summary>
/// Provides methods to interact with FFmpeg and FFprobe for video processing tasks.
/// </summary>
public class FFmpegService
{
    private readonly ILogger<FFmpegService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FFmpegService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for logging events.</param>
    public FFmpegService(ILogger<FFmpegService> logger)
    {
        _logger = logger;
    }

    // MARK: GetFFmpegPath
    /// <summary>
    /// Gets the path to the FFmpeg executable.
    /// Returns default "ffmpeg" if plugin instance is unavailable.
    /// </summary>
    private string GetFFmpegPath()
    {
        var plugin = Plugin.Instance;
        if (plugin == null)
        {
            _logger.LogError("Plugin instance not available");
            return "ffmpeg";
        }

        var path = plugin.GetFFmpegPath();
        _logger.LogDebug("Using FFmpeg path: {FFmpegPath}", path);
        return path;
    }

    // MARK: GetFFprobePath
    /// <summary>
    /// Gets the path to the FFprobe executable based on FFmpeg's directory.
    /// </summary>
    private string GetFFprobePath()
    {
        var ffmpegPath = GetFFmpegPath();
        var directory = Path.GetDirectoryName(ffmpegPath);
        return Path.Combine(directory ?? string.Empty, "ffprobe");
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
    /// Detects black scenes in a video asynchronously using FFmpeg's blackdetect filter.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <param name="pixelThreshold">Pixel luminance threshold to detect black frames.</param>
    /// <param name="durationThreshold">Minimum duration in seconds for black scenes.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A list of black scene intervals detected in the video.</returns>
    public async Task<List<BlackInterval>> DetectBlackScenesAsync(string videoPath, double pixelThreshold = 0.1, double durationThreshold = 0.1, CancellationToken cancellationToken = default)
    {
        var blackIntervals = new List<BlackInterval>();
        var arguments = $"-i \"{videoPath}\" -vf \"blackdetect=d={durationThreshold}:pix_th={pixelThreshold}\" -an -f null - -v info";

        try
        {
            var output = await ExecuteFFmpegAsync(arguments, cancellationToken).ConfigureAwait(false);
            blackIntervals = ParseBlackDetectOutput(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect black scenes for {VideoPath}", videoPath);
        }

        return blackIntervals;
    }

    // MARK: ExtractFrameAsync
    /// <summary>
    /// Extracts a single video frame at a specified timestamp asynchronously.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <param name="timestamp">Timestamp to extract the frame from.</param>
    /// <param name="outputPath">Path to save the extracted frame image.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The output path if extraction succeeded; otherwise, null.</returns>
    public async Task<string?> ExtractFrameAsync(string videoPath, TimeSpan timestamp, string outputPath, CancellationToken cancellationToken = default)
    {
        var timestampStr = $"{timestamp.Hours:D2}:{timestamp.Minutes:D2}:{timestamp.Seconds:D2}.{timestamp.Milliseconds:D3}";
        var arguments = $"-ss {timestampStr} -i \"{videoPath}\" -frames:v 1 -q:v 1 \"{outputPath}\"";

        try
        {
            await ExecuteFFmpegAsync(arguments, cancellationToken).ConfigureAwait(false);
            
            if (File.Exists(outputPath))
            {
                _logger.LogInformation("Successfully extracted frame to: {OutputPath}", outputPath);
                return outputPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract frame at {Timestamp} from {VideoPath}", timestamp, videoPath);
        }

        return null;
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