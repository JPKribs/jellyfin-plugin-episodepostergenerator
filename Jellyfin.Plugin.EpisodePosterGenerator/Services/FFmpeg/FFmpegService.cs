using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    public class FFmpegService
    {
        private const int MaxRetries = 30;
        private const int EarlyExitAttemptThreshold = 5;
        private const double BrightnessThreshold = 0.05;
        private const double SharpnessThreshold = 100.0;
        private const double EarlyExitScoreThreshold = 0.6;
        private const double DefaultDurationSeconds = 3600;
        private const double DefaultSeekStartPercent = 0.2;
        private const double DefaultSeekEndPercent = 0.8;
        private const double BrightnessWeight = 0.5;
        private const int AnalysisSize = 200;

        private readonly ILogger<FFmpegService> _logger;
        private readonly IMediaEncoder _mediaEncoder;
        private readonly BrightnessService _brightnessService;

        // Constructor
        // Initializes FFmpeg service with Jellyfin's media encoder for frame extraction.
        public FFmpegService(
            ILogger<FFmpegService> logger,
            IMediaEncoder mediaEncoder,
            BrightnessService brightnessService)
        {
            _logger = logger;
            _mediaEncoder = mediaEncoder;
            _brightnessService = brightnessService;
        }

        // ExtractSceneAsync
        // Extracts a high-quality frame from video using Jellyfin's built-in image extractor.
        public async Task<string?> ExtractSceneAsync(
            Episode episode,
            PosterSettings config,
            CancellationToken cancellationToken = default)
        {
            if (episode == null || string.IsNullOrEmpty(episode.Path))
            {
                _logger.LogError("Invalid episode provided to FFmpegService");
                return null;
            }

            var mediaSources = episode.GetMediaSources(false);
            var mediaSource = mediaSources?.Count > 0 ? mediaSources[0] : null;
            if (mediaSource == null)
            {
                _logger.LogError("No media source found for episode: {Path}", episode.Path);
                return null;
            }

            var videoStream = mediaSource.MediaStreams?
                .FirstOrDefault(s => s.Type == MediaStreamType.Video);
            if (videoStream == null)
            {
                _logger.LogError("No video stream found for episode: {Path}", episode.Path);
                return null;
            }

            var container = Path.GetExtension(episode.Path)?.TrimStart('.') ?? string.Empty;
            var videoDurationSeconds = (episode.RunTimeTicks ?? 0) / (double)TimeSpan.TicksPerSecond;
            if (videoDurationSeconds <= 0) videoDurationSeconds = DefaultDurationSeconds;

            _logger.LogInformation("Extracting frame from {Path} (duration: {Duration}s, container: {Container})",
                episode.Path, (int)videoDurationSeconds, container);

            string? bestFramePath = null;
            double bestQualityScore = 0.0;

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                string? extractedPath = null;
                bool keepFile = false;

                try
                {
                    var seekSeconds = GenerateSeekTime(videoDurationSeconds, attempt, config);
                    var offset = TimeSpan.FromSeconds(seekSeconds);

                    extractedPath = await _mediaEncoder.ExtractVideoImage(
                        episode.Path,
                        container,
                        mediaSource,
                        videoStream,
                        null,
                        offset,
                        cancellationToken).ConfigureAwait(false);

                    if (string.IsNullOrEmpty(extractedPath) || !File.Exists(extractedPath))
                    {
                        if (attempt == 0)
                        {
                            _logger.LogWarning("ExtractVideoImage returned no output on first attempt");
                        }
                        continue;
                    }

                    using var stream = File.OpenRead(extractedPath);
                    using var frameBitmap = SKBitmap.Decode(stream);
                    if (frameBitmap == null)
                    {
                        continue;
                    }

                    using var analysisBitmap = CreateAnalysisBitmap(frameBitmap);
                    var brightness = analysisBitmap != null ? GetFrameBrightness(analysisBitmap) : 0.0;
                    var sharpness = analysisBitmap != null ? GetFrameSharpness(analysisBitmap) : 0.0;
                    var qualityScore = CalculateQualityScore(brightness, sharpness);

                    var brightnessOk = brightness > BrightnessThreshold;
                    var sharpnessOk = sharpness >= SharpnessThreshold;

                    if (attempt < EarlyExitAttemptThreshold || (brightnessOk && sharpnessOk))
                    {
                        _logger.LogDebug("Attempt {Attempt}: Brightness {Brightness:F3}, Sharpness {Sharpness:F1}, Score {Score:F3}",
                            attempt + 1, brightness, sharpness, qualityScore);
                    }

                    if (brightnessOk && sharpnessOk)
                    {
                        TryDeleteFile(bestFramePath);
                        keepFile = true;
                        _logger.LogInformation("Found high-quality frame on attempt {Attempt} (brightness: {Brightness:F3}, sharpness: {Sharpness:F1})",
                            attempt + 1, brightness, sharpness);
                        return extractedPath;
                    }

                    if (qualityScore > bestQualityScore)
                    {
                        TryDeleteFile(bestFramePath);
                        bestFramePath = extractedPath;
                        keepFile = true;
                        bestQualityScore = qualityScore;
                    }

                    if (qualityScore > EarlyExitScoreThreshold && attempt > EarlyExitAttemptThreshold)
                    {
                        _logger.LogInformation("Found acceptable frame after {Attempts} attempts (score: {Score:F3})",
                            attempt + 1, qualityScore);
                        break;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Frame extraction failed on attempt {Attempt}", attempt + 1);
                    if (attempt < 3)
                    {
                        continue;
                    }
                    break;
                }
                finally
                {
                    if (!keepFile)
                    {
                        TryDeleteFile(extractedPath);
                    }
                }
            }

            if (bestFramePath != null)
            {
                _logger.LogInformation("Using best available frame (score: {Score:F3})", bestQualityScore);
                return bestFramePath;
            }

            _logger.LogError("Failed to extract any usable frames after {Attempts} attempts", MaxRetries);
            return null;
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

        // CreateAnalysisBitmap
        // Creates a small downscaled copy of a bitmap for fast quality analysis.
        private static SKBitmap? CreateAnalysisBitmap(SKBitmap source)
        {
            if (source == null) return null;

            float scale = Math.Min((float)AnalysisSize / source.Width, (float)AnalysisSize / source.Height);
            int newWidth = Math.Max(1, (int)(source.Width * scale));
            int newHeight = Math.Max(1, (int)(source.Height * scale));

            var resized = new SKBitmap(newWidth, newHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(resized);
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.Low };
            canvas.DrawBitmap(source, SKRect.Create(newWidth, newHeight), paint);

            return resized;
        }

        // GetFrameBrightness
        // Calculates average brightness from a pre-downscaled analysis bitmap.
        private static double GetFrameBrightness(SKBitmap analysis)
        {
            var pixels = analysis.Pixels;
            if (pixels == null || pixels.Length == 0) return 0.0;

            double total = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                total += (0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue) / 255.0;
            }

            return total / pixels.Length;
        }

        // GetFrameSharpness
        // Calculates sharpness using Laplacian variance from a pre-downscaled analysis bitmap.
        private static double GetFrameSharpness(SKBitmap analysis)
        {
            int width = analysis.Width;
            int height = analysis.Height;
            var pixels = analysis.Pixels;
            if (pixels == null || pixels.Length == 0) return 0.0;

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

        // CalculateQualityScore
        // Combines brightness and sharpness into a weighted quality score.
        private static double CalculateQualityScore(double brightness, double sharpness)
        {
            double normalizedBrightness = Math.Min(brightness / BrightnessThreshold, 1.0);
            double normalizedSharpness = Math.Min(sharpness / SharpnessThreshold, 1.0);
            double sharpnessWeight = 1.0 - BrightnessWeight;

            return (normalizedBrightness * BrightnessWeight) + (normalizedSharpness * sharpnessWeight);
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
    }
}
