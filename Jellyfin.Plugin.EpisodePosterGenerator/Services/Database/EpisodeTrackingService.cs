using System;
using System.IO;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.Database;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services;

public class EpisodeTrackingService
{
    private readonly ILogger<EpisodeTrackingService> _logger;
    private readonly EpisodeTrackingDatabase _database;
    private readonly ConfigurationHashService _configHashService;

    // EpisodeTrackingService
    // Initializes the tracking service with database and configuration hash dependencies.
    public EpisodeTrackingService(
        ILogger<EpisodeTrackingService> logger,
        EpisodeTrackingDatabase database,
        ConfigurationHashService configHashService)
    {
        _logger = logger;
        _database = database;
        _configHashService = configHashService;
    }

    // ShouldProcessEpisodeAsync
    // Determines if an episode needs to be processed based on file and config changes.
    public async Task<bool> ShouldProcessEpisodeAsync(Episode episode, PluginConfiguration config)
    {
        if (episode?.Id == null || string.IsNullOrEmpty(episode.Path))
        {
            return false;
        }

        var record = await _database.GetProcessedEpisodeAsync(episode.Id).ConfigureAwait(false);
        if (record == null)
        {
            return true;
        }

        if (HasVideoFileChanged(episode, record))
        {
            _logger.LogDebug("Video file changed for episode {EpisodeId}, requires reprocessing", episode.Id);
            return true;
        }

        var posterSettings = Plugin.Instance!.PosterConfigService.GetSettingsForEpisode(episode);
        var currentConfigHash = _configHashService.GetCurrentHash(posterSettings);

        if (record.ConfigurationHash != currentConfigHash)
        {
            _logger.LogDebug("Configuration changed for episode {EpisodeId}, requires reprocessing", episode.Id);
            return true;
        }

        if (IsImageModifiedAfterProcessing(episode, record))
        {
            _logger.LogDebug("Image manually modified for episode {EpisodeId}, skipping automatic reprocessing", episode.Id);
            return false;
        }

        return false;
    }

    // MarkEpisodeProcessedAsync
    // Records that an episode has been successfully processed with current settings.
    public async Task MarkEpisodeProcessedAsync(Episode episode, PluginConfiguration config)
    {
        if (string.IsNullOrEmpty(episode.Path) || !File.Exists(episode.Path))
        {
            return;
        }

        var fileInfo = new FileInfo(episode.Path);
        var posterSettings = Plugin.Instance!.PosterConfigService.GetSettingsForEpisode(episode);
        var configHash = _configHashService.GetCurrentHash(posterSettings);

        var record = new ProcessedEpisodeRecord
        {
            EpisodeId = episode.Id,
            LastProcessed = DateTime.UtcNow,
            VideoFilePath = episode.Path,
            VideoFileSize = fileInfo.Length,
            VideoFileLastModified = fileInfo.LastWriteTime,
            ConfigurationHash = configHash
        };

        await _database.SaveProcessedEpisodeAsync(record).ConfigureAwait(false);
    }

    // GetProcessedCountAsync
    // Returns the total number of processed episodes.
    public async Task<int> GetProcessedCountAsync()
    {
        return await _database.GetProcessedCountAsync().ConfigureAwait(false);
    }

    // ClearAllProcessedEpisodesAsync
    // Clears all processed episode tracking records.
    public async Task ClearAllProcessedEpisodesAsync()
    {
        await _database.ClearAllProcessedEpisodesAsync().ConfigureAwait(false);
    }

    // RemoveProcessedEpisodeAsync
    // Removes the tracking record for a specific episode.
    public async Task RemoveProcessedEpisodeAsync(Guid episodeId)
    {
        await _database.RemoveProcessedEpisodeAsync(episodeId).ConfigureAwait(false);
    }

    // HasVideoFileChanged
    // Checks if the video file has been modified since last processing.
    private bool HasVideoFileChanged(Episode episode, ProcessedEpisodeRecord record)
    {
        if (!File.Exists(episode.Path))
        {
            return true;
        }

        var fileInfo = new FileInfo(episode.Path);

        return record.VideoFilePath != episode.Path ||
               record.VideoFileSize != fileInfo.Length ||
               record.VideoFileLastModified != fileInfo.LastWriteTime;
    }

    // IsImageModifiedAfterProcessing
    // Checks if the episode image was manually modified after processing.
    private bool IsImageModifiedAfterProcessing(Episode episode, ProcessedEpisodeRecord record)
    {
        try
        {
            var imagePath = episode.GetImagePath(ImageType.Primary, 0);
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                return true;
            }

            var imageFileInfo = new FileInfo(imagePath);
            var imageLastModified = imageFileInfo.LastWriteTime;

            if (imageLastModified > record.LastProcessed)
            {
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check image modification time for episode {EpisodeId}, will reprocess", episode.Id);
            return true;
        }
    }
}
