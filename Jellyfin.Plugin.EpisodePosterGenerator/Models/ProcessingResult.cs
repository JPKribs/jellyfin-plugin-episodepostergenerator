using System.Collections.ObjectModel;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    /// <summary>
    /// Result of processing a single episode poster.
    /// </summary>
    public class ProcessingResult
    {
        /// <summary>
        /// Episode that was processed
        /// </summary>
        public Episode Episode { get; set; } = null!;

        /// <summary>
        /// Whether processing was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if processing failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Path to generated poster file
        /// </summary>
        public string? PosterPath { get; set; }

        /// <summary>
        /// Image data for provider mode
        /// </summary>
        public ReadOnlyCollection<byte>? ImageData { get; private set; }

        // MARK: SetImageData
        public void SetImageData(byte[] data)
        {
            ImageData = new ReadOnlyCollection<byte>(data);
        }
    }
}