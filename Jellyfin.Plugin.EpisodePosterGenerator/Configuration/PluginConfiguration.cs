using MediaBrowser.Model.Plugins;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether the plugin is enabled as a Provider.
        /// </summary>
        public bool EnableProvider { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the plugin is enabled as a Provider.
        /// </summary>
        public bool EnableTask { get; set; } = true;

        /// <summary>
        /// Gets or sets the style of the generated poster.
        /// </summary>
        public PosterStyle PosterStyle { get; set; } = PosterStyle.Standard;

        /// <summary>
        /// Gets or sets the poster cutout type when the poster style is cutout.
        /// </summary>
        public CutoutType CutoutType { get; set; } = CutoutType.Code;

        /// <summary>
        /// Gets or sets a the cutout type border.
        /// </summary>
        public bool CutoutBorder { get; set; } = true;

        /// <summary>
        /// Gets or sets the poster logo position when the poster style is logo.
        /// </summary>
        public Position LogoPosition { get; set; } = Position.Center;

        /// <summary>
        /// Gets or sets the poster logo position when the poster style is logo.
        /// </summary>
        public Alignment LogoAlignment { get; set; } = Alignment.Center;

        /// <summary>
        /// Gets or sets the percentage of height used for the Logo (e.g., 7.0 for 7%).
        /// </summary>
        public float LogoHeight { get; set; } = 30.0f;

        /// <summary>
        /// Extracts the poster from the Episode.
        /// </summary>
        public bool ExtractPoster { get; set; } = true;

        /// <summary>
        /// Gets or sets how the screenshot is fit into the poster.
        /// </summary>
        public PosterFill PosterFill { get; set; } = PosterFill.Original;

        /// <summary>
        /// Gets or sets the poster aspect ratio (e.g., "16:9", "3:2", "4:3").
        /// </summary>
        public string PosterDimensionRatio { get; set; } = "16:9";

        /// <summary>
        /// Gets or sets the percentage of height and width perserved as a safe area (e.g., 7.0 for 7%).
        /// </summary>
        public float PosterSafeArea { get; set; } = 5.0f;

        /// <summary>
        /// Gets or sets a value indicating whether to show the episode title.
        /// </summary>
        public bool ShowEpisode { get; set; } = true;

        /// <summary>
        /// Gets or sets the font family for episode numbers/season info.
        /// </summary>
        public string EpisodeFontFamily { get; set; } = "Arial";

        /// <summary>
        /// Gets or sets the font style for episode numbers/season info (Normal, Bold, Italic, BoldItalic).
        /// </summary>
        public string EpisodeFontStyle { get; set; } = "Bold";

        /// <summary>
        /// Gets or sets the episode numbers/season info font size as a percentage of the poster height (e.g., 7.0 for 7%).
        /// </summary>
        public float EpisodeFontSize { get; set; } = 7.0F;

        /// <summary>
        /// Gets or sets the hex color code for episode number text.
        /// </summary>
        public string EpisodeFontColor { get; set; } = "#FFFFFF";

        /// <summary>
        /// Gets or sets a value indicating whether to show the episode title.
        /// </summary>
        public bool ShowTitle { get; set; } = true;

        /// <summary>
        /// Gets or sets the font family for episode titles.
        /// </summary>
        public string TitleFontFamily { get; set; } = "Arial";
    
        /// <summary>
        /// Gets or sets the font style for episode titles (Normal, Bold, Italic, BoldItalic).
        /// </summary>
        public string TitleFontStyle { get; set; } = "Bold";

        /// <summary>
        /// Gets or sets the episode titles font size as a percentage of the poster height (e.g., 7.0 for 7%).
        /// </summary>
        public float TitleFontSize { get; set; } = 10.0F;

        /// <summary>
        /// Gets or sets the hex color code for episode title text.
        /// </summary>
        public string TitleFontColor { get; set; } = "#FFFFFF";

        /// <summary>
        /// Gets or sets the ARGB hex color for background overlay / tinting.
        /// </summary>
        public string OverlayColor { get; set; } = "#66000000";
    }
}