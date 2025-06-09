using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator;

public class Plugin : BasePlugin<PluginConfiguration>
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

    public string GetFFmpegPath()
    {
        var path = _mediaEncoder.EncoderPath;
        if (string.IsNullOrEmpty(path))
        {
            _logger.LogWarning("MediaEncoder.EncoderPath is empty, using fallback detection");
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--ffmpeg")
                {
                    _logger.LogInformation("Found FFmpeg from command line: {Path}", args[i + 1]);
                    return args[i + 1];
                }
            }
            _logger.LogInformation("Using macOS fallback FFmpeg path");
            return "/Applications/Jellyfin.app/Contents/MacOS/ffmpeg";
        }
        _logger.LogInformation("Using MediaEncoder FFmpeg path: {Path}", path);
        return path;
    }
}