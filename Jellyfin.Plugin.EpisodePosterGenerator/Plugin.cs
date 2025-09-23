using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Services;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.Database;
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
    /// <summary>
    /// Episode Poster Generator plugin for automatic poster creation with video analysis
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IDisposable
    {
        /// <summary>
        /// Singleton instance for global access
        /// </summary>
        public static Plugin? Instance { get; private set; }

        /// <summary>
        /// Logger for plugin-level operations
        /// </summary>
        private readonly ILogger<Plugin> _logger;

        /// <summary>
        /// Media encoder service for video processing
        /// </summary>
        private readonly IMediaEncoder _mediaEncoder;

        /// <summary>
        /// Library manager for accessing episodes
        /// </summary>
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// Application paths for data directories
        /// </summary>
        private readonly IApplicationPaths _applicationPaths;

        /// <summary>
        /// Logger factory for service creation
        /// </summary>
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// FFmpeg service for video processing
        /// </summary>
        private readonly FFmpegService _ffmpegService;

        /// <summary>
        /// Poster generation service
        /// </summary>
        private readonly PosterGeneratorService _posterGeneratorService;

        /// <summary>
        /// Episode tracking service
        /// </summary>
        private readonly EpisodeTrackingService _trackingService;

        /// <summary>
        /// SQLite database service
        /// </summary>
        private readonly EpisodeTrackingDatabase _trackingDatabase;

        /// <summary>
        /// Disposal state flag
        /// </summary>
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

            _posterGeneratorService = new PosterGeneratorService();
            _ffmpegService = new FFmpegService(loggerFactory.CreateLogger<FFmpegService>(), mediaEncoder, configurationManager);
            _trackingDatabase = new EpisodeTrackingDatabase(loggerFactory.CreateLogger<EpisodeTrackingDatabase>(), applicationPaths);
            _trackingService = new EpisodeTrackingService(loggerFactory.CreateLogger<EpisodeTrackingService>(), _trackingDatabase);

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

            _logger.LogInformation("Episode Poster Generator plugin initialized successfully");
        }

        public override string Name => "Episode Poster Generator";
        public override Guid Id => Guid.Parse("b8715e44-6b77-4c88-9c74-2b6f4c7b9a1e");
        public override string Description => "Automatically generates episode poster cards with titles overlaid on representative frames from video files.";

        public IMediaEncoder MediaEncoder => _mediaEncoder;
        public ILibraryManager LibraryManager => _libraryManager;
        public ILoggerFactory LoggerFactory => _loggerFactory;
        public FFmpegService FFmpegService => _ffmpegService;
        public PosterGeneratorService PosterGeneratorService => _posterGeneratorService;
        public EpisodeTrackingService TrackingService => _trackingService;
        public EpisodeTrackingDatabase TrackingDatabase => _trackingDatabase;

        // MARK: GetPages
        public IEnumerable<PluginPageInfo> GetPages()
        {
            yield return new PluginPageInfo
            {
                Name = "Episode Poster Generator",
                EmbeddedResourcePath = typeof(Plugin).Namespace + ".Configuration.configPage.html"
            };
        }

        // MARK: GetImage
        public Stream GetImage()
        {
            var assembly = GetType().Assembly;
            var resourceNames = assembly.GetManifestResourceNames();
            _logger.LogError("Available resources: {Resources}", string.Join(", ", resourceNames));
            
            return assembly.GetManifestResourceStream(typeof(Plugin).Namespace + ".Logo.png")!;
        }

        // MARK: Dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // MARK: Dispose
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _trackingDatabase?.Dispose();
                _ffmpegService?.Dispose();
                _disposed = true;
            }
        }
    }
}