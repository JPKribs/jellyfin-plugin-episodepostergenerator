using System;
using System.Collections.Generic;
using System.IO;
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
using SkiaSharp;

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

            var config = Plugin.Instance?.Configuration;
            if (config == null || !config.EnablePlugin)
            {
                _logger.LogInformation("Episode Poster Generator is disabled via configuration.");
                return new DynamicImageResponse { HasImage = false };
            }

            if (item is not Episode episode)
            {
                _logger.LogWarning("Item is not an Episode.");
                return new DynamicImageResponse { HasImage = false };
            }

            if (type != ImageType.Primary)
            {
                _logger.LogInformation("Image type {ImageType} not supported.", type);
                return new DynamicImageResponse { HasImage = false };
            }

            if (config.PosterStyle != PosterStyle.Numeral && (string.IsNullOrEmpty(episode.Path) || !File.Exists(episode.Path)))
            {
                _logger.LogInformation("Episode \"{EpisodeName}\" has no valid video file.", episode.Name);
                return new DynamicImageResponse { HasImage = false };
            }

            try
            {
                _logger.LogInformation("Processing episode: \"{EpisodeName}\" with style: {PosterStyle}", episode.Name, config.PosterStyle);

                var imageStream = await GenerateEpisodeImageAsync(episode, cancellationToken).ConfigureAwait(false);
                if (imageStream == null)
                {
                    _logger.LogWarning("Failed to generate image for episode: \"{EpisodeName}\"", episode.Name);
                    return new DynamicImageResponse { HasImage = false };
                }

                // Mark episode as processed in tracking service
                var trackingService = Plugin.Instance?.TrackingService;
                if (trackingService != null)
                {
                    try
                    {
                        await trackingService.MarkEpisodeProcessedAsync(episode, config).ConfigureAwait(false);
                        _logger.LogDebug("Marked episode as processed in tracking service: {EpisodeName}", episode.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to mark episode as processed in tracking service: {EpisodeName}", episode.Name);
                    }
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
                _logger.LogInformation("Starting optimized poster generation for episode: \"{EpisodeName}\"", episode.Name);

                var config = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();

                var ffmpegService = Plugin.Instance?.FFmpegService;
                var posterGeneratorService = Plugin.Instance?.PosterGeneratorService;

                if (ffmpegService == null || posterGeneratorService == null)
                {
                    _logger.LogError("Plugin services not available");
                    return null;
                }

                var tempDir = Path.Combine(_appPaths.TempDirectory, "episodeposter");
                Directory.CreateDirectory(tempDir);

                var tempFramePath = Path.Combine(tempDir, $"frame_{episode.Id}_{DateTime.UtcNow.Ticks}.jpg");
                var tempPosterPath = Path.Combine(tempDir, $"poster_{episode.Id}_{DateTime.UtcNow.Ticks}.jpg");

                try
                {
                    string? extractedFramePath;

                    if (config.PosterStyle == PosterStyle.Numeral)
                    {
                        _logger.LogDebug("Creating transparent background for Numeral style");
                        extractedFramePath = CreateTransparentImage(tempFramePath);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(episode.Path) || !File.Exists(episode.Path))
                        {
                            _logger.LogWarning("Episode video file not found: \"{Path}\"", episode.Path);
                            return null;
                        }

                        var duration = GetDurationFromEpisode(episode);
                        if (!duration.HasValue)
                        {
                            _logger.LogInformation("Episode duration not available from metadata, falling back to FFprobe");
                            duration = await ffmpegService.GetVideoDurationAsync(episode.Path, cancellationToken).ConfigureAwait(false);
                        }

                        if (!duration.HasValue)
                        {
                            _logger.LogWarning("Could not get video duration for: \"{Path}\"", episode.Path);
                            return null;
                        }

                        _logger.LogDebug("Video duration: {Duration} for episode: \"{EpisodeName}\"", duration.Value, episode.Name);

                        var blackIntervals = await ffmpegService.DetectBlackScenesAsync(episode.Path, duration.Value, 0.1, 0.1, cancellationToken).ConfigureAwait(false);
                        var selectedTimestamp = ffmpegService.SelectRandomTimestamp(duration.Value, blackIntervals);

                        _logger.LogDebug("Random timestamp selected: {Timestamp} for episode: \"{EpisodeName}\"", selectedTimestamp, episode.Name);

                        extractedFramePath = await ffmpegService.ExtractFrameAsync(episode.Path, selectedTimestamp, tempFramePath, cancellationToken).ConfigureAwait(false);
                    }

                    if (extractedFramePath == null || !File.Exists(extractedFramePath))
                    {
                        _logger.LogWarning("Failed to create source image");
                        return null;
                    }

                    var processedPath = posterGeneratorService.ProcessImageWithText(extractedFramePath, tempPosterPath, episode, config);
                    if (processedPath == null || !File.Exists(processedPath))
                    {
                        _logger.LogWarning("Failed to process image with text overlay");
                        return null;
                    }

                    var imageBytes = await File.ReadAllBytesAsync(processedPath, cancellationToken).ConfigureAwait(false);
                    return new MemoryStream(imageBytes);
                }
                finally
                {
                    if (File.Exists(tempFramePath))
                    {
                        try { File.Delete(tempFramePath); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temp frame: \"{Path}\"", tempFramePath); }
                    }

                    if (File.Exists(tempPosterPath))
                    {
                        try { File.Delete(tempPosterPath); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temp poster: \"{Path}\"", tempPosterPath); }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating poster for episode: \"{EpisodeName}\"", episode.Name);
                return null;
            }
        }

        // MARK: GetDurationFromEpisode
        private TimeSpan? GetDurationFromEpisode(Episode episode)
        {
            if (episode.RunTimeTicks.HasValue)
            {
                return TimeSpan.FromTicks(episode.RunTimeTicks.Value);
            }
            
            return null;
        }

        // MARK: CreateTransparentImage
        private string? CreateTransparentImage(string outputPath)
        {
            try
            {
                using var bitmap = new SKBitmap(3000, 2000);
                using var canvas = new SKCanvas(bitmap);

                canvas.Clear(SKColors.Transparent);

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
                using var outputStream = File.OpenWrite(outputPath);
                data.SaveTo(outputStream);

                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create transparent image at: \"{Path}\"", outputPath);
                return null;
            }
        }
    }
}