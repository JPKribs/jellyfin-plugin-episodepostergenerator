using System;
using System.Collections.Generic;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Services;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;
using JPKribs.Jellyfin.Base;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator
{
    public class Plugin : PluginBase<Plugin, PluginConfiguration>
    {
        public override string Name => "Episode Poster Generator";
        public override Guid Id => Guid.Parse("b8715e44-6b77-4c88-9c74-2b6f4c7b9a1e");
        public override string Description => "Automatically generates episode poster cards with titles overlaid on representative frames from video files.";

        private readonly ILogger<Plugin> _logger;
        private readonly PosterService _posterService;
        private readonly PosterConfigurationService _posterConfigService;
        private readonly PreviewService _previewService;
        private readonly GeneratedImageCache _generatedImageCache;

        // Plugin
        // Initializes the plugin with all required services and dependencies.
        public Plugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            ILogger<Plugin> logger,
            ILoggerFactory loggerFactory,
            IMediaEncoder mediaEncoder)
            : base(applicationPaths, xmlSerializer)
        {
            _logger = logger;

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
                loggerFactory);

            _previewService = new PreviewService(loggerFactory, applicationPaths);
            _generatedImageCache = new GeneratedImageCache(
                loggerFactory.CreateLogger<GeneratedImageCache>());

            // Container images ship almost none of the common desktop fonts, so a configured
            // family often silently falls back to Skia's default. Surface it once per family.
            Utilities.FontUtils.SetMissingFamilyReporter(family =>
                _logger.LogWarning(
                    "Font family '{Family}' is not installed on this server; falling back to the default font. Install the font, or pick one offered by the configuration page.",
                    family));

            _logger.LogInformation("Episode Poster Generator plugin initialized");
        }

        public PosterService PosterService => _posterService;

        public PosterConfigurationService PosterConfigService => _posterConfigService;

        public PreviewService PreviewService => _previewService;

        /// <summary>
        /// Gets the short-lived store backing the generated poster URLs handed to Jellyfin's
        /// remote image picker.
        /// </summary>
        public GeneratedImageCache GeneratedImageCache => _generatedImageCache;

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

        // UpdateConfiguration
        // Updates the configuration and reinitializes the poster configuration service.
        public override void UpdateConfiguration(BasePluginConfiguration configuration)
        {
            base.UpdateConfiguration(configuration);
            _posterConfigService?.Initialize(Configuration);
        }
    }
}
