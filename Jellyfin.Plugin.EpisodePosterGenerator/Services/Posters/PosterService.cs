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
using SkiaSharp;

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
        // Generates a poster for an episode and returns the path to the generated file,
        // along with an optional backdrop image derived from the extracted canvas.
        public async Task<PosterGenerationResult?> GeneratePosterAsync(Episode episode)
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

            // Capture the cropped canvas as a backdrop before the poster layers are rendered onto it.
            string? backdropPath = null;
            if (posterSettings.CanvasSource == CanvasSource.Extract && posterSettings.GenerateBackdrop)
            {
                backdropPath = GetTemporaryBackdropPath(episode.Id);
                if (!TrySaveBackdrop(bitmap, backdropPath))
                {
                    backdropPath = null;
                }
            }

            IPosterGenerator generator = PreviewService.CreateGenerator(posterSettings.PosterStyle, _loggerFactory);

            var tempFilePath = GetTemporaryPosterPath(episode.Id);

            var generatedPath = generator.Generate(bitmap, episodeMetadata, posterSettings, tempFilePath);

            if (generatedPath == null)
            {
                _logger.LogError("Failed to generate poster for episode: {EpisodeName}", episode.Name);
                DeleteTemporaryFile(backdropPath);
                return null;
            }

            _logger.LogInformation("Successfully generated poster for {SeriesName} - {EpisodeName}",
                episode.Series?.Name ?? "Unknown Series",
                episode.Name ?? "Unknown Episode");

            return new PosterGenerationResult
            {
                PosterPath = generatedPath,
                BackdropPath = backdropPath
            };
        }

        // TrySaveBackdrop
        // Encodes the canvas bitmap to a JPEG file for use as the episode backdrop.
        private bool TrySaveBackdrop(SKBitmap bitmap, string outputPath)
        {
            try
            {
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
                using var stream = File.Create(outputPath);
                data.SaveTo(stream);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save backdrop image to {OutputPath}", outputPath);
                return false;
            }
        }

        // DeleteTemporaryFile
        // Removes a temporary file, ignoring any cleanup errors.
        private void DeleteTemporaryFile(string? path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temporary file: {Path}", path);
            }
        }

        // GetTemporaryBackdropPath
        // Constructs the temporary file path for the generated backdrop.
        private string GetTemporaryBackdropPath(Guid episodeId)
        {
            var tempDir = _configurationManager.GetTranscodePath();
            var fileName = $"{episodeId}-backdrop.jpg";
            return Path.Combine(tempDir, fileName);
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
