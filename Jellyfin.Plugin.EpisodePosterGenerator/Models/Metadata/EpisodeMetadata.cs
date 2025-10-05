using System;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    /// <summary>
    /// Complete metadata for an episode including both episode and video information
    /// </summary>
    public class EpisodeMetadata
    {
        /// <summary>
        /// Name of the series this episode belongs to
        /// </summary>
        public Guid SeriesId { get; set; }

        /// <summary>
        /// Name of the series this episode belongs to
        /// </summary>
        public string? SeriesName { get; set; }

        /// <summary>
        /// Name of the season this episode belongs to
        /// </summary>
        public string? SeasonName { get; set; }

        /// <summary>
        /// Season number for this episode
        /// </summary>
        public int? SeasonNumber { get; set; }

        /// <summary>
        /// Title/name of this specific episode
        /// </summary>
        public string? EpisodeName { get; set; }

        /// <summary>
        /// Starting episode number (for single episodes, same as end)
        /// </summary>
        public int? EpisodeNumberStart { get; set; }

        /// <summary>
        /// Ending episode number (for double episodes or multi-part episodes)
        /// </summary>
        public int? EpisodeNumberEnd { get; set; }

        /// <summary>
        /// Video-specific metadata for this episode
        /// </summary>
        public VideoMetadata VideoMetadata { get; set; }

        public EpisodeMetadata()
        {
            VideoMetadata = new VideoMetadata();
        }

        public EpisodeMetadata(VideoMetadata videoMetadata)
        {
            VideoMetadata = videoMetadata;
        }

        // MARK: CreateFromEpisode
        public static EpisodeMetadata CreateFromEpisode(Episode episode)
        {
            // First create the video metadata
            var videoMetadata = VideoMetadata.CreateFromEpisode(episode);

            // Create the episode metadata
            var episodeMetadata = new EpisodeMetadata(videoMetadata)
            {
                SeriesName = GetSeriesName(episode),
                SeasonName = GetSeasonName(episode),
                SeasonNumber = GetSeasonNumber(episode),
                EpisodeName = episode.Name,
                EpisodeNumberStart = episode.IndexNumber,
                EpisodeNumberEnd = episode.IndexNumberEnd ?? episode.IndexNumber
            };

            return episodeMetadata;
        }

        // MARK: GetSeriesName
        private static string? GetSeriesName(Episode episode)
        {
            var series = episode.Series;
            if (series != null)
                return series.Name;

            // Fallback to episode's series name property
            return episode.SeriesName;
        }

        // MARK: GetSeasonName
        private static string? GetSeasonName(Episode episode)
        {
            var season = episode.Season;
            if (season != null)
                return season.Name;

            // Fallback to episode's season name property
            if (!string.IsNullOrEmpty(episode.SeasonName))
                return episode.SeasonName;

            // Generate season name from number if available
            var seasonNumber = GetSeasonNumber(episode);
            if (seasonNumber.HasValue)
                return $"Season {seasonNumber.Value}";

            return null;
        }

        // MARK: GetSeasonNumber
        private static int? GetSeasonNumber(Episode episode)
        {
            // Try episode's parent index number first
            if (episode.ParentIndexNumber.HasValue)
                return episode.ParentIndexNumber.Value;

            // Try the season object's index number
            var season = episode.Season;
            if (season?.IndexNumber.HasValue == true)
                return season.IndexNumber.Value;

            // Try episode's aired season number
            return episode.AiredSeasonNumber;
        }
    }
}