using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    public class EpisodeTrackingService
    {
        private readonly ILogger<EpisodeTrackingService> _logger;
        private readonly string _trackingFilePath;
        private Dictionary<Guid, ProcessedEpisodeRecord> _processedEpisodes;

        private static readonly JsonSerializerOptions SaveOptions = new()
        {
            WriteIndented = true
        };

        private static readonly JsonSerializerOptions HashOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // MARK: Constructor
        public EpisodeTrackingService(ILogger<EpisodeTrackingService> logger, IApplicationPaths appPaths)
        {
            _logger = logger;
            var dataPath = Path.Combine(appPaths.DataPath, "episodeposter");
            Directory.CreateDirectory(dataPath);
            _trackingFilePath = Path.Combine(dataPath, "processed_episodes.json");
            _processedEpisodes = new Dictionary<Guid, ProcessedEpisodeRecord>();
            
            LoadTrackingData();
        }

        // MARK: ShouldProcessEpisode
        public bool ShouldProcessEpisode(Episode episode, PluginConfiguration config)
        {
            if (string.IsNullOrEmpty(episode.Path) || !File.Exists(episode.Path))
            {
                return false;
            }

            // Always process if episode has no primary image
            if (!episode.HasImage(ImageType.Primary, 0))
            {
                _logger.LogDebug("Episode {EpisodeName} has no primary image, should process", episode.Name);
                return true;
            }

            if (!_processedEpisodes.TryGetValue(episode.Id, out var record))
            {
                _logger.LogDebug("Episode {EpisodeName} not in tracking records, should process", episode.Name);
                return true;
            }

            var fileInfo = new FileInfo(episode.Path);
            var currentConfigHash = GenerateConfigurationHash(config);

            // Check if video file or config changed
            var shouldReprocess = record.ShouldReprocess(
                episode.Path,
                fileInfo.Length,
                fileInfo.LastWriteTime,
                currentConfigHash
            );

            if (shouldReprocess)
            {
                _logger.LogDebug("Episode {EpisodeName} video or config changed, should process", episode.Name);
                return true;
            }

            // Check if image was modified after we last processed it
            var imageModifiedAfterProcessing = IsImageModifiedAfterProcessing(episode, record);
            if (imageModifiedAfterProcessing)
            {
                _logger.LogDebug("Episode {EpisodeName} image was modified after processing, should reprocess", episode.Name);
                return true;
            }

            _logger.LogDebug("Episode {EpisodeName} does not need processing", episode.Name);
            return false;
        }

        // MARK: MarkEpisodeProcessed
        public async Task MarkEpisodeProcessedAsync(Episode episode, PluginConfiguration config)
        {
            if (string.IsNullOrEmpty(episode.Path) || !File.Exists(episode.Path))
            {
                return;
            }

            var fileInfo = new FileInfo(episode.Path);
            var configHash = GenerateConfigurationHash(config);

            var record = new ProcessedEpisodeRecord
            {
                EpisodeId = episode.Id,
                LastProcessed = DateTime.UtcNow,
                VideoFilePath = episode.Path,
                VideoFileSize = fileInfo.Length,
                VideoFileLastModified = fileInfo.LastWriteTime,
                ConfigurationHash = configHash
            };

            _processedEpisodes[episode.Id] = record;
            await SaveTrackingDataAsync().ConfigureAwait(false);
        }

        // MARK: GetProcessedCount
        public int GetProcessedCount()
        {
            return _processedEpisodes.Count;
        }

        // MARK: ClearProcessedRecords
        public async Task ClearProcessedRecordsAsync()
        {
            _processedEpisodes.Clear();
            await SaveTrackingDataAsync().ConfigureAwait(false);
            _logger.LogInformation("Cleared all processed episode records");
        }

        // MARK: RemoveProcessedRecord
        public async Task RemoveProcessedRecordAsync(Guid episodeId)
        {
            if (_processedEpisodes.Remove(episodeId))
            {
                await SaveTrackingDataAsync().ConfigureAwait(false);
                _logger.LogDebug("Removed processed record for episode: {EpisodeId}", episodeId);
            }
        }

        // MARK: ForceReprocessEpisode
        public async Task ForceReprocessEpisodeAsync(Guid episodeId)
        {
            if (_processedEpisodes.Remove(episodeId))
            {
                await SaveTrackingDataAsync().ConfigureAwait(false);
                _logger.LogInformation("Forced reprocessing for episode: {EpisodeId}", episodeId);
            }
        }

        // MARK: IsImageModifiedAfterProcessing
        private bool IsImageModifiedAfterProcessing(Episode episode, ProcessedEpisodeRecord record)
        {
            try
            {
                var imagePath = episode.GetImagePath(ImageType.Primary, 0);
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                {
                    // Image was deleted, should reprocess
                    return true;
                }

                var imageFileInfo = new FileInfo(imagePath);
                var imageLastModified = imageFileInfo.LastWriteTime;

                // If image was modified after we last processed the episode, reprocess
                if (imageLastModified > record.LastProcessed)
                {
                    _logger.LogDebug("Image for episode {EpisodeId} was modified {ImageModified} after last processing {LastProcessed}", 
                        episode.Id, imageLastModified, record.LastProcessed);
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

        // MARK: LoadTrackingData
        private void LoadTrackingData()
        {
            try
            {
                if (!File.Exists(_trackingFilePath))
                {
                    _logger.LogInformation("No existing tracking file found, starting fresh");
                    return;
                }

                var json = File.ReadAllText(_trackingFilePath);
                var records = JsonSerializer.Deserialize<List<ProcessedEpisodeRecord>>(json);
                
                if (records != null)
                {
                    _processedEpisodes.Clear();
                    foreach (var record in records)
                    {
                        _processedEpisodes[record.EpisodeId] = record;
                    }
                    
                    _logger.LogInformation("Loaded {Count} processed episode records", _processedEpisodes.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load tracking data, starting fresh");
                _processedEpisodes.Clear();
            }
        }

        // MARK: SaveTrackingDataAsync
        private async Task SaveTrackingDataAsync()
        {
            try
            {
                var records = new List<ProcessedEpisodeRecord>(_processedEpisodes.Values);
                var json = JsonSerializer.Serialize(records, SaveOptions);
                
                await File.WriteAllTextAsync(_trackingFilePath, json).ConfigureAwait(false);
                _logger.LogDebug("Saved tracking data for {Count} episodes", records.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save tracking data");
            }
        }

        // MARK: GenerateConfigurationHash
        private string GenerateConfigurationHash(PluginConfiguration config)
        {
            var configString = JsonSerializer.Serialize(config, HashOptions);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(configString));
            return Convert.ToBase64String(hash);
        }
    }
}