using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
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
    /// <summary>
    /// Scheduled task trigger for batch episode poster generation.
    /// Gets episodes from Jellyfin, filters by tracking database, passes to orchestrator.
    /// </summary>
    public class EpisodePosterGenerationTask : IScheduledTask
    {
        /// <summary>
        /// Logger for task execution monitoring
        /// </summary>
        private readonly ILogger<EpisodePosterGenerationTask> _logger;

        /// <summary>
        /// Jellyfin's library manager for episode discovery
        /// </summary>
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// Jellyfin's provider manager for image upload operations
        /// </summary>
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
            if (config == null || !config.EnableTask)
            {
                return;
            }

            var trackingService = Plugin.Instance?.TrackingService;
            var orchestrator = Plugin.Instance?.Orchestrator;

            if (trackingService == null || orchestrator == null)
            {
                _logger.LogError("Required services not available");
                return;
            }

            try
            {
                // Get all episodes from Jellyfin
                var allEpisodes = GetAllEpisodesFromJellyfin();
                
                // Filter episodes that need processing using tracking database
                var episodesToProcess = await FilterEpisodesThatNeedProcessing(allEpisodes, config, trackingService, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("{ProcessCount} items need a poster", episodesToProcess.Length);

                if (episodesToProcess.Length == 0)
                {
                    progress?.Report(100);
                    return;
                }

                // Pass array to orchestrator and let it handle everything
                var posterPaths = await orchestrator.GeneratePoster(episodesToProcess, config, cancellationToken).ConfigureAwait(false);
                
                // Upload generated posters to Jellyfin and update tracking
                await ProcessGeneratedPosters(episodesToProcess, posterPaths, config, trackingService, progress, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Episode Poster Generation task");
                throw;
            }
        }

        // MARK: GetAllEpisodesFromJellyfin
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

        // MARK: FilterEpisodesThatNeedProcessing
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

        // MARK: ProcessGeneratedPosters
        private async Task ProcessGeneratedPosters(
            Episode[] episodes,
            string[] posterPaths,
            Configuration.PluginConfiguration config,
            EpisodeTrackingService trackingService,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            int succeeded = 0;
            int failed = 0;

            for (int i = 0; i < episodes.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var episode = episodes[i];
                
                // Check if we have a poster for this episode
                if (i < posterPaths.Length && !string.IsNullOrEmpty(posterPaths[i]))
                {
                    var success = await UploadImageToJellyfinAsync(episode, posterPaths[i], cancellationToken).ConfigureAwait(false);
                    
                    if (success)
                    {
                        await trackingService.MarkEpisodeProcessedAsync(episode, config).ConfigureAwait(false);
                        succeeded++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                else
                {
                    failed++;
                }

                var progressPercentage = (double)(i + 1) / episodes.Length * 100;
                progress?.Report(progressPercentage);
            }

            _logger.LogInformation("{Succeeded} succeeded and {Failed} failed", succeeded, failed);
        }

        // MARK: UploadImageToJellyfinAsync
        private async Task<bool> UploadImageToJellyfinAsync(Episode episode, string imagePath, CancellationToken cancellationToken)
        {
            try
            {
                var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken).ConfigureAwait(false);
                using var imageStream = new MemoryStream(imageBytes);

                await _providerManager.SaveImage(
                    episode,
                    imageStream,
                    "image/jpeg",
                    ImageType.Primary,
                    null,
                    cancellationToken).ConfigureAwait(false);

                await episode.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, cancellationToken).ConfigureAwait(false);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload image for episode: {EpisodeName}", episode.Name);
                return false;
            }
        }
    }
}