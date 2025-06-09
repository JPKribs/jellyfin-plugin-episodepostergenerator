using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Configuration
{
    /// <summary>
    /// Configuration for Episode Poster Generator plugin.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        private double _blackDetectionThreshold = 0.1;
        private double _blackDurationThreshold = 0.1;
        private int _episodeFontSize = 32;
        private int _titleFontSize = 24;

        /// <summary>
        /// Gets or sets a value indicating whether the plugin is enabled.
        /// </summary>
        public bool EnablePlugin { get; set; } = true;

        /// <summary>
        /// Gets or sets the black frame detection threshold.
        /// </summary>
        public double BlackDetectionThreshold
        {
            get => _blackDetectionThreshold;
            set => _blackDetectionThreshold = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Gets or sets the black duration threshold.
        /// </summary>
        public double BlackDurationThreshold
        {
            get => _blackDurationThreshold;
            set => _blackDurationThreshold = value < 0 ? 0 : value;
        }

        /// <summary>
        /// Gets or sets the font size for the episode number text.
        /// </summary>
        public int EpisodeFontSize
        {
            get => _episodeFontSize;
            set => _episodeFontSize = value < 1 ? 1 : value;
        }

        /// <summary>
        /// Gets or sets the font size for the episode title text.
        /// </summary>
        public int TitleFontSize
        {
            get => _titleFontSize;
            set => _titleFontSize = value < 1 ? 1 : value;
        }

        /// <summary>
        /// Gets or sets the color of the text to render on the poster.
        /// </summary>
        public string TextColor { get; set; } = "White";

        /// <summary>
        /// Gets or sets the position of the text on the poster.
        /// </summary>
        public string TextPosition { get; set; } = "Bottom";

        /// <summary>
        /// Gets or sets the file path to the overlay image.
        /// </summary>
        public string? OverlayImagePath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use the overlay image.
        /// </summary>
        public bool UseOverlay { get; set; } = false;
    }
}