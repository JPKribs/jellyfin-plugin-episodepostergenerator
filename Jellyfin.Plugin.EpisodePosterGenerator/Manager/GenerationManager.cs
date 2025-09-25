using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Services;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.Media;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Managers
{
    /// <summary>
    /// Orchestrates complete episode poster workflow including metadata collection, generation, and result handling.
    /// </summary>
    public class GenerationManager
    {
        private readonly ILogger<GenerationManager> _logger;
        private readonly PosterGeneratorService _posterGeneratorService;
        private readonly FFmpegManager _ffmpegManager;
        private readonly IServerConfigurationManager? _configurationManager;
        private readonly IProviderManager? _providerManager;
        private readonly EpisodeTrackingService? _trackingService;

        // MARK: Constructor
        public GenerationManager(
            ILogger<GenerationManager> logger,
            PosterGeneratorService posterGeneratorService,
            FFmpegManager ffmpegManager,
            IServerConfigurationManager? configurationManager = null,
            IProviderManager? providerManager = null,
            EpisodeTrackingService? trackingService = null)
        {
            _logger = logger;
            _posterGeneratorService = posterGeneratorService;
            _ffmpegManager = ffmpegManager;
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

                // Extract frame from video using sophisticated FFmpegManager
                var extractedFramePath = await ExtractVideoFrameAsync(metadata, config, cancellationToken).ConfigureAwait(false);
                
                if (string.IsNullOrEmpty(extractedFramePath))
                {
                    _logger.LogWarning("Failed to extract frame from video: {VideoPath}", metadata.Episode.Path);
                    return null;
                }

                // Generate poster from extracted frame
                var outputPath = Path.GetTempFileName() + ".jpg";
                var result = _posterGeneratorService.ProcessImageWithText(
                    extractedFramePath,
                    outputPath,
                    episode,
                    config);

                // Cleanup temporary extracted frame
                try
                {
                    if (File.Exists(extractedFramePath))
                    {
                        File.Delete(extractedFramePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup temporary frame: {FramePath}", extractedFramePath);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate poster for episode: {EpisodeName}", episode.Name);
                return null;
            }
        }

        // MARK: ExtractVideoFrameAsync
        private async Task<string?> ExtractVideoFrameAsync(
            EpisodePosterMetadata metadata,
            PluginConfiguration config,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(metadata.Episode.Path) || !File.Exists(metadata.Episode.Path))
            {
                _logger.LogWarning("Video file not found: {VideoPath}", metadata.Episode.Path);
                return null;
            }

            try
            {
                // Get video duration for smart timestamp calculation
                var duration = await _ffmpegManager.GetVideoDurationAsync(metadata.Episode.Path, cancellationToken).ConfigureAwait(false);
                if (!duration.HasValue)
                {
                    _logger.LogWarning("Could not determine video duration for: {VideoPath}", metadata.Episode.Path);
                    duration = TimeSpan.FromMinutes(45); // Default assumption
                }

                // Calculate optimal timestamp based on configuration
                var timestamp = CalculateExtractionTimestamp(duration.Value, config);
                
                _logger.LogDebug("Extracting frame at {Timestamp} from {VideoPath}", timestamp, metadata.Episode.Path);

                // Use FFmpegManager for intelligent frame extraction (HW/SW routing + black scene avoidance)
                var extractedFramePath = await _ffmpegManager.ExtractFrameAsync(
                    metadata.Episode.Path,
                    timestamp,
                    cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrEmpty(extractedFramePath) || !File.Exists(extractedFramePath))
                {
                    // Fallback: try different timestamp if first attempt failed
                    var fallbackTimestamp = CalculateFallbackTimestamp(duration.Value, timestamp);
                    _logger.LogDebug("Retrying frame extraction at fallback timestamp: {Timestamp}", fallbackTimestamp);
                    
                    extractedFramePath = await _ffmpegManager.ExtractFrameAsync(
                        metadata.Episode.Path,
                        fallbackTimestamp,
                        cancellationToken).ConfigureAwait(false);
                }

                return extractedFramePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract frame from video: {VideoPath}", metadata.Episode.Path);
                return null;
            }
        }

        // MARK: CalculateExtractionTimestamp  
        private TimeSpan CalculateExtractionTimestamp(TimeSpan duration, PluginConfiguration config)
        {
            // Use the frame extraction method from config if it exists, otherwise default to beginning
            var extractionMethod = config.ExtractionMethod;
            
            return extractionMethod switch
            {
                FrameExtractionMethod.Beginning => TimeSpan.FromSeconds(Math.Min(30, duration.TotalSeconds * 0.1)),
                FrameExtractionMethod.Middle => TimeSpan.FromSeconds(duration.TotalSeconds * 0.5),
                FrameExtractionMethod.End => TimeSpan.FromSeconds(duration.TotalSeconds * 0.8),
                FrameExtractionMethod.Custom => TimeSpan.FromSeconds(config.CustomTimestampSeconds),
                _ => TimeSpan.FromSeconds(Math.Min(30, duration.TotalSeconds * 0.1))
            };
        }

        // MARK: CalculateFallbackTimestamp
        private TimeSpan CalculateFallbackTimestamp(TimeSpan duration, TimeSpan originalTimestamp)
        {
            // Try middle of video as fallback
            if (originalTimestamp < TimeSpan.FromSeconds(duration.TotalSeconds * 0.5))
            {
                return TimeSpan.FromSeconds(duration.TotalSeconds * 0.5);
            }
            else
            {
                return TimeSpan.FromSeconds(duration.TotalSeconds * 0.25);
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
            var encodingOptions = await GetEncodingOptionsAsync().ConfigureAwait(false);

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
                    var posterPath = await GeneratePoster(metadata.Episode, config, cancellationToken).ConfigureAwait(false);
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
                var posterPath = posterPaths[i];

                var result = await ProcessSingleResult(metadata, posterPath, config, trigger, cancellationToken).ConfigureAwait(false);
                results.Add(result);
                
                progress?.Report((double)(i + 1) / episodeMetadataList.Count * 100);
            }

            return results.ToArray();
        }

        // MARK: GetEncodingOptionsAsync
        private async Task<EncodingOptions> GetEncodingOptionsAsync()
        {
            if (_configurationManager == null)
            {
                _logger.LogWarning("Configuration manager not available, using default encoding options");
                return new EncodingOptions();
            }

            try
            {
                var encodingOptions = _configurationManager.GetEncodingOptions();
                return encodingOptions ?? new EncodingOptions();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get encoding options, using defaults");
                return new EncodingOptions();
            }
        }

        // MARK: CollectEpisodeMetadata
        private EpisodePosterMetadata CollectEpisodeMetadata(Episode episode, EncodingOptions? encodingOptions)
        {
            // Get video metadata using utility
            var mediaDetails = BaseItemVideoDetails.GetMediaDetails(episode);

            // Extract episode information
            var seasonNumber = episode.ParentIndexNumber;
            var episodeNumber = episode.IndexNumber;
            var episodeTitle = episode.Name;
            var seriesName = episode.Series?.Name;

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
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get series logo for episode: {SeriesName} - {EpisodeName}", episode.Series?.Name, episode.Name);
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
                            _logger.LogWarning(ex, "Failed to update tracking for provider-generated poster: {SeriesName} - {EpisodeName}", metadata.Episode.Series?.Name, metadata.Episode.Name);
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

                _logger.LogInformation("Successfully uploaded poster for episode: {SeriesName} - {EpisodeName}", episode.Series?.Name, episode.Name);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload image for episode: {SeriesName} - {EpisodeName}", episode.Series?.Name, episode.Name);
                return false;
            }
        }
    }
}