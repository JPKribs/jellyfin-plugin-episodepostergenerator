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
    /// <summary>
    /// Main plugin class for Episode Poster Generator
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IDisposable
    {
        public override string Name => "Episode Poster Generator";
        public override Guid Id => Guid.Parse("b8715e44-6b77-4c88-9c74-2b6f4c7b9a1e");
        public override string Description => "Automatically generates episode poster cards with titles overlaid on representative frames from video files.";
        public static Plugin? Instance { get; private set; }

        // MARK: Dependencies
        private readonly ILogger<Plugin> _logger;
        private readonly ILoggerFactory _loggerFactory;

        private readonly EpisodeTrackingService _trackingService;
        private readonly EpisodeTrackingDatabase _trackingDatabase;
        private readonly FFmpegService _ffmpegService;
        private readonly HardwareFFmpegService _hardwareFFmpegService;
        private readonly SoftwareFFmpegService _softwareFFmpegService;
        private readonly CanvasService _canvasService;
        private readonly BrightnessService _brightnessService;
        private readonly CroppingService _croppingService;
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

            // MARK: Initialize configuration hash service
            var configHashService = new ConfigurationHashService();

            // MARK: Initialize tracking services
            _trackingDatabase = new EpisodeTrackingDatabase(
                loggerFactory.CreateLogger<EpisodeTrackingDatabase>(),
                applicationPaths);
            _trackingService = new EpisodeTrackingService(
                loggerFactory.CreateLogger<EpisodeTrackingService>(),
                _trackingDatabase,
                configHashService);

            // MARK: Initialize FFmpeg services
            _brightnessService = new BrightnessService(
                loggerFactory.CreateLogger<BrightnessService>());
            _hardwareFFmpegService = new HardwareFFmpegService(
                loggerFactory.CreateLogger<HardwareFFmpegService>());
            _softwareFFmpegService = new SoftwareFFmpegService(
                loggerFactory.CreateLogger<SoftwareFFmpegService>());
            _ffmpegService = new FFmpegService(
                loggerFactory.CreateLogger<FFmpegService>(),
                configurationManager,
                _hardwareFFmpegService,
                _softwareFFmpegService,
                _brightnessService);

            // MARK: Initialize cropping service
            _croppingService = new CroppingService(
                loggerFactory.CreateLogger<CroppingService>());

            // MARK: Initialize canvas service with cropping support
            _canvasService = new CanvasService(
                loggerFactory.CreateLogger<CanvasService>(),
                _ffmpegService,
                _croppingService,
                _brightnessService);

            // MARK: Initialize poster service
            _posterService = new PosterService(
                loggerFactory.CreateLogger<PosterService>(),
                _canvasService,
                configurationManager,
                loggerFactory);

            // MARK: Initialize database asynchronously
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

        // MARK: Public property accessors
        public ILoggerFactory LoggerFactory => _loggerFactory;
        public EpisodeTrackingService TrackingService => _trackingService;
        public EpisodeTrackingDatabase TrackingDatabase => _trackingDatabase;
        public CanvasService CanvasService => _canvasService;
        public CroppingService CroppingService => _croppingService;
        public FFmpegService FFmpegService => _ffmpegService;
        public HardwareFFmpegService HardwareFFmpegService => _hardwareFFmpegService;
        public SoftwareFFmpegService SoftwareFFmpegService =>
        _softwareFFmpegService;
        public PosterService PosterService => _posterService;

        // MARK: GetPages
        public IEnumerable<PluginPageInfo> GetPages()
        {
            yield return new PluginPageInfo
            {
                Name = "EpisodePosterGenerator",
                EmbeddedResourcePath = $"{typeof(Plugin).Namespace}.Configuration.configPage.html",
                MenuSection = "plugin",
                DisplayName = "Episode Poster Generator"
            };
        }

        // MARK: GetImage
        public Stream GetImage()
        {
            var assembly = GetType().Assembly;
            var stream = assembly.GetManifestResourceStream($"{typeof(Plugin).Namespace}.Logo.png");

            return stream!;
        }

        // MARK: Dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // MARK: Dispose(bool)
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