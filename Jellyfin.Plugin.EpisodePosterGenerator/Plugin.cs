using System;
using System.Collections.Generic;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public static Plugin? Instance { get; private set; }

        private readonly ILogger<Plugin> _logger;
        private readonly IMediaEncoder _mediaEncoder;
        private readonly ILibraryManager _libraryManager;
        private readonly IApplicationPaths _applicationPaths;
        private readonly ILoggerFactory _loggerFactory;
        private readonly FFmpegService _ffmpegService;
        private readonly PosterGeneratorService _posterGeneratorService;
        private readonly EpisodeTrackingService _trackingService;

        // MARK: Constructor
        public Plugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            IMediaEncoder mediaEncoder,
            ILibraryManager libraryManager,
            ILogger<Plugin> logger,
            ILoggerFactory loggerFactory)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            _logger = logger;
            _mediaEncoder = mediaEncoder;
            _libraryManager = libraryManager;
            _applicationPaths = applicationPaths;
            _loggerFactory = loggerFactory;

            _ffmpegService = new FFmpegService(loggerFactory.CreateLogger<FFmpegService>(), mediaEncoder);
            _posterGeneratorService = new PosterGeneratorService();
            _trackingService = new EpisodeTrackingService(loggerFactory.CreateLogger<EpisodeTrackingService>(), applicationPaths);

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

        // MARK: GetPages
        public IEnumerable<PluginPageInfo> GetPages()
        {
            yield return new PluginPageInfo
            {
                Name = "Episode Poster Generator",
                EmbeddedResourcePath = typeof(Plugin).Namespace + ".Configuration.configPage.html"
            };
        }
    }
}