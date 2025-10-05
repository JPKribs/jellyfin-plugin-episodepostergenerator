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
    public class EpisodePosterImageProvider : IDynamicImageProvider
    {
        private readonly ILogger<EpisodePosterImageProvider> _logger;
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

            var posterService = Plugin.Instance?.PosterService;
            if (posterService == null)
            {
                _logger.LogError("PosterService not available");
                return new DynamicImageResponse { HasImage = false };
            }

            try
            {
                _logger.LogInformation("Starting to create poster for {SeriesName} - {EpisodeName}", episode.SeriesName, episode.Name);

                var posterPath = await posterService.GeneratePosterAsync(episode).ConfigureAwait(false);
                
                if (string.IsNullOrEmpty(posterPath) || !File.Exists(posterPath))
                {
                    _logger.LogWarning("Failed to generate image for episode: {SeriesName} - {EpisodeName}", episode.SeriesName, episode.Name);
                    return new DynamicImageResponse { HasImage = false };
                }

                var imageBytes = await File.ReadAllBytesAsync(posterPath, cancellationToken).ConfigureAwait(false);
                var imageStream = new MemoryStream(imageBytes);

                var trackingService = Plugin.Instance?.TrackingService;
                if (trackingService != null)
                {
                    try
                    {
                        await trackingService.MarkEpisodeProcessedAsync(episode, config).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to mark episode as processed in tracking service: {SeriesName} - {EpisodeName}", episode.SeriesName, episode.Name);
                    }
                }

                _logger.LogInformation("Poster created for {SeriesName} - {EpisodeName}", episode.SeriesName, episode.Name);

                var posterSettings = Plugin.Instance!.PosterConfigService.GetSettingsForEpisode(episode);
                var imageFormat = posterSettings.PosterFileType == Models.PosterFileType.PNG ? ImageFormat.Png :
                                  posterSettings.PosterFileType == Models.PosterFileType.WEBP ? ImageFormat.Webp :
                                  ImageFormat.Jpg;

                return new DynamicImageResponse
                {
                    HasImage = true,
                    Stream = imageStream,
                    Format = imageFormat
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating image for episode: {SeriesName} - {EpisodeName}", episode.SeriesName, episode.Name);
                return new DynamicImageResponse { HasImage = false };
            }
        }
    }
}