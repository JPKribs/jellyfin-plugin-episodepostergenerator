using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Managers;
using Jellyfin.Plugin.EpisodePosterGenerator.Services;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.Database;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.Generation;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using MediaBrowser.Controller.Configuration;

namespace Jellyfin.Plugin.EpisodePosterGenerator
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IDisposable
    {
        public static Plugin? Instance { get; private set; }

        private readonly ILogger<Plugin> _logger;
        private readonly IMediaEncoder _mediaEncoder;
        private readonly ILibraryManager _libraryManager;
        private readonly IApplicationPaths _applicationPaths;
        private readonly ILoggerFactory _loggerFactory;
        private readonly FFmpegManager _ffmpegManager;
        private readonly PosterGeneratorService _posterGeneratorService;
        private readonly EpisodeTrackingService _trackingService;
        private readonly GenerationManager _manager;
        private readonly EpisodeTrackingDatabase _trackingDatabase;
        private bool _disposed;

        // MARK: Constructor
        public Plugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            IMediaEncoder mediaEncoder,
            ILibraryManager libraryManager,
            ILogger<Plugin> logger,
            ILoggerFactory loggerFactory,
            IServerConfigurationManager configurationManager)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;

            _logger = logger;
            _mediaEncoder = mediaEncoder;
            _libraryManager = libraryManager;
            _applicationPaths = applicationPaths;
            _loggerFactory = loggerFactory;

            // Initialize tracking services first
            _trackingDatabase = new EpisodeTrackingDatabase(loggerFactory.CreateLogger<EpisodeTrackingDatabase>(), applicationPaths);
            _trackingService = new EpisodeTrackingService(loggerFactory.CreateLogger<EpisodeTrackingService>(), _trackingDatabase);

            // Initialize FFmpeg Manager (coordinates your existing FFmpeg services)
            _ffmpegManager = new FFmpegManager(
                loggerFactory.CreateLogger<FFmpegManager>(),
                loggerFactory.CreateLogger<Services.FFmpegService>(),
                mediaEncoder,
                configurationManager);

            // Initialize poster generation service
            _posterGeneratorService = new PosterGeneratorService();

            // Initialize generation manager with FFmpegManager
            _manager = new GenerationManager(
                loggerFactory.CreateLogger<GenerationManager>(),
                _posterGeneratorService,
                _ffmpegManager,
                configurationManager,
                null, // IProviderManager - add if available
                _trackingService);

            // Initialize database
            _ = Task.Run(async () =>
            {
                try
                {
                    await _trackingDatabase.InitializeAsync().ConfigureAwait(false);
                    _logger.LogInformation("Episode tracking database initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize episode tracking database");
                }
            });

            _logger.LogInformation("Episode Poster Generator plugin initialized");
        }

        public IMediaEncoder MediaEncoder => _mediaEncoder;
        public ILibraryManager LibraryManager => _libraryManager;
        public ILoggerFactory LoggerFactory => _loggerFactory;
        public FFmpegManager FFmpegManager => _ffmpegManager;
        public PosterGeneratorService PosterGeneratorService => _posterGeneratorService;
        public EpisodeTrackingService TrackingService => _trackingService;
        public GenerationManager Manager => _manager;
        public EpisodeTrackingDatabase TrackingDatabase => _trackingDatabase;

        public IEnumerable<PluginPageInfo> GetPages()
        {
            yield return new PluginPageInfo
            {
                Name = "Episode Poster Generator",
                EmbeddedResourcePath = typeof(Plugin).Namespace + ".Configuration.configPage.html"
            };
        }

        public Stream GetImage()
        {
            var assembly = GetType().Assembly;
            var resourceNames = assembly.GetManifestResourceNames();
            _logger.LogError("Available resources: {Resources}", string.Join(", ", resourceNames));
            
            return assembly.GetManifestResourceStream(typeof(Plugin).Namespace + ".Logo.png")!;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _trackingDatabase?.Dispose();
                _ffmpegManager?.Dispose();
                _disposed = true;
            }
        }
    }
}