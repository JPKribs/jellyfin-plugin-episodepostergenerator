using MediaBrowser.Controller.Entities.TV;
using Jellyfin.Plugin.EpisodePosterGenerator.Utils;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    /// <summary>
    /// Complete metadata package for episode poster generation containing all necessary information.
    /// </summary>
    public class EpisodePosterMetadata
    {
        /// <summary>
        /// Original episode object for reference
        /// </summary>
        public Episode Episode { get; set; }

        /// <summary>
        /// Complete media details including video metadata from utility
        /// </summary>
        public MediaDetails MediaDetails { get; set; }

        /// <summary>
        /// Season number for poster text
        /// </summary>
        public int SeasonNumber { get; set; }

        /// <summary>
        /// Episode number for poster text
        /// </summary>
        public int EpisodeNumber { get; set; }

        /// <summary>
        /// Episode title for poster text
        /// </summary>
        public string EpisodeTitle { get; set; }

        /// <summary>
        /// Series name for poster text
        /// </summary>
        public string SeriesName { get; set; }

        /// <summary>
        /// File path to series logo image if available
        /// </summary>
        public string? SeriesLogoPath { get; set; }

        public EpisodePosterMetadata(
            Episode episode,
            MediaDetails mediaDetails,
            int seasonNumber,
            int episodeNumber,
            string episodeTitle,
            string seriesName,
            string? seriesLogoPath = null)
        {
            Episode = episode;
            MediaDetails = mediaDetails;
            SeasonNumber = seasonNumber;
            EpisodeNumber = episodeNumber;
            EpisodeTitle = episodeTitle;
            SeriesName = seriesName;
            SeriesLogoPath = seriesLogoPath;
        }
    }
}