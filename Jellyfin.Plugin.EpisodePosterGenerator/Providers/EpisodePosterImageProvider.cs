using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Providers
{
    /// <summary>
    /// Generates an episode poster automatically during a metadata refresh when the episode
    /// has no primary image. Users who want to choose between several frames instead go
    /// through <see cref="EpisodePosterRemoteImageProvider"/> in the Edit Images dialog.
    /// </summary>
    public class EpisodePosterImageProvider : IDynamicImageProvider
    {
        private readonly ILogger<EpisodePosterImageProvider> _logger;
        private readonly IProviderManager _providerManager;

        // EpisodePosterImageProvider
        // Initializes the image provider with logging and provider manager dependencies.
        public EpisodePosterImageProvider(
            ILogger<EpisodePosterImageProvider> logger,
            IProviderManager providerManager)
        {
            _logger = logger;
            _providerManager = providerManager;
        }

        public string Name => "Episode Poster Generator";

        // Supports
        // Returns true if the item is an Episode.
        public bool Supports(BaseItem item)
        {
            return item is Episode;
        }

        // GetSupportedImages
        // Returns the Primary image type for episodes.
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            if (item is Episode)
            {
                yield return ImageType.Primary;
            }
        }

        // GetImage
        // Generates and returns a poster image for the episode.
        public async Task<DynamicImageResponse> GetImage(BaseItem item, ImageType type, CancellationToken cancellationToken)
        {
            if (Plugin.Instance == null)
            {
                return new DynamicImageResponse { HasImage = false };
            }

            var config = Plugin.Instance.Configuration;
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

            var posterService = Plugin.Instance.PosterService;
            if (posterService == null)
            {
                _logger.LogError("PosterService not available");
                return new DynamicImageResponse { HasImage = false };
            }

            try
            {
                _logger.LogInformation("Starting to create poster for {SeriesName} - {EpisodeName}", episode.SeriesName, episode.Name);

                var result = await posterService.GeneratePosterAsync(episode, cancellationToken).ConfigureAwait(false);

                if (result == null)
                {
                    _logger.LogWarning("Failed to generate image for episode: {SeriesName} - {EpisodeName}", episode.SeriesName, episode.Name);
                    return new DynamicImageResponse { HasImage = false };
                }

                await SaveBackdropIfPresentAsync(episode, result.Backdrop, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Poster created for {SeriesName} - {EpisodeName}", episode.SeriesName, episode.Name);

                return new DynamicImageResponse
                {
                    HasImage = true,

                    // DynamicImageResponse takes ownership of the stream and disposes it.
                    Stream = new MemoryStream(result.Poster),
                    Format = ImageFormat.Jpg
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating image for episode: {SeriesName} - {EpisodeName}", episode.SeriesName, episode.Name);
                return new DynamicImageResponse { HasImage = false };
            }
        }

        // SaveBackdropIfPresentAsync
        // Uploads the generated backdrop image to the episode when one was produced.
        private async Task SaveBackdropIfPresentAsync(Episode episode, byte[]? backdrop, CancellationToken cancellationToken)
        {
            if (backdrop == null || backdrop.Length == 0)
            {
                return;
            }

            try
            {
                await RemoveExistingBackdropsAsync(episode).ConfigureAwait(false);

                using (var backdropStream = new MemoryStream(backdrop))
                {
                    await _providerManager.SaveImage(
                        episode,
                        backdropStream,
                        "image/jpeg",
                        ImageType.Backdrop,
                        null,
                        cancellationToken).ConfigureAwait(false);
                }

                await episode.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save backdrop image for episode: {SeriesName} - {EpisodeName}", episode.SeriesName, episode.Name);
            }
        }

        // RemoveExistingBackdropsAsync
        // Deletes any backdrop images already attached to the episode so the extracted
        // frame becomes the sole backdrop.
        private static async Task RemoveExistingBackdropsAsync(Episode episode)
        {
            foreach (var image in episode.GetImages(ImageType.Backdrop).ToList())
            {
                await episode.DeleteImageAsync(ImageType.Backdrop, episode.GetImageIndex(image)).ConfigureAwait(false);
            }
        }
    }
}
