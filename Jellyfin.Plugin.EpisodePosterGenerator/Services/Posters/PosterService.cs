using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.TV;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Microsoft.Extensions.Logging;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Main service for generating episode posters from video content
    /// </summary>
    public class PosterService
    {
        /// <summary>
        /// Logger for poster generation operations
        /// </summary>
        private readonly ILogger<PosterService> _logger;

        /// <summary>
        /// Canvas service for creating poster canvases
        /// </summary>
        private readonly CanvasService _canvasService;

        /// <summary>
        /// Server Configuraiton Manager
        /// </summary>
        private readonly IServerConfigurationManager _configurationManager;

        // MARK: Constructor
        public PosterService(ILogger<PosterService> logger, CanvasService canvasService, IServerConfigurationManager configurationManager)
        {
            _logger = logger;
            _canvasService = canvasService;
            _configurationManager = configurationManager;
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

                // Log the extracted metadata for verification
                LogExtractedMetadata(episodeMetadata);

                // Generate canvas using CanvasService
                var canvasData = await _canvasService.GenerateCanvasAsync(episodeMetadata, config).ConfigureAwait(false);
                
                if (canvasData == null || canvasData.Length == 0)
                {
                    _logger.LogWarning("Failed to generate canvas for episode: {EpisodeName}", episode.Name);
                    return null;
                }

                // Save canvas to temporary file
                var tempFilePath = GetTemporaryPosterPath(episode.Id, config.PosterFileType);
                await File.WriteAllBytesAsync(tempFilePath, canvasData).ConfigureAwait(false);

                _logger.LogInformation("Poster generated and saved to: {FilePath}", tempFilePath);
                return tempFilePath;
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

        // MARK: LogExtractedMetadata
        private void LogExtractedMetadata(EpisodeMetadata metadata)
        {
            var video = metadata.VideoMetadata;

            _logger.LogDebug("=== Episode Metadata ===");
            _logger.LogDebug("Series Name: {SeriesName}", metadata.SeriesName ?? "N/A");
            _logger.LogDebug("Season Name: {SeasonName}", metadata.SeasonName ?? "N/A");
            _logger.LogDebug("Season Number: {SeasonNumber}", metadata.SeasonNumber?.ToString(CultureInfo.InvariantCulture) ?? "N/A");
            _logger.LogDebug("Episode Name: {EpisodeName}", metadata.EpisodeName ?? "N/A");
            _logger.LogDebug("Episode Number Start: {EpisodeStart}", metadata.EpisodeNumberStart?.ToString(CultureInfo.InvariantCulture) ?? "N/A");
            _logger.LogDebug("Episode Number End: {EpisodeEnd}", metadata.EpisodeNumberEnd?.ToString(CultureInfo.InvariantCulture) ?? "N/A");

            _logger.LogDebug("=== Video Metadata ===");
            _logger.LogDebug("File Path: {FilePath}", video.EpisodeFilePath ?? "N/A");
            _logger.LogDebug("Series Logo Path: {LogoPath}", video.SeriesLogoFilePath ?? "N/A");
            _logger.LogDebug("Resolution: {Width}x{Height}", video.VideoWidth, video.VideoHeight);
            _logger.LogDebug("Container: {Container}", video.VideoContainer);
            _logger.LogDebug("Codec: {Codec}", video.VideoCodec);
            _logger.LogDebug("Duration (Ticks): {Duration}", video.VideoLengthTicks);
            _logger.LogDebug("Color Space: {ColorSpace}", video.VideoColorSpace);
            _logger.LogDebug("Color Bits: {ColorBits}", video.VideoColorBits);
            _logger.LogDebug("HDR Type: {HdrType}", video.VideoHdrType);
            _logger.LogDebug("Is HDR: {IsHdr}", video.VideoHdrType.IsHDR());
            _logger.LogDebug("Is Dolby Vision: {IsDolbyVision}", video.VideoHdrType.IsDolbyVision());
        }
    }
}