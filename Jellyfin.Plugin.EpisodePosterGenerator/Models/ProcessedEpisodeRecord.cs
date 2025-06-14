using System;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    /// <summary>
    /// Represents a record of an episode that has been processed by the poster generator.
    /// Used to track when episodes were last processed and determine if they need reprocessing
    /// based on changes to the video file, configuration, or processing timestamp.
    /// </summary>
    public class ProcessedEpisodeRecord
    {
        /// <summary>
        /// Gets or sets the unique identifier of the processed episode.
        /// </summary>
        public Guid EpisodeId { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp when the episode was last processed by the poster generator.
        /// Used to determine if manual image changes occurred after processing.
        /// </summary>
        public DateTime LastProcessed { get; set; }
        
        /// <summary>
        /// Gets or sets the file path of the video file that was processed.
        /// Used to detect if the episode's video file has been moved or renamed.
        /// </summary>
        public string VideoFilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the file size in bytes of the video file when it was processed.
        /// Used to detect if the video file has been replaced or modified.
        /// </summary>
        public long VideoFileSize { get; set; }
        
        /// <summary>
        /// Gets or sets the last modified timestamp of the video file when it was processed.
        /// Used to detect if the video file has been updated since processing.
        /// </summary>
        public DateTime VideoFileLastModified { get; set; }
        
        /// <summary>
        /// Gets or sets a hash of the plugin configuration when the episode was processed.
        /// Used to detect if poster generation settings have changed, requiring reprocessing
        /// to apply new styles, fonts, or other visual settings.
        /// </summary>
        public string ConfigurationHash { get; set; } = string.Empty;
        
        /// <summary>
        /// Determines whether an episode should be reprocessed based on changes to the video file or configuration.
        /// </summary>
        /// <param name="currentVideoPath">The current file path of the episode's video file.</param>
        /// <param name="currentFileSize">The current file size in bytes of the episode's video file.</param>
        /// <param name="currentLastModified">The current last modified timestamp of the episode's video file.</param>
        /// <param name="currentConfigHash">The current hash of the plugin configuration.</param>
        /// <returns>
        /// <c>true</c> if the episode should be reprocessed due to video file changes, configuration changes,
        /// or file relocation; otherwise, <c>false</c> if the episode has not changed since last processing.
        /// </returns>
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