using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Providers;

public class EpisodePosterImageProvider : IDynamicImageProvider
{
    private readonly ILogger<EpisodePosterImageProvider> _logger;
    private readonly IApplicationPaths _appPaths;

    public EpisodePosterImageProvider(ILogger<EpisodePosterImageProvider> logger, IApplicationPaths appPaths)
    {
        _logger = logger;
        _appPaths = appPaths;
        _logger.LogInformation("Episode Poster Generator image provider initialized");
    }

    public string Name => "Episode Poster Generator";

    public bool Supports(BaseItem item)
    {
        var isEpisode = item is Episode;
        
        _logger.LogInformation("Supports check - Item: \"{ItemName}\", IsEpisode: {IsEpisode}", 
            item.Name ?? "null", isEpisode);
        
        if (isEpisode)
        {
            _logger.LogInformation("Supporting episode for library-level configuration");
            return true;
        }
        
        _logger.LogInformation("Not supporting item type: \"{ItemType}\"", item.GetType().Name);
        return false;
    }

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        _logger.LogInformation("GetSupportedImages called for item: \"{ItemName}\" (Type: \"{ItemType}\")", item.Name, item.GetType().Name);
        
        if (item is Episode)
        {
            _logger.LogInformation("Returning Primary image support for episode: \"{EpisodeName}\"", item.Name);
            yield return ImageType.Primary;
        }
        else
        {
            _logger.LogInformation("Item is not an Episode, returning no supported images");
        }
    }

    public async Task<DynamicImageResponse> GetImage(BaseItem item, ImageType type, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GetImage called for item: \"{ItemName}\" (Type: \"{ItemType}\"), ImageType: {ImageType}", 
            item.Name, item.GetType().Name, type);

        if (item is not Episode episode)
        {
            _logger.LogWarning("Item is not an Episode, returning empty response");
            return new DynamicImageResponse { HasImage = false };
        }

        if (type != ImageType.Primary)
        {
            _logger.LogInformation("Image type {ImageType} not supported, only Primary is supported", type);
            return new DynamicImageResponse { HasImage = false };
        }

        if (string.IsNullOrEmpty(episode.Path) || !File.Exists(episode.Path))
        {
            _logger.LogInformation("Episode \"{EpisodeName}\" has no valid video file, skipping", episode.Name);
            return new DynamicImageResponse { HasImage = false };
        }

        try
        {
            _logger.LogInformation("Processing episode: \"{EpisodeName}\" at \"{Path}\"", episode.Name, episode.Path);

            var imageStream = await GenerateEpisodeImageAsync(episode, cancellationToken).ConfigureAwait(false);
            if (imageStream == null)
            {
                _logger.LogWarning("Failed to generate image for episode: \"{EpisodeName}\"", episode.Name);
                return new DynamicImageResponse { HasImage = false };
            }

            _logger.LogInformation("Successfully generated poster for episode: \"{EpisodeName}\"", episode.Name);
            
            return new DynamicImageResponse
            {
                HasImage = true,
                Stream = imageStream,
                Format = ImageFormat.Jpg
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating image for episode: \"{EpisodeName}\"", episode.Name);
            return new DynamicImageResponse { HasImage = false };
        }
    }

    // MARK: GenerateEpisodeImageAsync
    private async Task<Stream?> GenerateEpisodeImageAsync(Episode episode, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting poster generation for episode: \"{EpisodeName}\"", episode.Name);

            if (string.IsNullOrEmpty(episode.Path) || !File.Exists(episode.Path))
            {
                _logger.LogWarning("Episode video file not found: \"{Path}\"", episode.Path);
                return null;
            }

            var ffmpegService = new FFmpegService(Microsoft.Extensions.Logging.Abstractions.NullLogger<FFmpegService>.Instance);
            var imageService = new ImageProcessingService(Microsoft.Extensions.Logging.Abstractions.NullLogger<ImageProcessingService>.Instance);
            
            var tempDir = Path.Combine(_appPaths.TempDirectory, "episodeposter");
            Directory.CreateDirectory(tempDir);

            var tempFramePath = Path.Combine(tempDir, $"frame_{episode.Id}_{DateTime.UtcNow.Ticks}.jpg");
            var tempPosterPath = Path.Combine(tempDir, $"poster_{episode.Id}_{DateTime.UtcNow.Ticks}.jpg");

            try
            {
                var duration = await ffmpegService.GetVideoDurationAsync(episode.Path, cancellationToken).ConfigureAwait(false);
                if (!duration.HasValue)
                {
                    _logger.LogWarning("Could not get video duration for: \"{Path}\"", episode.Path);
                    return null;
                }

                _logger.LogInformation("Video duration: {Duration}", duration.Value);

                var blackIntervals = await ffmpegService.DetectBlackScenesAsync(episode.Path, 0.1, 0.1, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Detected {Count} black intervals", blackIntervals.Count);

                var selectedTimestamp = SelectOptimalTimestamp(duration.Value, blackIntervals);
                _logger.LogInformation("Selected timestamp: {Timestamp}", selectedTimestamp);

                var extractedFramePath = await ffmpegService.ExtractFrameAsync(episode.Path, selectedTimestamp, tempFramePath, cancellationToken).ConfigureAwait(false);
                if (extractedFramePath == null || !File.Exists(extractedFramePath))
                {
                    _logger.LogWarning("Failed to extract frame at timestamp: {Timestamp}", selectedTimestamp);
                    return null;
                }

                _logger.LogInformation("Successfully extracted frame to: \"{Path}\"", extractedFramePath);

                var config = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();
                var processedPath = imageService.ProcessImageWithText(extractedFramePath, tempPosterPath, episode, config);
                
                if (processedPath == null || !File.Exists(processedPath))
                {
                    _logger.LogWarning("Failed to process image with text overlay");
                    return null;
                }

                _logger.LogInformation("Successfully processed image with text overlay to: \"{Path}\"", processedPath);

                var imageBytes = await File.ReadAllBytesAsync(processedPath, cancellationToken).ConfigureAwait(false);
                return new MemoryStream(imageBytes);
            }
            finally
            {
                if (File.Exists(tempFramePath))
                {
                    try
                    {
                        File.Delete(tempFramePath);
                        _logger.LogDebug("Cleaned up temp frame file: \"{Path}\"", tempFramePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to clean up temp frame file: \"{Path}\"", tempFramePath);
                    }
                }
                
                if (File.Exists(tempPosterPath))
                {
                    try
                    {
                        File.Delete(tempPosterPath);
                        _logger.LogDebug("Cleaned up temp poster file: \"{Path}\"", tempPosterPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to clean up temp poster file: \"{Path}\"", tempPosterPath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating poster for episode: \"{EpisodeName}\"", episode.Name);
            return null;
        }
    }

    // MARK: SelectOptimalTimestamp
    private TimeSpan SelectOptimalTimestamp(TimeSpan duration, List<BlackInterval> blackIntervals)
    {
        var candidates = new[]
        {
            TimeSpan.FromSeconds(duration.TotalSeconds * 0.25),
            TimeSpan.FromSeconds(duration.TotalSeconds * 0.5),
            TimeSpan.FromSeconds(duration.TotalSeconds * 0.75),
            TimeSpan.FromSeconds(duration.TotalSeconds * 0.1),
            TimeSpan.FromSeconds(duration.TotalSeconds * 0.9)
        };

        foreach (var candidate in candidates)
        {
            var isInBlackInterval = blackIntervals.Any(interval => 
                candidate >= interval.Start && candidate <= interval.End);

            if (!isInBlackInterval)
            {
                return candidate;
            }
        }

        return TimeSpan.FromSeconds(duration.TotalSeconds * 0.5);
    }
}