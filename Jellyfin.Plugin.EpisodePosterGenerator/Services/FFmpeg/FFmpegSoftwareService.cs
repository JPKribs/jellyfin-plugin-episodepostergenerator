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

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Media
{
    /// <summary>
    /// Software-based FFmpeg operations with advanced black scene detection and frame extraction
    /// </summary>
    public class FFmpegSoftwareService
    {
        private readonly ILogger<FFmpegSoftwareService> _logger;
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
        /// Number of threads for FFmpeg to use
        /// </summary>
        private readonly int _ffmpegThreads;

        /// <summary>
        /// Threading arguments for FFmpeg commands
        /// </summary>
        private readonly string _threadingArgs;

        /// <summary>
        /// Semaphore to control concurrent operations
        /// </summary>
        private readonly SemaphoreSlim _operationSemaphore;

        // MARK: Constructor
        public FFmpegSoftwareService(ILogger<FFmpegSoftwareService> logger, IMediaEncoder mediaEncoder)
        {
            _logger = logger;
            _mediaEncoder = mediaEncoder;
            
            _ffmpegThreads = Math.Max(1, Environment.ProcessorCount / 4);
            _threadingArgs = $"-threads {_ffmpegThreads}";
            
            var maxConcurrentOperations = Math.Max(1, (Environment.ProcessorCount - 2) / 2);
            _operationSemaphore = new SemaphoreSlim(maxConcurrentOperations, maxConcurrentOperations);
            
            _logger.LogInformation("Software FFmpeg service initialized with {Threads} threads", _ffmpegThreads);
        }

        // MARK: ExtractFrameAsync
        public async Task<string?> ExtractFrameAsync(
            string videoPath,
            TimeSpan timestamp,
            CancellationToken cancellationToken = default)
        {
            await _operationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            
            try
            {
                var outputPath = Path.GetTempFileName() + ".jpg";
                var timestampStr = $"{(int)timestamp.TotalHours:D2}:{timestamp.Minutes:D2}:{timestamp.Seconds:D2}.{timestamp.Milliseconds:D3}";
                
                // Build software video filter chain
                var filterChain = BuildSoftwareVideoFilter();
                var arguments = $"-ss {timestampStr} -i \"{videoPath}\" -frames:v 1 {filterChain} -q:v 1 {_threadingArgs} \"{outputPath}\"";
                
                try
                {
                    await ExecuteFFmpegAsync(arguments, cancellationToken).ConfigureAwait(false);
                    
                    if (File.Exists(outputPath))
                    {
                        _logger.LogDebug("Software frame extraction successful at {Timestamp} from {VideoPath}", timestamp, videoPath);
                        return outputPath;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Software frame extraction failed at {Timestamp} from {VideoPath}", timestamp, videoPath);
                    
                    // Clean up failed attempt
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }
                }
                
                return null;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        // MARK: GetVideoDurationAsync
        public async Task<TimeSpan?> GetVideoDurationAsync(string videoPath, CancellationToken cancellationToken = default)
        {
            var arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";
            
            try
            {
                var result = await ExecuteFFprobeAsync(arguments, cancellationToken).ConfigureAwait(false);
                
                if (double.TryParse(result.Trim(), out var durationSeconds))
                {
                    return TimeSpan.FromSeconds(durationSeconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get video duration for: {VideoPath}", videoPath);
            }
            
            return null;
        }

        // MARK: DetectBlackScenesAsync
        public async Task<BlackInterval[]> DetectBlackScenesAsync(string videoPath, CancellationToken cancellationToken = default)
        {
            // Check cache first
            lock (_blackIntervalCache)
            {
                if (_blackIntervalCache.TryGetValue(videoPath, out var cached))
                {
                    if (DateTime.UtcNow - cached.Created < _cacheExpiry)
                    {
                        return cached.Intervals.ToArray();
                    }
                    
                    _blackIntervalCache.Remove(videoPath);
                }
            }

            await _operationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            
            try
            {
                var intervals = new List<BlackInterval>();
                
                // Use blackdetect filter to find black intervals
                var arguments = $"-i \"{videoPath}\" -vf blackdetect=d=1.0:pix_th=0.05 -an -f null -";
                
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = GetFFmpegPath(),
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                var errorOutput = "";
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorOutput += e.Data + "\n";
                    }
                };

                process.Start();
                process.BeginErrorReadLine();
                
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                // Parse black intervals from stderr output
                var regex = new Regex(@"black_start:(\d+(?:\.\d+)?)\s+black_end:(\d+(?:\.\d+)?)\s+black_duration:(\d+(?:\.\d+)?)");
                var matches = regex.Matches(errorOutput);
                
                foreach (Match match in matches)
                {
                    if (double.TryParse(match.Groups[1].Value, out var start) &&
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

                // Cache the results
                lock (_blackIntervalCache)
                {
                    _blackIntervalCache[videoPath] = (DateTime.UtcNow, intervals);
                }

                _logger.LogDebug("Detected {Count} black intervals in {VideoPath}", intervals.Count, videoPath);
                
                return intervals.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to detect black scenes in: {VideoPath}", videoPath);
                return Array.Empty<BlackInterval>();
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        // MARK: ExtractSmartFrameAsync
        public async Task<string?> ExtractSmartFrameAsync(
            string videoPath,
            TimeSpan preferredTimestamp,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get video duration
                var duration = await GetVideoDurationAsync(videoPath, cancellationToken).ConfigureAwait(false);
                if (!duration.HasValue)
                {
                    return await ExtractFrameAsync(videoPath, preferredTimestamp, cancellationToken).ConfigureAwait(false);
                }

                // Detect black scenes
                var blackScenes = await DetectBlackScenesAsync(videoPath, cancellationToken).ConfigureAwait(false);
                
                // Find a good timestamp avoiding black scenes
                var smartTimestamp = FindOptimalTimestamp(preferredTimestamp, duration.Value, blackScenes);
                
                return await ExtractFrameAsync(videoPath, smartTimestamp, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Smart frame extraction failed, falling back to basic extraction");
                return await ExtractFrameAsync(videoPath, preferredTimestamp, cancellationToken).ConfigureAwait(false);
            }
        }

        // MARK: FindOptimalTimestamp
        private TimeSpan FindOptimalTimestamp(TimeSpan preferred, TimeSpan duration, BlackInterval[] blackScenes)
        {
            // If no black scenes detected, use preferred timestamp
            if (blackScenes.Length == 0)
            {
                return preferred;
            }

            // Check if preferred timestamp is in a black scene
            var isInBlackScene = blackScenes.Any(interval => 
                preferred >= interval.Start && preferred <= interval.End);

            if (!isInBlackScene)
            {
                return preferred;
            }

            // Find alternative timestamps
            var candidates = new List<TimeSpan>
            {
                TimeSpan.FromSeconds(duration.TotalSeconds * 0.25),
                TimeSpan.FromSeconds(duration.TotalSeconds * 0.5),
                TimeSpan.FromSeconds(duration.TotalSeconds * 0.75),
                TimeSpan.FromSeconds(Math.Min(30, duration.TotalSeconds * 0.1))
            };

            // Find first candidate not in a black scene
            foreach (var candidate in candidates)
            {
                var candidateInBlack = blackScenes.Any(interval => 
                    candidate >= interval.Start && candidate <= interval.End);
                
                if (!candidateInBlack)
                {
                    _logger.LogDebug("Selected alternative timestamp {Timestamp} to avoid black scene", candidate);
                    return candidate;
                }
            }

            // If all candidates are in black scenes, try random timestamps
            for (int i = 0; i < 5; i++)
            {
                var randomSeconds = _random.NextDouble() * duration.TotalSeconds;
                var randomTimestamp = TimeSpan.FromSeconds(randomSeconds);
                
                var randomInBlack = blackScenes.Any(interval => 
                    randomTimestamp >= interval.Start && randomTimestamp <= interval.End);
                
                if (!randomInBlack)
                {
                    _logger.LogDebug("Selected random timestamp {Timestamp} to avoid black scenes", randomTimestamp);
                    return randomTimestamp;
                }
            }

            // Last resort: use preferred timestamp anyway
            _logger.LogWarning("Could not find timestamp outside black scenes, using preferred timestamp");
            return preferred;
        }

        // MARK: BuildSoftwareVideoFilter
        private string BuildSoftwareVideoFilter()
        {
            var filters = new List<string>
            {
                "scale=600:900:force_original_aspect_ratio=increase",
                "crop=600:900"
            };
            
            return $"-vf \"{string.Join(",", filters)}\"";
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

        // MARK: Dispose
        public void Dispose()
        {
            _operationSemaphore?.Dispose();
        }
    }
}