namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    // PosterGenerationResult
    // Holds the paths produced by a poster generation run: the fully rendered
    // primary poster and, optionally, a backdrop image derived from the canvas.
    public class PosterGenerationResult
    {
        // Path to the fully rendered poster (used as the episode Primary image).
        public string PosterPath { get; set; } = string.Empty;

        // Optional path to the cropped canvas image (used as the episode Backdrop image).
        public string? BackdropPath { get; set; }
    }
}
