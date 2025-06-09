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
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static Plugin? Instance { get; private set; }

    private readonly ILogger<Plugin> _logger;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly ILibraryManager _libraryManager;

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

    public override string Name => "Episode Poster Generator";

    public override Guid Id => Guid.Parse("b8715e44-6b77-4c88-9c74-2b6f4c7b9a1e");

    public override string Description => "Automatically generates episode poster cards with titles overlaid on representative frames from video files.";

    public IMediaEncoder MediaEncoder => _mediaEncoder;

    public ILibraryManager LibraryManager => _libraryManager;

    // MARK: GetPages
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = "Episode Poster Generator",
            EmbeddedResourcePath = typeof(Plugin).Namespace + ".Configuration.configPage.html"
        };
    }

    // MARK: GetFFmpegPath
    public string GetFFmpegPath()
    {
        var path = _mediaEncoder.EncoderPath;
        if (string.IsNullOrEmpty(path))
        {
            _logger.LogError("FFmpeg path not available from MediaEncoder. Jellyfin is not properly configured with FFmpeg.");
            throw new InvalidOperationException("FFmpeg is not available. Please ensure Jellyfin is properly configured with FFmpeg.");
        }
        
        _logger.LogDebug("Using FFmpeg path: {Path}", path);
        return path;
    }
}