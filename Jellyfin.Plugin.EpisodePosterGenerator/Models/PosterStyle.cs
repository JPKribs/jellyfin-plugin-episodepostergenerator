namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    public enum PosterStyle
    {
        /// <summary>
        /// Episode Screenshot with the Episode Name on the bottom.
        /// </summary>
        Standard,

        /// <summary>
        /// Episode number overlayed with the Episode Screenshot underneath.
        /// </summary>
        Cutout,
        /// <summary>
        /// Large episode number displayed as transparent cutout revealing the screenshot beneath.
        /// </summary>
        Numeral
    }
}