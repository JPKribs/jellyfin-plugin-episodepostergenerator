using System;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    public class ProcessedEpisodeRecord
    {
        /// <summary>
        /// Gets or sets the unique identifier of the processed episode.
        /// </summary>
        public Guid EpisodeId { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp when the episode was last processed by the poster generator.
        /// </summary>
        public DateTime LastProcessed { get; set; }
        
        /// <summary>
        /// Gets or sets the file path of the video file that was processed.
        /// </summary>
        public string VideoFilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the file size in bytes of the video file when it was processed.
        /// </summary>
        public long VideoFileSize { get; set; }
        
        /// <summary>
        /// Gets or sets the last modified timestamp of the video file when it was processed.
        /// </summary>
        public DateTime VideoFileLastModified { get; set; }
        
        /// <summary>
        /// Gets or sets a hash of the plugin configuration when the episode was processed.
        /// </summary>
        public string ConfigurationHash { get; set; } = string.Empty;
        
        // MARK: ShouldReprocess
        public bool ShouldReprocess(string currentVideoPath, long currentFileSize, DateTime currentLastModified, string currentConfigHash)
        {
            return VideoFilePath != currentVideoPath ||
                   VideoFileSize != currentFileSize ||
                   VideoFileLastModified != currentLastModified ||
                   ConfigurationHash != currentConfigHash;
        }
    }
}