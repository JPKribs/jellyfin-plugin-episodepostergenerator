using System.Linq;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    public class VideoMetadata
    {
        public string? SeriesLogoFilePath { get; set; }

        public string? SeriesPosterFilePath { get; set; }

        public string? EpisodeFilePath { get; set; }

        public int VideoWidth { get; set; }

        public int VideoHeight { get; set; }

        public long VideoLengthTicks { get; set; }

        // CreateFromEpisode
        // Creates a VideoMetadata instance by extracting video information from an Episode.
        public static VideoMetadata CreateFromEpisode(Episode episode)
        {
            var metadata = new VideoMetadata
            {
                EpisodeFilePath = episode.Path
            };

            var series = episode.Series;
            if (series != null)
            {
                metadata.SeriesLogoFilePath = GetSeriesLogoPath(series);
                metadata.SeriesPosterFilePath = GetSeriesPosterPath(series);
            }

            var mediaStreams = episode.GetMediaStreams();
            var videoStream = mediaStreams?.FirstOrDefault(s => s.Type == MediaStreamType.Video);

            if (videoStream != null)
            {
                metadata.VideoWidth = videoStream.Width ?? 1920;
                metadata.VideoHeight = videoStream.Height ?? 1080;
            }
            else
            {
                metadata.VideoWidth = 1920;
                metadata.VideoHeight = 1080;
            }

            metadata.VideoLengthTicks = episode.RunTimeTicks ?? 0;

            return metadata;
        }

        // GetSeriesLogoPath
        // Retrieves the file path of the series logo image if available.
        private static string? GetSeriesLogoPath(Series series)
        {
            var logoImage = series.GetImages(ImageType.Logo).FirstOrDefault();
            return logoImage?.Path;
        }

        // GetSeriesPosterPath
        // Retrieves the file path of the series primary poster image if available.
        private static string? GetSeriesPosterPath(Series series)
        {
            var posterImage = series.GetImages(ImageType.Primary).FirstOrDefault();
            return posterImage?.Path;
        }
    }
}
