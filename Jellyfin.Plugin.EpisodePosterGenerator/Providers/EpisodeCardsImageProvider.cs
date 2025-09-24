using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Providers
{
    /// <summary>
    /// Image provider trigger for on-demand episode poster generation.
    /// Gets episode from Jellyfin, passes directly to orchestrator (no filtering).
    /// </summary>
    public class EpisodePosterImageProvider : IDynamicImageProvider
    {
        /// <summary>
        /// Logger for image provider operations
        /// </summary>
        private readonly ILogger<EpisodePosterImageProvider> _logger;

        /// <summary>
        /// Application paths for temporary file management
        /// </summary>
        private readonly IApplicationPaths _appPaths;

        // MARK: Constructor
        public EpisodePosterImageProvider(
            ILogger<EpisodePosterImageProvider> logger,
            IApplicationPaths appPaths)
        {
            _logger = logger;
            _appPaths = appPaths;
        }

        public string Name => "Episode Poster Generator";

        // MARK: Supports
        public bool Supports(BaseItem item)
        {
            return item is Episode;
        }

        // MARK: GetSupportedImages
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            if (item is Episode)
            {
                yield return ImageType.Primary;
            }
        }

        // MARK: GetImage
        public async Task<DynamicImageResponse> GetImage(BaseItem item, ImageType type, CancellationToken cancellationToken)
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null || !config.EnableProvider)
            {
                return new DynamicImageResponse { HasImage = false };
            }

            if (item is not Episode episode)
            {
                return new DynamicImageResponse { HasImage = false };
            }

            if (type != ImageType.Primary)
            {
                return new DynamicImageResponse { HasImage = false };
            }

            if (string.IsNullOrEmpty(episode.Path) || !File.Exists(episode.Path))
            {
                return new DynamicImageResponse { HasImage = false };
            }

            var orchestrator = Plugin.Instance?.Orchestrator;
            if (orchestrator == null)
            {
                _logger.LogError("Plugin orchestrator not available");
                return new DynamicImageResponse { HasImage = false };
            }

            try
            {
                _logger.LogInformation("Starting to create poster for {EpisodeName}", episode.Name);

                // Pass single episode to orchestrator (no filtering - provider always processes)
                var posterPath = await orchestrator.GeneratePoster(episode, config, cancellationToken).ConfigureAwait(false);
                
                if (string.IsNullOrEmpty(posterPath) || !File.Exists(posterPath))
                {
                    _logger.LogWarning("Failed to generate image for episode: {EpisodeName}", episode.Name);
                    return new DynamicImageResponse { HasImage = false };
                }

                // Load poster into memory stream for Jellyfin
                var imageBytes = await File.ReadAllBytesAsync(posterPath, cancellationToken).ConfigureAwait(false);
                var imageStream = new MemoryStream(imageBytes);

                // Mark as processed in tracking service
                var trackingService = Plugin.Instance?.TrackingService;
                if (trackingService != null)
                {
                    try
                    {
                        await trackingService.MarkEpisodeProcessedAsync(episode, config).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to mark episode as processed in tracking service: {EpisodeName}", episode.Name);
                    }
                }

                _logger.LogInformation("Poster created for {EpisodeName}", episode.Name);

                return new DynamicImageResponse
                {
                    HasImage = true,
                    Stream = imageStream,
                    Format = ImageFormat.Jpg
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating image for episode: {EpisodeName}", episode.Name);
                return new DynamicImageResponse { HasImage = false };
            }
        }
    }
}