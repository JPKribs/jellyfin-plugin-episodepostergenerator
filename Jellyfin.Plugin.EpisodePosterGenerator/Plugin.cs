using System;
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

namespace Jellyfin.Plugin.EpisodePosterGenerator
{
    /// <summary>
    /// Main plugin class for the Episode Poster Generator Jellyfin plugin.
    /// Automatically generates custom episode posters using smart frame analysis, black frame detection, 
    /// and configurable text styling. Supports multiple poster styles including Standard, Cutout, Numeral, and Logo.
    /// 
    /// The plugin integrates with Jellyfin's image provider system to automatically generate posters for episodes
    /// that don't have primary images or when video files/configuration changes. Uses SQLite for tracking
    /// processed episodes to avoid unnecessary regeneration and improve performance.
    /// 
    /// Key Features:
    /// - Smart frame extraction with black scene avoidance
    /// - Multiple poster styles with customizable typography
    /// - Hardware-accelerated video processing when available
    /// - Intelligent episode tracking and reprocessing logic
    /// - Scheduled task support for batch processing
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IDisposable
    {
        /// <summary>
        /// Singleton instance of the plugin for global access.
        /// Used by other components to access plugin services and configuration.
        /// </summary>
        public static Plugin? Instance { get; private set; }

        /// <summary>
        /// Logger instance for plugin-level logging operations.
        /// Used to log initialization, errors, and important plugin lifecycle events.
        /// </summary>
        private readonly ILogger<Plugin> _logger;

        /// <summary>
        /// Jellyfin's media encoder service for video processing operations.
        /// Provides access to FFmpeg paths and hardware acceleration capabilities.
        /// </summary>
        private readonly IMediaEncoder _mediaEncoder;

        /// <summary>
        /// Jellyfin's library manager for accessing episode metadata and media items.
        /// Used by scheduled tasks and image providers to enumerate and process episodes.
        /// </summary>
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// Jellyfin's application paths service for determining data directory locations.
        /// Used to store the SQLite database and temporary files in appropriate system locations.
        /// </summary>
        private readonly IApplicationPaths _applicationPaths;

        /// <summary>
        /// Logger factory for creating logger instances for individual services.
        /// Ensures consistent logging configuration across all plugin components.
        /// </summary>
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Service responsible for FFmpeg video processing operations.
        /// Handles frame extraction, black scene detection, video duration analysis,
        /// and hardware acceleration when available.
        /// </summary>
        private readonly FFmpegService _ffmpegService;

        /// <summary>
        /// Service responsible for generating poster images with text overlays.
        /// Processes extracted frames and applies configured poster styles, fonts, and layouts.
        /// </summary>
        private readonly PosterGeneratorService _posterGeneratorService;

        /// <summary>
        /// Service responsible for tracking processed episodes and determining when reprocessing is needed.
        /// Uses SQLite database to store episode processing history and detect changes in video files or configuration.
        /// </summary>
        private readonly EpisodeTrackingService _trackingService;

        /// <summary>
        /// SQLite database service for persistent storage of episode tracking data.
        /// Stores episode processing records including file metadata and configuration hashes
        /// to enable intelligent reprocessing decisions.
        /// </summary>
        private readonly EpisodeTrackingDatabase _trackingDatabase;

        /// <summary>
        /// Flag indicating whether the plugin has been disposed to prevent multiple disposal operations.
        /// Used to implement the standard IDisposable pattern safely.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the Episode Poster Generator plugin.
        /// Sets up all required services, initializes the SQLite database asynchronously,
        /// and configures the plugin for operation within the Jellyfin ecosystem.
        /// 
        /// The initialization process:
        /// 1. Stores dependency injection parameters for later use
        /// 2. Creates core video processing and poster generation services
        /// 3. Initializes SQLite database and episode tracking services
        /// 4. Starts asynchronous database initialization
        /// 5. Sets up global plugin instance for access by other components
        /// </summary>
        /// <param name="applicationPaths">Jellyfin's application paths service for determining data directories.</param>
        /// <param name="xmlSerializer">Jellyfin's XML serialization service for configuration persistence.</param>
        /// <param name="mediaEncoder">Jellyfin's media encoder service for FFmpeg access and video processing.</param>
        /// <param name="libraryManager">Jellyfin's library manager for accessing media items and metadata.</param>
        /// <param name="logger">Logger instance for plugin-level logging operations.</param>
        /// <param name="loggerFactory">Factory for creating logger instances for individual services.</param>
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
            // Set up singleton instance for global access
            Instance = this;

            // Store injected dependencies for service creation and property access
            _logger = logger;
            _mediaEncoder = mediaEncoder;
            _libraryManager = libraryManager;
            _applicationPaths = applicationPaths;
            _loggerFactory = loggerFactory;

            // Initialize core video processing service with FFmpeg integration
            _ffmpegService = new FFmpegService(loggerFactory.CreateLogger<FFmpegService>(), mediaEncoder);

            // Initialize poster generation service for image processing and text overlay
            _posterGeneratorService = new PosterGeneratorService();
            
            // Initialize SQLite database service for episode tracking persistence
            _trackingDatabase = new EpisodeTrackingDatabase(loggerFactory.CreateLogger<EpisodeTrackingDatabase>(), applicationPaths);

            // Initialize episode tracking service with database dependency
            _trackingService = new EpisodeTrackingService(loggerFactory.CreateLogger<EpisodeTrackingService>(), _trackingDatabase);

            // Initialize database asynchronously to avoid blocking plugin startup
            // Database initialization includes connection setup and table creation
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

        /// <summary>
        /// Gets the display name of the plugin shown in the Jellyfin administration interface.
        /// </summary>
        public override string Name => "Episode Poster Generator";

        /// <summary>
        /// Gets the unique identifier for this plugin used by Jellyfin for registration and management.
        /// This GUID must remain constant across plugin versions to maintain proper plugin lifecycle management.
        /// </summary>
        public override Guid Id => Guid.Parse("b8715e44-6b77-4c88-9c74-2b6f4c7b9a1e");

        /// <summary>
        /// Gets the plugin description displayed in the Jellyfin administration interface.
        /// Provides a concise explanation of the plugin's functionality for administrators.
        /// </summary>
        public override string Description => "Automatically generates episode poster cards with titles overlaid on representative frames from video files.";

        /// <summary>
        /// Gets the media encoder service for FFmpeg access and video processing operations.
        /// Exposes Jellyfin's media encoding capabilities to plugin components that need direct access.
        /// </summary>
        public IMediaEncoder MediaEncoder => _mediaEncoder;

        /// <summary>
        /// Gets the library manager service for accessing episode metadata and media library operations.
        /// Used by scheduled tasks and other components to enumerate and process episodes in the library.
        /// </summary>
        public ILibraryManager LibraryManager => _libraryManager;

        /// <summary>
        /// Gets the logger factory for creating consistent logger instances across plugin services.
        /// Ensures all plugin components use the same logging configuration and formatting.
        /// </summary>
        public ILoggerFactory LoggerFactory => _loggerFactory;

        /// <summary>
        /// Gets the FFmpeg service responsible for video analysis and frame extraction operations.
        /// Provides smart frame selection, black scene detection, and hardware-accelerated processing
        /// when supported by the system.
        /// </summary>
        public FFmpegService FFmpegService => _ffmpegService;

        /// <summary>
        /// Gets the poster generation service responsible for creating styled episode images.
        /// Handles image processing, text overlay, and multiple poster style generation
        /// based on user configuration preferences.
        /// </summary>
        public PosterGeneratorService PosterGeneratorService => _posterGeneratorService;

        /// <summary>
        /// Gets the episode tracking service responsible for managing processed episode records.
        /// Determines when episodes need reprocessing based on file changes, configuration updates,
        /// or manual image modifications.
        /// </summary>
        public EpisodeTrackingService TrackingService => _trackingService;

        /// <summary>
        /// Gets the SQLite database service for persistent episode tracking data storage.
        /// Provides direct database access for advanced operations and maintenance tasks.
        /// </summary>
        public EpisodeTrackingDatabase TrackingDatabase => _trackingDatabase;

        /// <summary>
        /// Gets the web pages provided by this plugin for the Jellyfin administration interface.
        /// Returns configuration pages that allow administrators to customize plugin behavior,
        /// poster styles, and generation settings.
        /// </summary>
        /// <returns>An enumerable collection of plugin page information for the administration interface.</returns>
        // MARK: GetPages
        public IEnumerable<PluginPageInfo> GetPages()
        {
            yield return new PluginPageInfo
            {
                Name = "Episode Poster Generator",
                EmbeddedResourcePath = typeof(Plugin).Namespace + ".Configuration.configPage.html"
            };
        }

        /// <summary>
        /// Releases all resources used by the plugin and ensures proper cleanup of managed resources.
        /// Implements the standard IDisposable pattern with finalizer suppression for optimal performance.
        /// This method is called automatically when the plugin is unloaded or Jellyfin shuts down.
        /// 
        /// Cleanup includes:
        /// - Disposing the SQLite database connection
        /// - Releasing any other managed resources
        /// - Suppressing finalizer execution for performance
        /// </summary>
        // MARK: Dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of the dispose pattern for proper resource cleanup.
        /// Ensures that resources are only disposed once and handles both explicit disposal
        /// and garbage collection scenarios appropriately.
        /// 
        /// The disposal process:
        /// 1. Checks if already disposed to prevent multiple cleanup attempts
        /// 2. Disposes managed resources when called explicitly (disposing = true)
        /// 3. Marks the object as disposed to prevent future operations
        /// 4. For this plugin, primarily focuses on SQLite database connection cleanup
        /// </summary>
        /// <param name="disposing">
        /// True when called explicitly (via Dispose()), false when called from finalizer.
        /// Determines whether managed resources should be disposed.
        /// </param>
        // MARK: Dispose
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // Dispose the SQLite database connection and any associated resources
                _trackingDatabase?.Dispose();
                _disposed = true;
            }
        }
    }
}