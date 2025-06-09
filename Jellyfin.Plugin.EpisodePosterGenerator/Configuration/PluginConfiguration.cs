using MediaBrowser.Model.Plugins;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Configuration
{
    /// <summary>
    /// Configuration for Episode Poster Generator plugin.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether the plugin is enabled.
        /// </summary>
        public bool EnablePlugin { get; set; } = true;

        /// <summary>
        /// Gets or sets how the screenshot is fit into the poster.
        /// </summary>
        public PosterFill PosterFill { get; set; } = PosterFill.Original;

        /// <summary>
        /// Gets or sets the style of the generated poster.
        /// </summary>
        public PosterStyle PosterStyle { get; set; } = PosterStyle.Standard;

        /// <summary>
        /// Gets or sets the hex color of the text.
        /// </summary>
        public string TextColor { get; set; } = "#FFFFFF";

        /// <summary>
        /// Gets or sets the font size of the episode text.
        /// </summary>
        public int EpisodeFontSize { get; set; } = 20;

        /// <summary>
        /// Gets or sets the font size of the title text.
        /// </summary>
        public int TitleFontSize { get; set; } = 28;

        /// <summary>
        /// Gets or sets the text position ("Top", "Bottom", etc.).
        /// </summary>
        public string TextPosition { get; set; } = "Bottom";

        /// <summary>
        /// Gets or sets the hex color to use as a background overlay (e.g. "#66000000").
        /// </summary>
        public string OverlayColor { get; set; } = "#66000000";
    }
}