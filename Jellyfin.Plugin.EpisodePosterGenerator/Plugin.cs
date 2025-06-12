using System;
using System.Collections.Generic;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator;

// MARK: Plugin

/// <summary>
/// Main plugin class for Episode Poster Generator.
/// Manages plugin lifecycle, configuration, and access to Jellyfin services.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Singleton instance of the plugin.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    private readonly ILogger<Plugin> _logger;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        IMediaEncoder mediaEncoder,
        ILibraryManager libraryManager,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _logger = logger;
        _mediaEncoder = mediaEncoder;
        _libraryManager = libraryManager;

        _logger.LogInformation("Episode Poster Generator plugin initialized successfully");
    }

    /// <inheritdoc/>
    public override string Name => "Episode Poster Generator";

    /// <inheritdoc/>
    public override Guid Id => Guid.Parse("b8715e44-6b77-4c88-9c74-2b6f4c7b9a1e");

    /// <inheritdoc/>
    public override string Description => "Automatically generates episode poster cards with titles overlaid on representative frames from video files.";

    /// <summary>
    /// Gets the media encoder instance.
    /// </summary>
    public IMediaEncoder MediaEncoder => _mediaEncoder;

    /// <summary>
    /// Gets the library manager instance.
    /// </summary>
    public ILibraryManager LibraryManager => _libraryManager;

    // MARK: GetPages

    /// <summary>
    /// Returns the plugin's web configuration pages.
    /// </summary>
    /// <returns>Collection of <see cref="PluginPageInfo"/> representing plugin pages.</returns>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = "Episode Poster Generator",
            EmbeddedResourcePath = typeof(Plugin).Namespace + ".Configuration.configPage.html"
        };
    }
}