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

namespace Jellyfin.Plugin.EpisodePosterGenerator.Tasks
{
    /// <summary>
    /// Scheduled task trigger for batch episode poster generation.
    /// Gets episodes from Jellyfin, filters by tracking database, passes to manager.
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
            var manager = Plugin.Instance?.Manager;

            if (trackingService == null || manager == null)
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

                // Use ProcessEpisodesAsync for batch processing
                var results = await manager.ProcessEpisodesAsync(episodesToProcess, config, TaskTrigger.Task, progress, cancellationToken).ConfigureAwait(false);
                
                // Log results
                var succeeded = results.Count(r => r.Success);
                var failed = results.Length - succeeded;
                _logger.LogInformation("{Succeeded} succeeded and {Failed} failed", succeeded, failed);
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
    }
}