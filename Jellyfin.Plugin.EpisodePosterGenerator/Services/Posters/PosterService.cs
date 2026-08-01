using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;
using Jellyfin.Plugin.EpisodePosterGenerator.Utilities;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    public class PosterService
    {
        /// <summary>
        /// Upper bound on how many alternate posters a single request may ask for. Each one
        /// costs a frame extraction, so this caps the worst case for the Edit Images picker.
        /// </summary>
        public const int MaxCandidates = 10;

        private readonly ILogger<PosterService> _logger;
        private readonly CanvasService _canvasService;
        private readonly ILoggerFactory _loggerFactory;

        // PosterService
        // Initializes the poster service with canvas and configuration dependencies.
        public PosterService(
            ILogger<PosterService> logger,
            CanvasService canvasService,
            ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _canvasService = canvasService;
            _loggerFactory = loggerFactory;
        }

        // GeneratePosterAsync
        // Generates a single poster for an episode, plus an optional backdrop derived from
        // the extracted canvas. Returns null when no usable frame or canvas could be produced.
        public async Task<PosterGenerationResult?> GeneratePosterAsync(Episode episode, CancellationToken cancellationToken = default)
        {
            var results = await GeneratePosterCandidatesAsync(episode, 1, cancellationToken).ConfigureAwait(false);
            return results.Count > 0 ? results[0] : null;
        }

        // GeneratePosterCandidatesAsync
        // Generates up to <paramref name="count"/> posters for an episode, each drawn over a
        // different extracted frame so the Edit Images picker can offer a real choice. Styles
        // that do not extract frames have only one possible output, so a single result is returned.
        public async Task<IReadOnlyList<PosterGenerationResult>> GeneratePosterCandidatesAsync(
            Episode episode,
            int count,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(episode);

            if (Plugin.Instance == null)
            {
                _logger.LogError("Plugin instance not available");
                return Array.Empty<PosterGenerationResult>();
            }

            var posterSettings = Plugin.Instance.PosterConfigService.GetSettingsForEpisode(episode);

            // Only an extracted frame varies between candidates; every other canvas source
            // produces an identical poster, so asking for more would just burn CPU.
            var requested = posterSettings.CanvasSource == CanvasSource.Extract
                ? Math.Clamp(count, 1, MaxCandidates)
                : 1;

            _logger.LogInformation(
                "Generating {Count} poster candidate(s) for {SeriesName} - {EpisodeName}",
                requested,
                episode.Series?.Name ?? "Unknown Series",
                episode.Name ?? "Unknown Episode");

            var episodeMetadata = EpisodeMetadata.CreateFromEpisode(episode);

            var canvases = await _canvasService
                .GenerateCanvasesAsync(episode, episodeMetadata, posterSettings, requested, cancellationToken)
                .ConfigureAwait(false);

            if (canvases.Count == 0)
            {
                _logger.LogWarning("Failed to generate any canvas for episode: {EpisodeName}", episode.Name);
                return Array.Empty<PosterGenerationResult>();
            }

            var generator = PreviewService.CreateGenerator(posterSettings.PosterStyle, _loggerFactory);

            // Only the single-candidate call feeds the refresh path, which is the one that uploads
            // a backdrop. Encoding one per candidate for the image picker would spend a full-size
            // JPEG encode on every alternate poster and then discard all of them.
            var wantsBackdrop = posterSettings.CanvasSource == CanvasSource.Extract
                && posterSettings.GenerateBackdrop
                && canvases.Count == 1;

            var results = new List<PosterGenerationResult>(canvases.Count);

            try
            {
                foreach (var canvas in canvases)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // The backdrop is the bare cropped canvas, captured before the poster
                    // layers are rendered on top of it.
                    var backdrop = wantsBackdrop ? EncodeJpeg(canvas) : null;

                    var poster = generator.Generate(canvas, episodeMetadata, posterSettings);
                    if (poster == null)
                    {
                        _logger.LogWarning("Poster rendering failed for episode: {EpisodeName}", episode.Name);
                        continue;
                    }

                    results.Add(new PosterGenerationResult(poster, backdrop, canvas.Width, canvas.Height));
                }
            }
            finally
            {
                foreach (var canvas in canvases)
                {
                    canvas.Dispose();
                }
            }

            if (results.Count > 0)
            {
                _logger.LogInformation(
                    "Generated {Count} poster(s) for {SeriesName} - {EpisodeName}",
                    results.Count,
                    episode.Series?.Name ?? "Unknown Series",
                    episode.Name ?? "Unknown Episode");
            }

            return results;
        }

        // EncodeJpeg
        // Encodes a bitmap to JPEG bytes, returning null if encoding fails.
        private byte[]? EncodeJpeg(SKBitmap bitmap)
        {
            try
            {
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, RenderConstants.JpegQuality);
                return data?.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to encode backdrop image");
                return null;
            }
        }
    }
}
