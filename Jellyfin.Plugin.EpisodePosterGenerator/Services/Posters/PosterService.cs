using System;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.TV;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;
using Microsoft.Extensions.Logging;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    public class PosterService
    {
        private readonly ILogger<PosterService> _logger;
        private readonly CanvasService _canvasService;
        private readonly IServerConfigurationManager _configurationManager;
        private readonly ILoggerFactory _loggerFactory;

        // PosterService
        // Initializes the poster service with canvas and configuration dependencies.
        public PosterService(
            ILogger<PosterService> logger,
            CanvasService canvasService,
            IServerConfigurationManager configurationManager,
            ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _canvasService = canvasService;
            _configurationManager = configurationManager;
            _loggerFactory = loggerFactory;
        }

        // GeneratePosterAsync
        // Generates a poster for an episode and returns the path to the generated file.
        public async Task<string?> GeneratePosterAsync(Episode episode)
        {
            if (Plugin.Instance == null)
            {
                _logger.LogError("Plugin instance not available");
                return null;
            }

            var config = Plugin.Instance.Configuration;
            if (config == null)
            {
                _logger.LogError("Plugin configuration not available");
                return null;
            }

            var posterSettings = Plugin.Instance.PosterConfigService.GetSettingsForEpisode(episode);

            _logger.LogInformation("Generating poster for {SeriesName} - {EpisodeName}",
                episode.Series?.Name ?? "Unknown Series",
                episode.Name ?? "Unknown Episode");

            var episodeMetadata = EpisodeMetadata.CreateFromEpisode(episode);

            using var bitmap = await _canvasService.GenerateCanvasAsync(episode, episodeMetadata, posterSettings).ConfigureAwait(false);
            if (bitmap == null)
            {
                _logger.LogWarning("Failed to generate canvas for episode: {EpisodeName}", episode.Name);
                return null;
            }

            IPosterGenerator generator = posterSettings.PosterStyle switch
            {
                PosterStyle.Logo => new LogoPosterGenerator(_loggerFactory.CreateLogger<LogoPosterGenerator>()),
                PosterStyle.Numeral => new NumeralPosterGenerator(_loggerFactory.CreateLogger<NumeralPosterGenerator>()),
                PosterStyle.Cutout => new CutoutPosterGenerator(_loggerFactory.CreateLogger<CutoutPosterGenerator>()),
                PosterStyle.Standard => new StandardPosterGenerator(_loggerFactory.CreateLogger<StandardPosterGenerator>()),
                PosterStyle.Frame => new FramePosterGenerator(_loggerFactory.CreateLogger<FramePosterGenerator>()),
                PosterStyle.Brush => new BrushPosterGenerator(_loggerFactory.CreateLogger<BrushPosterGenerator>()),
                PosterStyle.Split => new SplitPosterGenerator(_loggerFactory.CreateLogger<SplitPosterGenerator>()),
                _ => new StandardPosterGenerator(_loggerFactory.CreateLogger<StandardPosterGenerator>())
            };

            var tempFilePath = GetTemporaryPosterPath(episode.Id);

            var generatedPath = generator.Generate(bitmap, episodeMetadata, posterSettings, tempFilePath);

            if (generatedPath == null)
            {
                _logger.LogError("Failed to generate poster for episode: {EpisodeName}", episode.Name);
                return null;
            }

            _logger.LogInformation("Successfully generated poster for {SeriesName} - {EpisodeName}",
                episode.Series?.Name ?? "Unknown Series",
                episode.Name ?? "Unknown Episode");

            return generatedPath;
        }

        // GetTemporaryPosterPath
        // Constructs the temporary file path for the generated poster.
        private string GetTemporaryPosterPath(Guid episodeId)
        {
            var tempDir = _configurationManager.GetTranscodePath();
            var fileName = $"{episodeId}.jpg";
            return Path.Combine(tempDir, fileName);
        }
    }
}
