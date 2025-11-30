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
        Numeral,
        /// <summary>
        /// Series logo with episode code and optional title on solid background.
        /// </summary>
        Logo,
        /// <summary>
        /// Frame border around poster with episode title and optional season/episode information.
        /// </summary>
        Frame,
        /// <summary>
        /// Random paintbrush strokes cut out from overlay revealing screenshot beneath with episode code and title in bottom-left.
        /// </summary>
        Brush
    }
}