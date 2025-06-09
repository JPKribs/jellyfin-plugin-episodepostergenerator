using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Controller.Entities.TV;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services;

public class FFmpegService
{
    private readonly ILogger<FFmpegService> _logger;

    public FFmpegService(ILogger<FFmpegService> logger)
    {
        _logger = logger;
    }

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

    private string GetFFprobePath()
    {
        var ffmpegPath = GetFFmpegPath();
        var directory = Path.GetDirectoryName(ffmpegPath);
        return Path.Combine(directory ?? string.Empty, "ffprobe");
    }

    public async Task<TimeSpan?> GetVideoDurationAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        var arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";
        
        try
        {
            var result = await ExecuteFFprobeAsync(arguments, cancellationToken);
            
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

    public async Task<List<BlackInterval>> DetectBlackScenesAsync(string videoPath, double pixelThreshold = 0.1, double durationThreshold = 0.1, CancellationToken cancellationToken = default)
    {
        var blackIntervals = new List<BlackInterval>();
        var arguments = $"-i \"{videoPath}\" -vf \"blackdetect=d={durationThreshold}:pix_th={pixelThreshold}\" -an -f null - -v info";

        try
        {
            var output = await ExecuteFFmpegAsync(arguments, cancellationToken);
            blackIntervals = ParseBlackDetectOutput(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect black scenes for {VideoPath}", videoPath);
        }

        return blackIntervals;
    }

    // MARK: ExtractFrameWithTextAsync
    public async Task<string?> ExtractFrameWithTextAsync(string videoPath, TimeSpan timestamp, string outputPath, Episode episode, CancellationToken cancellationToken = default)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        
        var timestampStr = $"{timestamp.Hours:D2}:{timestamp.Minutes:D2}:{timestamp.Seconds:D2}.{timestamp.Milliseconds:D3}";
        
        var episodeText = FormatEpisodeText(episode);
        var titleText = EscapeTextForFFmpeg(episode.Name ?? "Unknown Episode");
        
        var episodeFontSize = Math.Max(24, config.EpisodeFontSize);
        var titleFontSize = Math.Max(16, config.TitleFontSize);
        var textColor = GetFFmpegColor(config.TextColor);
        
        var episodeY = GetEpisodeYPosition(config.TextPosition);
        var titleY = GetTitleYPosition(config.TextPosition, episodeFontSize);
        
        var episodeDrawtext = $"drawtext=text='{episodeText}':fontsize={episodeFontSize}:fontcolor={textColor}:x=(w-text_w)/2:y={episodeY}:box=1:boxcolor=black@0.5:boxborderw=5";
        var titleDrawtext = $"drawtext=text='{titleText}':fontsize={titleFontSize}:fontcolor={textColor}:x=(w-text_w)/2:y={titleY}:box=1:boxcolor=black@0.5:boxborderw=5";
        
        var videoFilter = $"{episodeDrawtext},{titleDrawtext}";
        
        if (config.UseOverlay && !string.IsNullOrEmpty(config.OverlayImagePath))
        {
            var overlayPath = GetOverlayPath(config.OverlayImagePath);
            if (File.Exists(overlayPath))
            {
                videoFilter = $"movie='{overlayPath}'[overlay];[in][overlay]overlay=0:0[text];[text]{episodeDrawtext},{titleDrawtext}[out]";
            }
        }
        
        var arguments = $"-ss {timestampStr} -i \"{videoPath}\" -vf \"{videoFilter}\" -frames:v 1 -q:v 1 \"{outputPath}\"";

        try
        {
            _logger.LogInformation("Extracting frame with text overlay using command: {Arguments}", arguments);
            await ExecuteFFmpegAsync(arguments, cancellationToken);
            
            if (File.Exists(outputPath))
            {
                _logger.LogInformation("Successfully created frame with text overlay: {OutputPath}", outputPath);
                return outputPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract frame with text at {Timestamp} from {VideoPath}", timestamp, videoPath);
        }

        return null;
    }

    public async Task<string?> ExtractFrameAsync(string videoPath, TimeSpan timestamp, string outputPath, CancellationToken cancellationToken = default)
    {
        var timestampStr = $"{timestamp.Hours:D2}:{timestamp.Minutes:D2}:{timestamp.Seconds:D2}.{timestamp.Milliseconds:D3}";
        var arguments = $"-ss {timestampStr} -i \"{videoPath}\" -frames:v 1 -q:v 1 \"{outputPath}\"";

        try
        {
            await ExecuteFFmpegAsync(arguments, cancellationToken);
            
            if (File.Exists(outputPath))
            {
                return outputPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract frame at {Timestamp} from {VideoPath}", timestamp, videoPath);
        }

        return null;
    }

    // MARK: FormatEpisodeText
    private string FormatEpisodeText(Episode episode)
    {
        var seasonNumber = episode.ParentIndexNumber ?? 0;
        var episodeNumber = episode.IndexNumber ?? 0;
        return $"S{seasonNumber:D2}E{episodeNumber:D2}";
    }

    // MARK: GetOverlayPath
    private string GetOverlayPath(string fileName)
    {
        var plugin = Plugin.Instance;
        if (plugin == null) return fileName;
        
        var pluginDataPath = plugin.DataFolderPath;
        return Path.Combine(pluginDataPath, "overlays", fileName);
    }

    // MARK: GetEpisodeYPosition
    private string GetEpisodeYPosition(string positionSetting)
    {
        return positionSetting?.ToLowerInvariant() switch
        {
            "top" => "50",
            "bottomleft" => "h-120",
            "bottomright" => "h-120",
            "bottom" or _ => "h-120"
        };
    }

    // MARK: GetTitleYPosition
    private string GetTitleYPosition(string positionSetting, int episodeFontSize)
    {
        var offset = episodeFontSize + 10;
        return positionSetting?.ToLowerInvariant() switch
        {
            "top" => $"50+{offset}",
            "bottomleft" => $"h-120+{offset}",
            "bottomright" => $"h-120+{offset}",
            "bottom" or _ => $"h-120+{offset}"
        };
    }

    private string EscapeTextForFFmpeg(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "Unknown Episode";
            
        return text
            .Replace("'", "\\'")
            .Replace(":", "\\:")
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace("[", "\\[")
            .Replace("]", "\\]");
    }

    private string GetFFmpegColor(string colorSetting)
    {
        return colorSetting?.ToLowerInvariant() switch
        {
            "black" => "black",
            "white" => "white",
            "yellow" => "yellow",
            "red" => "red",
            "blue" => "blue",
            "green" => "green",
            _ => "white"
        };
    }

    private string GetFFmpegPosition(string positionSetting)
    {
        return positionSetting?.ToLowerInvariant() switch
        {
            "top" => "x=(w-text_w)/2:y=50",
            "bottomleft" => "x=50:y=h-text_h-50",
            "bottomright" => "x=w-text_w-50:y=h-text_h-50",
            "bottom" or _ => "x=(w-text_w)/2:y=h-text_h-50"
        };
    }

    private async Task<string> ExecuteFFmpegAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return await ExecuteProcessAsync(GetFFmpegPath(), arguments, cancellationToken);
    }

    private async Task<string> ExecuteFFprobeAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return await ExecuteProcessAsync(GetFFprobePath(), arguments, cancellationToken);
    }

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

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("Process {FileName} exited with code {ExitCode}. Error: {Error}", fileName, process.ExitCode, error);
        }

        return output + error;
    }

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