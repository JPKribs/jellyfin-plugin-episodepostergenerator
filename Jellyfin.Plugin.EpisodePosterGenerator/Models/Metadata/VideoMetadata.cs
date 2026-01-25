using System;
using System.IO;
using System.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    public class VideoMetadata
    {
        public string? SeriesLogoFilePath { get; set; }

        public string? EpisodeFilePath { get; set; }

        public int VideoWidth { get; set; }

        public int VideoHeight { get; set; }

        public MediaContainer VideoContainer { get; set; } = MediaContainer.Unknown;

        public VideoCodec VideoCodec { get; set; } = VideoCodec.Unknown;

        public long VideoLengthTicks { get; set; }

        public string VideoColorSpace { get; set; } = string.Empty;

        public int VideoColorBits { get; set; }

        public VideoRangeType VideoHdrType { get; set; } = VideoRangeType.Unknown;

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
                var logoPath = GetSeriesLogoPath(series);
                metadata.SeriesLogoFilePath = logoPath;
            }

            var mediaStreams = episode.GetMediaStreams();
            var videoStream = mediaStreams?.FirstOrDefault(s => s.Type == MediaStreamType.Video);

            if (videoStream != null)
            {
                metadata.VideoWidth = videoStream.Width ?? 1920;
                metadata.VideoHeight = videoStream.Height ?? 1080;
                metadata.VideoContainer = MediaContainerExtensions.FromFileExtension(episode.Path);
                metadata.VideoCodec = VideoCodecExtensions.FromString(videoStream.Codec);
                metadata.VideoColorSpace = videoStream.ColorSpace ?? "Unknown";
                metadata.VideoColorBits = ExtractColorBits(videoStream);
                metadata.VideoHdrType = ExtractVideoRangeType(videoStream);
            }
            else
            {
                metadata.VideoWidth = 1920;
                metadata.VideoHeight = 1080;
                metadata.VideoContainer = MediaContainerExtensions.FromFileExtension(episode.Path);
                metadata.VideoCodec = VideoCodec.Unknown;
                metadata.VideoColorSpace = "Unknown";
                metadata.VideoColorBits = 8;
                metadata.VideoHdrType = VideoRangeType.Unknown;
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

        // ExtractColorBits
        // Extracts the color bit depth from the video stream metadata.
        private static int ExtractColorBits(MediaStream videoStream)
        {
            var bitDepth = videoStream.BitDepth;
            if (bitDepth.HasValue)
                return bitDepth.Value;

            var profile = videoStream.Profile?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(profile))
            {
                if (profile.Contains("10", StringComparison.OrdinalIgnoreCase) || profile.Contains("main10", StringComparison.OrdinalIgnoreCase))
                    return 10;
                if (profile.Contains("12", StringComparison.OrdinalIgnoreCase) || profile.Contains("main12", StringComparison.OrdinalIgnoreCase))
                    return 12;
            }

            return 8;
        }

        // ExtractVideoRangeType
        // Determines the video range type (HDR/SDR) from the video stream metadata.
        private static VideoRangeType ExtractVideoRangeType(MediaStream videoStream)
        {
            if (videoStream.VideoRangeType != VideoRangeType.Unknown)
                return videoStream.VideoRangeType;

            return VideoRangeTypeExtensions.FromColorTransferAndPrimaries(
                videoStream.ColorTransfer,
                videoStream.ColorPrimaries);
        }
    }
}
