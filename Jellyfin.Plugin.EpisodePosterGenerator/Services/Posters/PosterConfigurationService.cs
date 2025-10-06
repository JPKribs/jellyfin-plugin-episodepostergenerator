using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Controller.Entities.TV;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Manages poster configuration lookup and validation
    /// </summary>
    public class PosterConfigurationService
    {
        private readonly ILogger<PosterConfigurationService> _logger;
        private Dictionary<Guid, PosterSettings> _seriesLookup = new Dictionary<Guid, PosterSettings>();
        private PosterSettings? _defaultSettings;

        public PosterConfigurationService(ILogger<PosterConfigurationService> logger)
        {
            _logger = logger;
        }

        // MARK: Initialize
        public void Initialize(PluginConfiguration config)
        {
            _seriesLookup.Clear();
            _defaultSettings = null;

            var defaults = config.PosterConfigurations.Where(c => c.IsDefault).ToList();
            
            if (defaults.Count == 0)
            {
                _logger.LogInformation("No default poster configuration found, creating one");
                var newDefault = new PosterConfiguration
                {
                    Name = "Default",
                    IsDefault = true
                };
                config.PosterConfigurations.Insert(0, newDefault);
                _defaultSettings = newDefault.Settings;
                
                Plugin.Instance?.SaveConfiguration();
            }
            else if (defaults.Count > 1)
            {
                _logger.LogWarning("Multiple default configurations found, using first one");
                _defaultSettings = defaults[0].Settings;
            }
            else
            {
                _defaultSettings = defaults[0].Settings;
            }

            var duplicates = new List<Guid>();
            
            foreach (var posterConfig in config.PosterConfigurations.Where(c => !c.IsDefault))
            {
                foreach (var seriesId in posterConfig.SeriesIds)
                {
                    if (_seriesLookup.ContainsKey(seriesId))
                    {
                        duplicates.Add(seriesId);
                        _logger.LogWarning("Series {SeriesId} assigned to multiple poster configurations, using first assignment", seriesId);
                    }
                    else
                    {
                        _seriesLookup[seriesId] = posterConfig.Settings;
                    }
                }
            }

            _logger.LogInformation("Poster configuration initialized: {Count} series-specific configs, {DuplicateCount} duplicates ignored", 
                _seriesLookup.Count, duplicates.Count);
        }

        // MARK: GetSettingsForEpisode
        public PosterSettings GetSettingsForEpisode(Episode episode)
        {
            if (episode?.Series?.Id == null)
            {
                _logger.LogDebug("Episode has no series, using default settings");
                
                if (_defaultSettings == null)
                {
                    _logger.LogError("Default settings are null, this should never happen. Creating fallback settings.");
                    return new PosterSettings();
                }
                
                return _defaultSettings;
            }

            return GetSettingsForSeries(episode.Series.Id);
        }

        // MARK: GetSettingsForSeries
        public PosterSettings GetSettingsForSeries(Guid seriesId)
        {
            if (_seriesLookup.TryGetValue(seriesId, out var settings))
            {
                _logger.LogDebug("Using custom settings for series {SeriesId}", seriesId);
                return settings;
            }

            if (_defaultSettings == null)
            {
                _logger.LogError("Default settings are null, this should never happen. Creating fallback settings.");
                return new PosterSettings();
            }

            _logger.LogDebug("Using default settings for series {SeriesId}", seriesId);
            return _defaultSettings;
        }
    }
}