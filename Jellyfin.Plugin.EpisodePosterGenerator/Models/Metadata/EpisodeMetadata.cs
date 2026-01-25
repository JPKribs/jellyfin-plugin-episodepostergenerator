using System;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    public class EpisodeMetadata
    {
        public Guid SeriesId { get; set; }

        public string? SeriesName { get; set; }

        public string? SeasonName { get; set; }

        public int? SeasonNumber { get; set; }

        public string? EpisodeName { get; set; }

        public int? EpisodeNumberStart { get; set; }

        public int? EpisodeNumberEnd { get; set; }

        public VideoMetadata VideoMetadata { get; set; }

        public EpisodeMetadata()
        {
            VideoMetadata = new VideoMetadata();
        }

        public EpisodeMetadata(VideoMetadata videoMetadata)
        {
            VideoMetadata = videoMetadata;
        }

        // CreateFromEpisode
        // Creates an EpisodeMetadata instance by extracting all metadata from an Episode.
        public static EpisodeMetadata CreateFromEpisode(Episode episode)
        {
            var videoMetadata = VideoMetadata.CreateFromEpisode(episode);

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

        // GetSeriesName
        // Retrieves the series name from the episode or its parent series.
        private static string? GetSeriesName(Episode episode)
        {
            var series = episode.Series;
            if (series != null)
                return series.Name;

            return episode.SeriesName;
        }

        // GetSeasonName
        // Retrieves the season name from the episode or generates one from the season number.
        private static string? GetSeasonName(Episode episode)
        {
            var season = episode.Season;
            if (season != null)
                return season.Name;

            if (!string.IsNullOrEmpty(episode.SeasonName))
                return episode.SeasonName;

            var seasonNumber = GetSeasonNumber(episode);
            if (seasonNumber.HasValue)
                return $"Season {seasonNumber.Value}";

            return null;
        }

        // GetSeasonNumber
        // Retrieves the season number from the episode or its parent season.
        private static int? GetSeasonNumber(Episode episode)
        {
            if (episode.ParentIndexNumber.HasValue)
                return episode.ParentIndexNumber.Value;

            var season = episode.Season;
            if (season?.IndexNumber.HasValue == true)
                return season.IndexNumber.Value;

            return episode.AiredSeasonNumber;
        }
    }
}
