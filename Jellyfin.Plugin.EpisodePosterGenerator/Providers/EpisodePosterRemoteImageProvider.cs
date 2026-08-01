using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Providers
{
    /// <summary>
    /// Offers freshly generated posters in an episode's Edit Images dialog, so a poster can be
    /// picked by hand instead of only arriving through a metadata refresh.
    /// </summary>
    /// <remarks>
    /// Jellyfin renders the picker by fetching each <see cref="RemoteImageInfo.Url"/> with the
    /// server's own HTTP client, and downloads the chosen one the same way. Those requests carry
    /// no user credentials, so the images are rendered here — inside an authenticated call — and
    /// parked in <see cref="Services.Posters.GeneratedImageCache"/> under an unguessable token.
    /// The URLs point back at this server over loopback and resolve to a plain cache lookup.
    /// </remarks>
    public class EpisodePosterRemoteImageProvider : IRemoteImageProvider, IHasOrder
    {
        /// <summary>
        /// Route serving cached candidates. Must stay in step with ConfigurationController.
        /// </summary>
        internal const string GeneratedImageRoute = "Plugins/EpisodePosterGenerator/Generated";

        private readonly ILogger<EpisodePosterRemoteImageProvider> _logger;
        private readonly IServerApplicationHost _appHost;

        public EpisodePosterRemoteImageProvider(
            ILogger<EpisodePosterRemoteImageProvider> logger,
            IServerApplicationHost appHost)
        {
            _logger = logger;
            _appHost = appHost;
        }

        public string Name => "Episode Poster Generator";

        /// <summary>
        /// Gets the ordering hint. Listed after the metadata providers so a real episode still
        /// takes precedence when Jellyfin picks a default.
        /// </summary>
        public int Order => 100;

        // Supports
        // Returns true if the item is an Episode.
        public bool Supports(BaseItem item) => item is Episode;

        // GetSupportedImages
        // Generated posters are offered as the episode's primary image.
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            if (item is Episode)
            {
                yield return ImageType.Primary;
            }
        }

        // GetImages
        // Renders the configured number of poster candidates and publishes them as remote images.
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var plugin = Plugin.Instance;
            if (plugin?.Configuration == null || !plugin.Configuration.EnableProvider)
            {
                return Array.Empty<RemoteImageInfo>();
            }

            if (item is not Episode episode)
            {
                return Array.Empty<RemoteImageInfo>();
            }

            if (string.IsNullOrEmpty(episode.Path) || !File.Exists(episode.Path))
            {
                _logger.LogDebug("Episode {EpisodeName} has no readable media file; no posters to offer", episode.Name);
                return Array.Empty<RemoteImageInfo>();
            }

            var baseUrl = GetLocalBaseUrl();
            if (baseUrl == null)
            {
                _logger.LogError("Could not determine this server's local API URL; generated posters cannot be offered in the image picker");
                return Array.Empty<RemoteImageInfo>();
            }

            try
            {
                // Jellyfin also queries remote image providers during a metadata refresh, for
                // items that are missing the image type. There is no flag distinguishing that
                // from a user opening the picker, so the candidate count is bounded by what the
                // episode already has: with no primary image the caller is filling a blank and
                // will keep exactly one, and rendering four to discard three would multiply the
                // cost of a first library scan. Replacing an existing poster — which is what
                // Edit Images is for — offers the full set.
                var count = episode.HasImage(ImageType.Primary)
                    ? Math.Clamp(plugin.Configuration.ImageChoiceCount, 1, Services.PosterService.MaxCandidates)
                    : 1;

                var candidates = await plugin.PosterService
                    .GeneratePosterCandidatesAsync(episode, count, cancellationToken)
                    .ConfigureAwait(false);

                if (candidates.Count == 0)
                {
                    _logger.LogWarning("No poster candidates could be generated for {EpisodeName}", episode.Name);
                    return Array.Empty<RemoteImageInfo>();
                }

                var images = new List<RemoteImageInfo>(candidates.Count);
                foreach (var candidate in candidates)
                {
                    var token = plugin.GeneratedImageCache.Add(candidate.Poster);
                    var url = string.Create(
                        CultureInfo.InvariantCulture,
                        $"{baseUrl}/{GeneratedImageRoute}/{token}");

                    images.Add(new RemoteImageInfo
                    {
                        ProviderName = Name,
                        Url = url,
                        ThumbnailUrl = url,
                        Type = ImageType.Primary,
                        Width = candidate.Width,
                        Height = candidate.Height
                    });
                }

                _logger.LogInformation("Offering {Count} generated poster(s) for {EpisodeName}", images.Count, episode.Name);
                return images;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate poster candidates for {EpisodeName}", episode.Name);
                return Array.Empty<RemoteImageInfo>();
            }
        }

        // GetImageResponse
        // Serves a cached candidate directly. Jellyfin calls this on the metadata refresh path
        // instead of going over the network, so the bytes are returned without an HTTP round trip.
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var plugin = Plugin.Instance;
            var token = ExtractToken(url);

            if (plugin != null && token != null && plugin.GeneratedImageCache.TryGet(token, out var bytes))
            {
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            }

            _logger.LogWarning("Generated poster for the requested URL is no longer available");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        // ExtractToken
        // Pulls the cache token off the tail of a generated image URL.
        private static string? ExtractToken(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            var trimmed = url.TrimEnd('/');
            var lastSlash = trimmed.LastIndexOf('/');
            if (lastSlash < 0 || lastSlash == trimmed.Length - 1)
            {
                return null;
            }

            return trimmed[(lastSlash + 1)..];
        }

        // GetLocalBaseUrl
        // Resolves the URL the server can use to reach itself. Loopback over plain HTTP avoids
        // depending on a publicly resolvable hostname or a certificate the server would have to
        // trust when it fetches its own image.
        private string? GetLocalBaseUrl()
        {
            try
            {
                var url = _appHost.GetApiUrlForLocalAccess(IPAddress.Loopback, allowHttps: false);
                if (!string.IsNullOrEmpty(url))
                {
                    return url.TrimEnd('/');
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve the local API URL; falling back to the configured HTTP port");
            }

            var port = _appHost.HttpPort;
            return port > 0
                ? string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}")
                : null;
        }
    }
}
