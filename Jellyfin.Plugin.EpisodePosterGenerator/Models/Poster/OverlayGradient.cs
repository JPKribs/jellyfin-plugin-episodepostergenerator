namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    /// <summary>
    /// Defines the direction and type of gradient overlay to apply to poster images
    /// </summary>
    public enum OverlayGradient
    {
        /// <summary>
        /// No gradient overlay is applied
        /// </summary>
        None,

        /// <summary>
        /// Gradient flows horizontally from left edge to right edge
        /// </summary>
        LeftToRight,

        /// <summary>
        /// Gradient flows vertically from bottom edge to top edge
        /// </summary>
        BottomToTop,

        /// <summary>
        /// Diagonal gradient flows from top-left corner to bottom-right corner
        /// </summary>
        TopLeftCornerToBottomRightCorner,

        /// <summary>
        /// Diagonal gradient flows from top-right corner to bottom-left corner
        /// </summary>
        TopRightCornerToBottomLeftCorner
    }
}