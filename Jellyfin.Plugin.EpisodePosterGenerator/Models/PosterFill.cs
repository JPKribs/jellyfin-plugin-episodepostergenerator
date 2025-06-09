namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    public enum PosterFill
    {
        /// <summary>
        /// Preserve the original dimensions of the episode screenshot. May result in non-standard sized posters.
        /// </summary>
        Original,

        /// <summary>
        /// Expand the episode screenshot to fill the 16:9 dimension for the poster. May result in stretched images.
        /// </summary>
        Fill,

        /// <summary>
        /// Zoom into the episode screenshot to fit the 16:9 dimension for the poster. May result in cutoff images.
        /// </summary>
        Fit
    }
}