using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    /// <summary>
    /// Exportable poster template for sharing configurations
    /// </summary>
    public class PosterTemplate
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("author")]
        public string? Author { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("createdDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("settings")]
        public PosterSettings Settings { get; set; } = new PosterSettings();
    }
}