using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Services;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.Database;
using JPKribs.Jellyfin.Base;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator
{
    public class Plugin : PluginBase<Plugin, PluginConfiguration>, IDisposable
    {
        public override string Name => "Episode Poster Generator";
        public override Guid Id => Guid.Parse("b8715e44-6b77-4c88-9c74-2b6f4c7b9a1e");
        public override string Description => "Automatically generates episode poster cards with titles overlaid on representative frames from video files.";

        private readonly ILogger<Plugin> _logger;
        private readonly EpisodeTrackingService _trackingService;
        private readonly EpisodeTrackingDatabase _trackingDatabase;
        private readonly PosterService _posterService;
        private readonly PosterConfigurationService _posterConfigService;
        private readonly Services.Posters.PreviewService _previewService;

        private readonly SemaphoreSlim _dbInitGate = new SemaphoreSlim(0, 1);
        private bool _dbInitialized;
        private bool _disposed;

        // Plugin
        // Initializes the plugin with all required services and dependencies.
        public Plugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            ILogger<Plugin> logger,
            ILoggerFactory loggerFactory,
            IServerConfigurationManager configurationManager,
            IMediaEncoder mediaEncoder)
            : base(applicationPaths, xmlSerializer)
        {
            _logger = logger;

            _trackingDatabase = new EpisodeTrackingDatabase(
                loggerFactory.CreateLogger<EpisodeTrackingDatabase>(),
                applicationPaths);
            _trackingService = new EpisodeTrackingService(
                loggerFactory.CreateLogger<EpisodeTrackingService>(),
                _trackingDatabase,
                new ConfigurationHashService());

            _posterConfigService = new PosterConfigurationService(
                loggerFactory.CreateLogger<PosterConfigurationService>());
            _posterConfigService.Initialize(Configuration);

            var brightnessService = new BrightnessService(
                loggerFactory.CreateLogger<BrightnessService>());
            var frameExtractionService = new FrameExtractionService(
                loggerFactory.CreateLogger<FrameExtractionService>(),
                mediaEncoder);
            var croppingService = new CroppingService(
                loggerFactory.CreateLogger<CroppingService>());
            var canvasService = new CanvasService(
                loggerFactory.CreateLogger<CanvasService>(),
                frameExtractionService,
                croppingService,
                brightnessService);

            _posterService = new PosterService(
                loggerFactory.CreateLogger<PosterService>(),
                canvasService,
                configurationManager,
                loggerFactory);

            _previewService = new Services.Posters.PreviewService(loggerFactory);

            _ = Task.Run(async () =>
            {
                try
                {
                    await _trackingDatabase.InitializeAsync().ConfigureAwait(false);
                    _dbInitialized = true;
                    _logger.LogInformation("Episode tracking database initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize episode tracking database");
                }
                finally
                {
                    _dbInitGate.Release();
                }
            });

            _logger.LogInformation("Episode Poster Generator plugin initialized");
        }

        /// <summary>
        /// Waits for database initialization to complete. Returns true if initialization succeeded.
        /// </summary>
        public async Task<bool> WaitForDatabaseAsync(CancellationToken cancellationToken = default)
        {
            await _dbInitGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            _dbInitGate.Release(); // Allow other waiters through
            return _dbInitialized;
        }

        public EpisodeTrackingService TrackingService => _trackingService;
        public PosterService PosterService => _posterService;
        public PosterConfigurationService PosterConfigService => _posterConfigService;
        public Services.Posters.PreviewService PreviewService => _previewService;


        // GetPages
        // Returns the plugin configuration page information.
        public override IEnumerable<PluginPageInfo> GetPages()
        {
            var ns = typeof(Plugin).Namespace;

            yield return new PluginPageInfo
            {
                Name = "epg_posters",
                EmbeddedResourcePath = $"{ns}.Configuration.epg_posters.html",
                MenuSection = "plugin",
                DisplayName = "Episode Poster Generator"
            };

            yield return new PluginPageInfo
            {
                Name = "epg_posters.js",
                EmbeddedResourcePath = $"{ns}.Configuration.epg_posters.js"
            };

            yield return new PluginPageInfo
            {
                Name = "epg_settings",
                EmbeddedResourcePath = $"{ns}.Configuration.epg_settings.html"
            };

            yield return new PluginPageInfo
            {
                Name = "epg_settings.js",
                EmbeddedResourcePath = $"{ns}.Configuration.epg_settings.js"
            };

            yield return new PluginPageInfo
            {
                Name = "epg_shared.css",
                EmbeddedResourcePath = $"{ns}.Configuration.epg_shared.css"
            };

            foreach (var page in GetSharedPages("epg"))
            {
                yield return page;
            }
        }

        // Dispose
        // Releases resources used by the plugin.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Dispose
        // Releases managed resources when disposing is true.
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _dbInitGate?.Dispose();
                _trackingDatabase?.Dispose();
                _disposed = true;
            }
        }

        // UpdateConfiguration
        // Updates the configuration and reinitializes the poster configuration service.
        public override void UpdateConfiguration(BasePluginConfiguration configuration)
        {
            base.UpdateConfiguration(configuration);
            _posterConfigService?.Initialize(Configuration);
        }
    }
}
