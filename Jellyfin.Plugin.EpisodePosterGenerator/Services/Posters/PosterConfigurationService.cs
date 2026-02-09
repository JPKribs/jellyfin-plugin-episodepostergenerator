using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Controller.Entities.TV;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    public class PosterConfigurationService
    {
        private readonly ILogger<PosterConfigurationService> _logger;
        private volatile Dictionary<Guid, PosterSettings> _seriesLookup = new Dictionary<Guid, PosterSettings>();
        private volatile PosterSettings? _defaultSettings;

        // PosterConfigurationService
        // Initializes the poster configuration service with logging support.
        public PosterConfigurationService(ILogger<PosterConfigurationService> logger)
        {
            _logger = logger;
        }

        // Initialize
        // Builds the series-to-settings lookup from the plugin configuration.
        // Uses atomic swap to avoid race conditions with concurrent readers.
        public void Initialize(PluginConfiguration config)
        {
            PosterSettings? newDefaultSettings = null;

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
                newDefaultSettings = newDefault.Settings;

                Plugin.Instance?.SaveConfiguration();
            }
            else if (defaults.Count > 1)
            {
                _logger.LogWarning("Multiple default configurations found, using first one");
                newDefaultSettings = defaults[0].Settings;
            }
            else
            {
                newDefaultSettings = defaults[0].Settings;
            }

            var newLookup = new Dictionary<Guid, PosterSettings>();
            var duplicates = new List<Guid>();

            foreach (var posterConfig in config.PosterConfigurations.Where(c => !c.IsDefault))
            {
                foreach (var seriesId in posterConfig.SeriesIds)
                {
                    if (newLookup.ContainsKey(seriesId))
                    {
                        duplicates.Add(seriesId);
                        _logger.LogWarning("Series {SeriesId} assigned to multiple poster configurations, using first assignment", seriesId);
                    }
                    else
                    {
                        newLookup[seriesId] = posterConfig.Settings;
                    }
                }
            }

            // Atomic swap — readers see either the old or the new state, never a partially built dictionary
            _defaultSettings = newDefaultSettings;
            _seriesLookup = newLookup;

            _logger.LogInformation("Poster configuration initialized: {Count} series-specific configs, {DuplicateCount} duplicates ignored",
                _seriesLookup.Count, duplicates.Count);
        }

        // GetSettingsForEpisode
        // Returns the poster settings for the episode's series or the default settings.
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

        // GetSettingsForSeries
        // Returns the poster settings for the specified series ID or the default settings.
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
