namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    // CanvasSource
    // Determines what image is used as the poster canvas background.
    public enum CanvasSource
    {
        // No background - a transparent canvas is used (overlay/text only).
        None,

        // Extract a representative frame from the episode video.
        Extract,

        // Use the parent series' backdrop image as the canvas.
        SeriesBackdrop
    }
}
