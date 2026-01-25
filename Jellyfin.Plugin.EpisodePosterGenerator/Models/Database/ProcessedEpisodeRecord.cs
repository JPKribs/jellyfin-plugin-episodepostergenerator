using System;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    public class ProcessedEpisodeRecord
    {
        public Guid EpisodeId { get; set; }

        public DateTime LastProcessed { get; set; }

        public string VideoFilePath { get; set; } = string.Empty;

        public long VideoFileSize { get; set; }

        public DateTime VideoFileLastModified { get; set; }

        public string ConfigurationHash { get; set; } = string.Empty;

        // ShouldReprocess
        // Determines if the episode should be reprocessed based on file and configuration changes.
        public bool ShouldReprocess(string currentVideoPath, long currentFileSize, DateTime currentLastModified, string currentConfigHash)
        {
            return VideoFilePath != currentVideoPath ||
                   VideoFileSize != currentFileSize ||
                   VideoFileLastModified != currentLastModified ||
                   ConfigurationHash != currentConfigHash;
        }
    }
}
