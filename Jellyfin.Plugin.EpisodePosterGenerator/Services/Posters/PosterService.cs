using System;
using System.Globalization;
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
    /// <summary>
    /// Main service for generating episode posters from video content
    /// </summary>
    public class PosterService
    {
        private readonly ILogger<PosterService> _logger;
        private readonly CanvasService _canvasService;
        private readonly IServerConfigurationManager _configurationManager;
        private readonly ILoggerFactory _loggerFactory;

        // MARK: Constructor
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

        // MARK: Generate
        public async Task<string?> GenerateAsync(TaskTrigger trigger, Episode episode, PluginConfiguration config)
        {
            if (episode == null)
            {
                _logger.LogError("Episode cannot be null");
                return null;
            }

            if (config == null)
            {
                _logger.LogError("Configuration cannot be null");
                return null;
            }

            try
            {
                _logger.LogInformation("Starting poster generation for episode: {SeriesName} - {EpisodeName}",
                    episode.Series?.Name ?? "Unknown Series",
                    episode.Name ?? "Unknown Episode");

                // Extract complete metadata from the episode
                var episodeMetadata = EpisodeMetadata.CreateFromEpisode(episode);

                // Generate canvas using CanvasService
                var canvasData = await _canvasService.GenerateCanvasAsync(episodeMetadata, config).ConfigureAwait(false);
                if (canvasData == null || canvasData.Length == 0)
                {
                    _logger.LogWarning("Failed to generate canvas for episode: {EpisodeName}", episode.Name);
                    return null;
                }

                // Decode SKBitmap from byte array
                using var bitmap = SkiaSharp.SKBitmap.Decode(canvasData);
                if (bitmap == null)
                {
                    _logger.LogWarning("Failed to decode bitmap for episode: {EpisodeName}", episode.Name);
                    return null;
                }

                // MARK: Select Poster Generator
                IPosterGenerator generator = config.PosterStyle switch
                {
                    PosterStyle.Logo => new LogoPosterGenerator(_loggerFactory.CreateLogger<LogoPosterGenerator>()),
                    PosterStyle.Numeral => new NumeralPosterGenerator(_loggerFactory.CreateLogger<NumeralPosterGenerator>()),
                    PosterStyle.Cutout => new CutoutPosterGenerator(_loggerFactory.CreateLogger<CutoutPosterGenerator>()),
                    PosterStyle.Standard => new StandardPosterGenerator(_loggerFactory.CreateLogger<StandardPosterGenerator>()),
                    _ => new StandardPosterGenerator(_loggerFactory.CreateLogger<StandardPosterGenerator>()) // fallback
                };

                // Determine temp path
                var tempFilePath = GetTemporaryPosterPath(episode.Id, config.PosterFileType);

                // Generate poster
                var resultPath = generator.Generate(bitmap, episodeMetadata, config, tempFilePath);
                if (resultPath == null)
                {
                    _logger.LogWarning("Poster generator failed for episode: {EpisodeName}", episode.Name);
                    return null;
                }

                _logger.LogInformation("Poster generated and saved to: {FilePath}", resultPath);
                return resultPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate poster for episode: {EpisodeName}", episode.Name);
                return null;
            }
        }

        // MARK: GetTemporaryPosterPath
        private string GetTemporaryPosterPath(Guid episodeId, PosterFileType fileType)
        {
            var tempDir = _configurationManager.GetTranscodePath();
            var fileName = $"{episodeId}{fileType.GetFileExtension()}";
            return Path.Combine(tempDir, fileName);
        }
    }
}