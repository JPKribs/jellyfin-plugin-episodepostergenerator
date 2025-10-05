using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    /// <summary>
    /// Poster configuration with series assignments
    /// </summary>
    public class PosterConfiguration
    {
        /// <summary>
        /// Unique identifier for this configuration
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// User-friendly name for this configuration
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// All poster rendering settings
        /// </summary>
        public PosterSettings Settings { get; set; }

        /// <summary>
        /// Series IDs that should use this configuration. Empty collection = default for all unassigned series
        /// </summary>
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "List<T> required for XML serialization")]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for XML serialization")]
        public List<Guid> SeriesIds { get; set; }

        /// <summary>
        /// Whether this is the default configuration (empty SeriesIds)
        /// </summary>
        public bool IsDefault => SeriesIds == null || SeriesIds.Count == 0;

        public PosterConfiguration()
        {
            Id = Guid.NewGuid();
            Name = "Poster";
            Settings = new PosterSettings();
            SeriesIds = new List<Guid>();
        }
    }
}