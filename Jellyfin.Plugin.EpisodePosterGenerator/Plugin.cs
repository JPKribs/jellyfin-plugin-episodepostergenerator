using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Services;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.Database;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IDisposable
    {
        public override string Name => "Episode Poster Generator";

        public static Plugin? Instance { get; private set; }

        private readonly ILogger<Plugin> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly EpisodeTrackingService _trackingService;
        private readonly EpisodeTrackingDatabase _trackingDatabase;
        private readonly CanvasService _canvasService;
        private readonly FFmpegService _ffmpegService;
        private readonly HardwareFFmpegService _hardwareFFmpegService;
        private readonly SoftwareFFmpegService _softwareFFmpegService;
        private readonly PosterService _posterService;
        private bool _disposed;

        // MARK: Constructor
        public Plugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            ILogger<Plugin> logger,
            ILoggerFactory loggerFactory,
            IServerConfigurationManager configurationManager)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;

            _logger = logger;
            _loggerFactory = loggerFactory;

            // Initialize tracking services
            _trackingDatabase = new EpisodeTrackingDatabase(loggerFactory.CreateLogger<EpisodeTrackingDatabase>(), applicationPaths);
            _trackingService = new EpisodeTrackingService(loggerFactory.CreateLogger<EpisodeTrackingService>(), _trackingDatabase);

            // Initialize FFmpeg services
            _hardwareFFmpegService = new HardwareFFmpegService(loggerFactory.CreateLogger<HardwareFFmpegService>());
            _softwareFFmpegService = new SoftwareFFmpegService(loggerFactory.CreateLogger<SoftwareFFmpegService>());
            _ffmpegService = new FFmpegService(
                loggerFactory.CreateLogger<FFmpegService>(),
                configurationManager,
                _hardwareFFmpegService,
                _softwareFFmpegService,
                loggerFactory);

            // Initialize canvas service
            _canvasService = new CanvasService(loggerFactory.CreateLogger<CanvasService>(), _ffmpegService);

            // Initialize poster service with canvas dependency
            _posterService = new PosterService(
                loggerFactory.CreateLogger<PosterService>(), 
                _canvasService,
                configurationManager);

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

        public ILoggerFactory LoggerFactory => _loggerFactory;
        public EpisodeTrackingService TrackingService => _trackingService;
        public EpisodeTrackingDatabase TrackingDatabase => _trackingDatabase;
        public CanvasService CanvasService => _canvasService;
        public FFmpegService FFmpegService => _ffmpegService;
        public HardwareFFmpegService HardwareFFmpegService => _hardwareFFmpegService;
        public SoftwareFFmpegService SoftwareFFmpegService => _softwareFFmpegService;
        public PosterService PosterService => _posterService;

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
                _disposed = true;
            }
        }
    }
}