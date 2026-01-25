using System.Collections.ObjectModel;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    public class ProcessingResult
    {
        public Episode Episode { get; set; } = null!;

        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }

        public string? PosterPath { get; set; }

        public ReadOnlyCollection<byte>? ImageData { get; private set; }

        // SetImageData
        // Sets the image data from a byte array as a read-only collection.
        public void SetImageData(byte[] data)
        {
            ImageData = new ReadOnlyCollection<byte>(data);
        }
    }
}
