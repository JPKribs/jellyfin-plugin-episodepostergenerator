using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace Jellyfin.Plugin.EpisodePosterGenerator.Tasks
{
    public class EpisodePosterGenerationTask : IScheduledTask
    {
        private readonly ILogger<EpisodePosterGenerationTask> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IProviderManager _providerManager;

        // EpisodePosterGenerationTask
        // Initializes the scheduled task with required Jellyfin services.
        public EpisodePosterGenerationTask(
            ILogger<EpisodePosterGenerationTask> logger,
            ILibraryManager libraryManager,
            IProviderManager providerManager)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _providerManager = providerManager;
        }

        public string Name => "Generate Episode Posters";

        public string Description => "Generates poster images for episodes that don't have them or need updating";

        public string Category => "Library";

        public string Key => "EpisodePosterGeneration";

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        // GetDefaultTriggers
        // Returns an empty collection of task triggers.
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }

        // ExecuteAsync
        // Processes all episodes that need poster generation.
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            if (Plugin.Instance == null)
            {
                _logger.LogError("No instance of the Episode Poster Generator has been found");
                return;
            }

            var config = Plugin.Instance.Configuration;
            if (config == null || !config.EnableTask)
            {
                return;
            }

            // Wait for database initialization before accessing tracking service
            var dbReady = await Plugin.Instance.WaitForDatabaseAsync(cancellationToken).ConfigureAwait(false);
            if (!dbReady)
            {
                _logger.LogError("Episode tracking database failed to initialize - cannot run poster generation task");
                return;
            }

            var trackingService = Plugin.Instance.TrackingService;
            var posterService = Plugin.Instance.PosterService;

            if (trackingService == null || posterService == null)
            {
                _logger.LogError("Required services not available");
                return;
            }

            try
            {
                var allEpisodes = GetAllEpisodesFromJellyfin();

                var episodesToProcess = await FilterEpisodesThatNeedProcessing(allEpisodes, config, trackingService, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("{ProcessCount} items need a poster", episodesToProcess.Length);

                if (episodesToProcess.Length == 0)
                {
                    progress?.Report(100);
                    return;
                }

                var successCount = 0;
                var failureCount = 0;

                for (int i = 0; i < episodesToProcess.Length; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var episode = episodesToProcess[i];

                    try
                    {
                        _logger.LogDebug("Processing episode {Current}/{Total}: {SeriesName} - {EpisodeName}",
                            i + 1, episodesToProcess.Length,
                            episode.Series?.Name ?? "Unknown Series",
                            episode.Name ?? "Unknown Episode");

                        var posterPath = await posterService.GeneratePosterAsync(episode).ConfigureAwait(false);

                        if (!string.IsNullOrEmpty(posterPath) && File.Exists(posterPath))
                        {
                            try
                            {
                                var posterSettings = Plugin.Instance.PosterConfigService.GetSettingsForEpisode(episode);

                                if (posterSettings == null)
                                {
                                    _logger.LogError("Failed to get poster settings for episode");
                                    continue;
                                }

                                using var imageStream = File.OpenRead(posterPath);

                                await _providerManager.SaveImage(
                                    episode,
                                    imageStream,
                                    posterSettings.PosterFileType.GetMimeType(),
                                    ImageType.Primary,
                                    null,
                                    cancellationToken).ConfigureAwait(false);

                                await episode.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, cancellationToken).ConfigureAwait(false);

                                successCount++;

                                await trackingService.MarkEpisodeProcessedAsync(episode, config).ConfigureAwait(false);

                                _logger.LogInformation("Successfully processed and saved poster for: {SeriesName} - {EpisodeName}",
                                    episode.Series?.Name ?? "Unknown Series",
                                    episode.Name ?? "Unknown Episode");
                            }
                            catch (Exception saveEx)
                            {
                                failureCount++;
                                _logger.LogError(saveEx, "Failed to save poster to Jellyfin for: {SeriesName} - {EpisodeName}",
                                    episode.Series?.Name ?? "Unknown Series",
                                    episode.Name ?? "Unknown Episode");
                            }
                        }
                        else
                        {
                            failureCount++;
                            _logger.LogWarning("Poster path invalid or file does not exist for: {SeriesName} - {EpisodeName}",
                                episode.Series?.Name ?? "Unknown Series",
                                episode.Name ?? "Unknown Episode");
                        }
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        _logger.LogError(ex, "Error processing episode: {SeriesName} - {EpisodeName}",
                            episode.Series?.Name ?? "Unknown Series",
                            episode.Name ?? "Unknown Episode");
                    }

                    var progressPercent = (double)(i + 1) / episodesToProcess.Length * 100;
                    progress?.Report(progressPercent);
                }

                _logger.LogInformation("Poster generation completed. {SuccessCount} succeeded, {FailureCount} failed",
                    successCount, failureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Episode Poster Generation task");
                throw;
            }
        }

        // GetAllEpisodesFromJellyfin
        // Queries Jellyfin for all episodes with valid video files.
        private Episode[] GetAllEpisodesFromJellyfin()
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
                       .ToArray();
        }

        // FilterEpisodesThatNeedProcessing
        // Filters episodes based on tracking database to find those needing processing.
        private async Task<Episode[]> FilterEpisodesThatNeedProcessing(
            Episode[] allEpisodes,
            Configuration.PluginConfiguration config,
            EpisodeTrackingService trackingService,
            CancellationToken cancellationToken)
        {
            var episodesToProcess = new List<Episode>();

            foreach (var episode in allEpisodes)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (await trackingService.ShouldProcessEpisodeAsync(episode, config).ConfigureAwait(false))
                {
                    episodesToProcess.Add(episode);
                }
            }

            return episodesToProcess.ToArray();
        }
    }
}
