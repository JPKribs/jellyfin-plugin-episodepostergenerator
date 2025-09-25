using System;
using System.IO;
using System.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    /// <summary>
    /// Video-specific metadata extracted from episode files for poster generation
    /// </summary>
    public class VideoMetadata
    {
        /// <summary>
        /// Path to series logo file for poster overlay
        /// </summary>
        public string? SeriesLogoFilePath { get; set; }

        /// <summary>
        /// Path to the episode video file
        /// </summary>
        public string? EpisodeFilePath { get; set; }

        /// <summary>
        /// Video width in pixels
        /// </summary>
        public int VideoWidth { get; set; }

        /// <summary>
        /// Video height in pixels
        /// </summary>
        public int VideoHeight { get; set; }

        /// <summary>
        /// Video container format (MP4, MKV, etc)
        /// </summary>
        public MediaContainer VideoContainer { get; set; } = MediaContainer.Unknown;

        /// <summary>
        /// Video compression codec (AV1, HEVC, H264, etc)
        /// </summary>
        public VideoCodec VideoCodec { get; set; } = VideoCodec.Unknown;

        /// <summary>
        /// Video duration in ticks
        /// </summary>
        public long VideoLengthTicks { get; set; }

        /// <summary>
        /// Video color space information
        /// </summary>
        public string VideoColorSpace { get; set; } = string.Empty;

        /// <summary>
        /// Video color bit depth (8, 10, 12)
        /// </summary>
        public int VideoColorBits { get; set; }

        /// <summary>
        /// HDR type if video contains HDR content
        /// </summary>
        public VideoRangeType VideoHdrType { get; set; } = VideoRangeType.Unknown;

        // MARK: CreateFromEpisode
        public static VideoMetadata CreateFromEpisode(Episode episode)
        {
            var metadata = new VideoMetadata
            {
                EpisodeFilePath = episode.Path
            };

            // Get series logo path if available
            var series = episode.Series;
            if (series != null)
            {
                var logoPath = GetSeriesLogoPath(series);
                metadata.SeriesLogoFilePath = logoPath;
            }

            // Extract video stream information
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
                // Fallback values if no video stream info available
                metadata.VideoWidth = 1920;
                metadata.VideoHeight = 1080;
                metadata.VideoContainer = MediaContainerExtensions.FromFileExtension(episode.Path);
                metadata.VideoCodec = VideoCodec.Unknown;
                metadata.VideoColorSpace = "Unknown";
                metadata.VideoColorBits = 8;
                metadata.VideoHdrType = VideoRangeType.Unknown;
            }

            // Get video duration
            metadata.VideoLengthTicks = episode.RunTimeTicks ?? 0;

            return metadata;
        }

        // MARK: GetSeriesLogoPath
        private static string? GetSeriesLogoPath(Series series)
        {
            var logoImage = series.GetImages(ImageType.Logo).FirstOrDefault();
            return logoImage?.Path;
        }

        // MARK: ExtractColorBits
        private static int ExtractColorBits(MediaStream videoStream)
        {
            var bitDepth = videoStream.BitDepth;
            if (bitDepth.HasValue)
                return bitDepth.Value;

            // Try to extract from codec profile
            var profile = videoStream.Profile?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(profile))
            {
                if (profile.Contains("10", StringComparison.OrdinalIgnoreCase) || profile.Contains("main10", StringComparison.OrdinalIgnoreCase))
                    return 10;
                if (profile.Contains("12", StringComparison.OrdinalIgnoreCase) || profile.Contains("main12", StringComparison.OrdinalIgnoreCase))
                    return 12;
            }

            return 8; // Default fallback
        }

        // MARK: ExtractVideoRangeType
        private static VideoRangeType ExtractVideoRangeType(MediaStream videoStream)
        {
            // First check if Jellyfin already provided the VideoRangeType
            if (videoStream.VideoRangeType != VideoRangeType.Unknown)
                return videoStream.VideoRangeType;

            // Fallback to analyzing color transfer and primaries
            return VideoRangeTypeExtensions.FromColorTransferAndPrimaries(
                videoStream.ColorTransfer, 
                videoStream.ColorPrimaries);
        }
    }
}