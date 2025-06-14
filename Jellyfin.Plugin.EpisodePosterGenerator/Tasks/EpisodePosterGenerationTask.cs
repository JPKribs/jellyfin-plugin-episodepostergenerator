using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Tasks
{
    public class EpisodePosterGenerationTask : IScheduledTask
    {
        private readonly ILogger<EpisodePosterGenerationTask> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IProviderManager _providerManager;

        // MARK: Constructor
        public EpisodePosterGenerationTask(
            ILogger<EpisodePosterGenerationTask> logger,
            ILibraryManager libraryManager,
            IProviderManager providerManager)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _providerManager = providerManager;
        }

        // MARK: Properties
        public string Name => "Generate Episode Posters";

        public string Description => "Generates poster images for episodes that don't have them or need updating";

        public string Category => "Library";

        public string Key => "EpisodePosterGeneration";

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        // MARK: GetDefaultTriggers
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }

        // MARK: ExecuteAsync
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null || !config.EnablePlugin)
            {
                _logger.LogInformation("Episode Poster Generator is disabled, skipping task");
                return;
            }

            var trackingService = Plugin.Instance?.TrackingService;
            if (trackingService == null)
            {
                _logger.LogError("Tracking service not available");
                return;
            }

            _logger.LogInformation("Starting Episode Poster Generation task");

            try
            {
                var allEpisodes = GetAllEpisodes();
                var episodesToProcess = new List<Episode>();

                _logger.LogInformation("Found {TotalCount} episodes, checking which need processing", allEpisodes.Count);

                foreach (var episode in allEpisodes)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (trackingService.ShouldProcessEpisode(episode, config))
                    {
                        episodesToProcess.Add(episode);
                    }
                }

                _logger.LogInformation("Found {ProcessCount} episodes that need processing", episodesToProcess.Count);

                if (episodesToProcess.Count == 0)
                {
                    _logger.LogInformation("No episodes need processing");
                    progress?.Report(100);
                    return;
                }

                await ProcessEpisodesAsync(episodesToProcess, config, trackingService, progress, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Episode Poster Generation task completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Episode Poster Generation task");
                throw;
            }
        }

        // MARK: GetAllEpisodes
        private List<Episode> GetAllEpisodes()
        {
            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Episode },
                IsVirtualItem = false,
                Recursive = true
            };

            var items = _libraryManager.GetItemList(query);
            return items.OfType<Episode>()
                       .Where(e => !string.IsNullOrEmpty(e.Path) && File.Exists(e.Path))
                       .ToList();
        }

        // MARK: ProcessEpisodesAsync
        private async Task ProcessEpisodesAsync(
            List<Episode> episodes, 
            Configuration.PluginConfiguration config,
            EpisodeTrackingService trackingService,
            IProgress<double> progress, 
            CancellationToken cancellationToken)
        {
            var ffmpegService = Plugin.Instance?.FFmpegService;
            var posterService = Plugin.Instance?.PosterGeneratorService;

            if (ffmpegService == null || posterService == null)
            {
                _logger.LogError("Plugin services not available");
                return;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "episodeposter_batch");
            Directory.CreateDirectory(tempDir);

            int processed = 0;
            int succeeded = 0;
            int failed = 0;

            try
            {
                foreach (var episode in episodes)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        _logger.LogDebug("Processing episode: {EpisodeName} (S{Season}E{Episode})", 
                            episode.Name, episode.ParentIndexNumber, episode.IndexNumber);

                        var success = await ProcessSingleEpisodeAsync(episode, config, ffmpegService, posterService, tempDir, cancellationToken).ConfigureAwait(false);

                        if (success)
                        {
                            await trackingService.MarkEpisodeProcessedAsync(episode, config).ConfigureAwait(false);
                            succeeded++;
                            _logger.LogDebug("Successfully processed episode: {EpisodeName}", episode.Name);
                        }
                        else
                        {
                            failed++;
                            _logger.LogWarning("Failed to process episode: {EpisodeName}", episode.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogError(ex, "Error processing episode: {EpisodeName}", episode.Name);
                    }

                    processed++;
                    var progressPercentage = (double)processed / episodes.Count * 100;
                    progress?.Report(progressPercentage);

                    if (processed % 10 == 0)
                    {
                        _logger.LogInformation("Progress: {Processed}/{Total} episodes processed ({Succeeded} succeeded, {Failed} failed)", 
                            processed, episodes.Count, succeeded, failed);
                    }
                }

                _logger.LogInformation("Batch processing completed: {Processed} total, {Succeeded} succeeded, {Failed} failed", 
                    processed, succeeded, failed);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temp directory: {TempDir}", tempDir);
                }
            }
        }

        // MARK: ProcessSingleEpisodeAsync
        private async Task<bool> ProcessSingleEpisodeAsync(
            Episode episode,
            Configuration.PluginConfiguration config,
            FFmpegService ffmpegService,
            PosterGeneratorService posterService,
            string tempDir,
            CancellationToken cancellationToken)
        {
            var tempFramePath = Path.Combine(tempDir, $"frame_{episode.Id}_{DateTime.UtcNow.Ticks}.jpg");
            var tempPosterPath = Path.Combine(tempDir, $"poster_{episode.Id}_{DateTime.UtcNow.Ticks}.jpg");

            try
            {
                string? extractedFramePath;

                if (config.PosterStyle == PosterStyle.Numeral)
                {
                    extractedFramePath = CreateTransparentImage(tempFramePath);
                }
                else
                {
                    var duration = GetDurationFromEpisode(episode);
                    if (!duration.HasValue)
                    {
                        duration = await ffmpegService.GetVideoDurationAsync(episode.Path, cancellationToken).ConfigureAwait(false);
                    }

                    if (!duration.HasValue)
                    {
                        _logger.LogWarning("Could not get video duration for: {Path}", episode.Path);
                        return false;
                    }

                    var blackIntervals = await ffmpegService.DetectBlackScenesAsync(episode.Path, duration.Value, 0.1, 0.1, cancellationToken).ConfigureAwait(false);
                    var selectedTimestamp = ffmpegService.SelectRandomTimestamp(duration.Value, blackIntervals);

                    extractedFramePath = await ffmpegService.ExtractFrameAsync(episode.Path, selectedTimestamp, tempFramePath, cancellationToken).ConfigureAwait(false);
                }

                if (extractedFramePath == null || !File.Exists(extractedFramePath))
                {
                    _logger.LogWarning("Failed to extract frame for episode: {EpisodeName}", episode.Name);
                    return false;
                }

                var processedPath = posterService.ProcessImageWithText(extractedFramePath, tempPosterPath, episode, config);
                if (processedPath == null || !File.Exists(processedPath))
                {
                    _logger.LogWarning("Failed to process image for episode: {EpisodeName}", episode.Name);
                    return false;
                }

                // Upload image using Jellyfin's API
                var success = await UploadImageToJellyfinAsync(episode, processedPath, cancellationToken).ConfigureAwait(false);
                
                if (success)
                {
                    _logger.LogInformation("Successfully uploaded poster for episode: {EpisodeName}", episode.Name);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to upload poster for episode: {EpisodeName}", episode.Name);
                    return false;
                }
            }
            finally
            {
                CleanupTempFile(tempFramePath);
                CleanupTempFile(tempPosterPath);
            }
        }

        // MARK: GetDurationFromEpisode
        private TimeSpan? GetDurationFromEpisode(Episode episode)
        {
            if (episode.RunTimeTicks.HasValue)
            {
                return TimeSpan.FromTicks(episode.RunTimeTicks.Value);
            }
            
            return null;
        }

        // MARK: CreateTransparentImage
        private string? CreateTransparentImage(string outputPath)
        {
            try
            {
                using var bitmap = new SKBitmap(3000, 2000);
                using var canvas = new SKCanvas(bitmap);

                canvas.Clear(SKColors.Transparent);

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
                using var outputStream = File.OpenWrite(outputPath);
                data.SaveTo(outputStream);

                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create transparent image");
                return null;
            }
        }

        // MARK: UploadImageToJellyfinAsync
        private async Task<bool> UploadImageToJellyfinAsync(Episode episode, string imagePath, CancellationToken cancellationToken)
        {
            try
            {
                var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken).ConfigureAwait(false);
                using var imageStream = new MemoryStream(imageBytes);

                // Save the image using Jellyfin's provider manager
                await _providerManager.SaveImage(
                    episode,
                    imageStream,
                    "image/jpeg",
                    ImageType.Primary,
                    null,
                    cancellationToken).ConfigureAwait(false);

                // Update the episode metadata to trigger UI refresh
                await episode.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, cancellationToken).ConfigureAwait(false);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload image for episode: {EpisodeName}", episode.Name);
                return false;
            }
        }

        // MARK: CleanupTempFile
        private void CleanupTempFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temp file: {FilePath}", filePath);
            }
        }
    }
}