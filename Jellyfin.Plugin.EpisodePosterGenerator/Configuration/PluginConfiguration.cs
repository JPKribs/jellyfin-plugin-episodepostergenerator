using MediaBrowser.Model.Plugins;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Master switch for poster generation. When enabled, episodes missing a primary image
        /// get one during a metadata refresh, and generated posters are offered in the item's
        /// Edit Images dialog. Turning it off disables both providers.
        /// </summary>
        public bool EnableProvider { get; set; } = true;

        /// <summary>
        /// Number of alternate posters offered when replacing an episode's poster from the Edit
        /// Images dialog (1-10). Each one costs a frame extraction, so higher values make the
        /// dialog slower to open. Episodes with no poster yet are only ever offered one, since
        /// that request comes from an automatic refresh that keeps a single image.
        /// </summary>
        public int ImageChoiceCount { get; set; } = 3;

        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "List<T> required for XML serialization")]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter required for XML serialization")]
        public List<PosterConfiguration> PosterConfigurations { get; set; }

        // PluginConfiguration
        // Initializes the plugin configuration with default values.
        public PluginConfiguration()
        {
            PosterConfigurations = new List<PosterConfiguration>();
        }
    }
}
