namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    /// <summary>
    /// How a poster handles an episode title that does not fit its text area.
    /// </summary>
    public enum LongTitleHandling
    {
        /// <summary>Trim the title with an ellipsis.</summary>
        Ellipsis,

        /// <summary>
        /// Shorten the title in stages: the text before a divider, then the first
        /// sentence, then word initials with periods such as "L.R.".
        /// </summary>
        Abbreviate,

        /// <summary>Do not draw the title at all.</summary>
        DropName
    }
}
