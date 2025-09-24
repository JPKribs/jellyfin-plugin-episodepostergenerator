using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Services;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Orchestrates complete episode poster workflow including metadata collection, generation, and result handling.
    /// </summary>
    public class EpisodePosterOrchestrator
    {
        private readonly ILogger<EpisodePosterOrchestrator> _logger;
        private readonly PosterGeneratorService _posterGeneratorService;
        private readonly IServerConfigurationManager? _configurationManager;
        private readonly IProviderManager? _providerManager;
        private readonly EpisodeTrackingService? _trackingService;

        // MARK: Constructor
        public EpisodePosterOrchestrator(
            ILogger<EpisodePosterOrchestrator> logger,
            PosterGeneratorService posterGeneratorService,
            IServerConfigurationManager? configurationManager = null,
            IProviderManager? providerManager = null,
            EpisodeTrackingService? trackingService = null)
        {
            _logger = logger;
            _posterGeneratorService = posterGeneratorService;
            _configurationManager = configurationManager;
            _providerManager = providerManager;
            _trackingService = trackingService;
        }

        // MARK: GeneratePoster
        public async Task<string?> GeneratePoster(
            Episode episode,
            PluginConfiguration config,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var encodingOptions = await GetEncodingOptionsAsync().ConfigureAwait(false);
                var metadata = CollectEpisodeMetadata(episode, encodingOptions);
                
                var outputPath = Path.GetTempFileName();
                
                var result = _posterGeneratorService.ProcessImageWithText(
                    metadata.Episode.Path,
                    outputPath,
                    episode,
                    config);
                    
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate poster for episode: {EpisodeName}", episode.Name);
                return null;
            }
        }

        // MARK: ProcessEpisodesAsync
        public async Task<ProcessingResult[]> ProcessEpisodesAsync(
            Episode[] episodes,
            PluginConfiguration config,
            TaskTrigger trigger,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<ProcessingResult>();

            // Get encoding configuration once at start
            EncodingOptions? encodingOptions = null;
            try
            {
                var namedConfig = _configurationManager?.GetConfiguration("encoding");
                if (namedConfig is EncodingOptions options)
                {
                    encodingOptions = options;
                    _logger.LogDebug("Retrieved encoding configuration - HWAccel: {HardwareAccelerationType}, Threads: {EncodingThreadCount}",
                        encodingOptions.HardwareAccelerationType, encodingOptions.EncodingThreadCount);
                }
                else
                {
                    _logger.LogWarning("Could not retrieve encoding configuration - using defaults");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve encoding configuration - using defaults");
            }

            // Collect metadata for all episodes
            var episodeMetadataList = new List<EpisodePosterMetadata>();
            for (int i = 0; i < episodes.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var metadata = CollectEpisodeMetadata(episodes[i], encodingOptions);
                    episodeMetadataList.Add(metadata);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to collect metadata for episode: {EpisodeName}", episodes[i].Name);
                    results.Add(new ProcessingResult 
                    { 
                        Episode = episodes[i], 
                        Success = false, 
                        ErrorMessage = ex.Message 
                    });
                }
            }

            // Generate posters for all episodes with metadata
            var posterPaths = new List<string?>();
            foreach (var metadata in episodeMetadataList)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var posterPath = _posterGeneratorService.ProcessImageWithText(
                        metadata.Episode.Path, 
                        Path.GetTempFileName(), 
                        metadata.Episode, 
                        config);
                    posterPaths.Add(posterPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate poster for episode: {EpisodeName}", metadata.Episode.Name);
                    posterPaths.Add(null);
                }
            }

            // Process results based on trigger type
            for (int i = 0; i < episodeMetadataList.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var metadata = episodeMetadataList[i];
                var posterPath = i < posterPaths.Count ? posterPaths[i] : null;

                var result = await ProcessSingleResult(metadata, posterPath, config, trigger, cancellationToken).ConfigureAwait(false);
                results.Add(result);

                var progressPercentage = (double)(i + 1) / episodeMetadataList.Count * 100;
                progress?.Report(progressPercentage);
            }

            return results.ToArray();
        }

        // MARK: GetEncodingOptionsAsync
        private async Task<EncodingOptions?> GetEncodingOptionsAsync()
        {
            if (_configurationManager == null)
            {
                _logger.LogWarning("Configuration manager not available - using defaults");
                return null;
            }

            try
            {
                var encodingOptions = _configurationManager.GetEncodingOptions();
                _logger.LogDebug("Retrieved encoding configuration - HWAccel: {HardwareAccelerationType}, Threads: {EncodingThreadCount}",
                    encodingOptions.HardwareAccelerationType, encodingOptions.EncodingThreadCount);
                return await Task.FromResult(encodingOptions).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve encoding configuration - using defaults");
                return null;
            }
        }

        // MARK: CollectEpisodeMetadata
        private EpisodePosterMetadata CollectEpisodeMetadata(Episode episode, EncodingOptions? encodingOptions)
        {
            // Get video metadata using utility
            var mediaDetails = BaseItemVideoDetails.GetMediaDetails(episode);

            // Extract episode information
            var seasonNumber = episode.ParentIndexNumber ?? 0;
            var episodeNumber = episode.IndexNumber ?? 0;
            var episodeTitle = episode.Name ?? "Unknown Episode";
            var seriesName = episode.Series?.Name ?? "Unknown Series";

            // Get series logo path if available
            string? seriesLogoPath = null;
            try
            {
                var series = episode.Series;
                if (series != null)
                {
                    var logoPath = series.GetImagePath(ImageType.Logo, 0);
                    if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                    {
                        seriesLogoPath = logoPath;
                    }
                    else
                    {
                        var primaryPath = series.GetImagePath(ImageType.Primary, 0);
                        if (!string.IsNullOrEmpty(primaryPath) && File.Exists(primaryPath))
                        {
                            seriesLogoPath = primaryPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get series logo for episode: {EpisodeName}", episode.Name);
            }

            return new EpisodePosterMetadata
            {
                Episode = episode,
                SeasonNumber = seasonNumber,
                EpisodeNumber = episodeNumber,
                EpisodeTitle = episodeTitle,
                SeriesName = seriesName,
                LogoInfo = new LogoInfo
                {
                    SeriesName = seriesName,
                    SeriesId = episode.Series?.Id ?? Guid.Empty,
                    HasLogo = !string.IsNullOrEmpty(seriesLogoPath),
                    LogoPath = seriesLogoPath
                }
            };
        }

        // MARK: ProcessSingleResult
        private async Task<ProcessingResult> ProcessSingleResult(
            EpisodePosterMetadata metadata,
            string? posterPath,
            PluginConfiguration config,
            TaskTrigger trigger,
            CancellationToken cancellationToken)
        {
            var result = new ProcessingResult { Episode = metadata.Episode };

            if (string.IsNullOrEmpty(posterPath) || !File.Exists(posterPath))
            {
                result.Success = false;
                result.ErrorMessage = "Poster generation failed - no output file";
                return result;
            }

            try
            {
                if (trigger == TaskTrigger.Task)
                {
                    // Task mode: Upload to Jellyfin filesystem and update tracking
                    var success = await UploadToJellyfinAsync(metadata.Episode, posterPath, cancellationToken).ConfigureAwait(false);
                    if (success && _trackingService != null)
                    {
                        await _trackingService.MarkEpisodeProcessedAsync(metadata.Episode, config).ConfigureAwait(false);
                    }
                    result.Success = success;
                    result.PosterPath = success ? posterPath : null;
                }
                else if (trigger == TaskTrigger.Provider)
                {
                    // Provider mode: Load into memory stream for immediate return
                    var imageBytes = await File.ReadAllBytesAsync(posterPath, cancellationToken).ConfigureAwait(false);
                    result.Success = true;
                    result.SetImageData(imageBytes);
                    result.PosterPath = posterPath;

                    // Still mark as processed for tracking
                    if (_trackingService != null)
                    {
                        try
                        {
                            await _trackingService.MarkEpisodeProcessedAsync(metadata.Episode, config).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to update tracking for provider-generated poster: {EpisodeName}", metadata.Episode.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process result for episode: {EpisodeName}", metadata.Episode.Name);
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        // MARK: UploadToJellyfinAsync
        private async Task<bool> UploadToJellyfinAsync(Episode episode, string imagePath, CancellationToken cancellationToken)
        {
            if (_providerManager == null)
            {
                _logger.LogError("Provider manager not available for uploading");
                return false;
            }

            try
            {
                var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken).ConfigureAwait(false);
                using var imageStream = new MemoryStream(imageBytes);

                await _providerManager.SaveImage(
                    episode,
                    imageStream,
                    "image/jpeg",
                    ImageType.Primary,
                    null,
                    cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Successfully uploaded poster for episode: {EpisodeName}", episode.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload image for episode: {EpisodeName}", episode.Name);
                return false;
            }
        }
    }
}