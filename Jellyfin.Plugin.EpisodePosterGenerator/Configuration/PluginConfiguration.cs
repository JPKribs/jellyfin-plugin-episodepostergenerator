using MediaBrowser.Model.Plugins;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public bool EnableProvider { get; set; } = true;

        public bool EnableTask { get; set; } = true;

        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "List<T> required for XML serialization")]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter required for XML serialization")]
        public List<PosterConfiguration> PosterConfigurations { get; set; }

        public PluginConfiguration()
        {
            PosterConfigurations = new List<PosterConfiguration>();
        }
    }
}