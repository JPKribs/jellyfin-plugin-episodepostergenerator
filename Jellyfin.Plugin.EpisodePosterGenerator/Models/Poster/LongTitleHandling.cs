namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    /// <summary>
    /// How a poster handles an episode title that does not fit its text area.
    /// </summary>
    public enum LongTitleHandling
    {
        /// <summary>Trim the title with an ellipsis.</summary>
        Ellipsis,

        /// <summary>Replace the title with word initials, e.g. "Lord of the Ring" becomes "LR".</summary>
        Abbreviate,

        /// <summary>Do not draw the title at all.</summary>
        DropName
    }
}
