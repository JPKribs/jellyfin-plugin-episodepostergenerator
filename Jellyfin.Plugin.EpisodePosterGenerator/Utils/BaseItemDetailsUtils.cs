using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Utils
{
    /// <summary>
    /// Utility for extracting detailed video metadata from Jellyfin BaseItems.
    /// </summary>
    public static class BaseItemVideoDetails
    {
        /// <summary>
        /// Get detailed media information for a given BaseItem, focusing on video metadata.
        /// </summary>
        public static MediaDetails GetMediaDetails(BaseItem item)
        {
            var mediaSources = item.GetMediaSources(false);
            var mediaSource = mediaSources.Count > 0 ? mediaSources[0] : null;
            
            MediaStream? videoStream = null;
            if (mediaSource?.MediaStreams != null)
            {
                for (int i = 0; i < mediaSource.MediaStreams.Count; i++)
                {
                    if (mediaSource.MediaStreams[i].Type == MediaStreamType.Video)
                    {
                        videoStream = mediaSource.MediaStreams[i];
                        break;
                    }
                }
            }

            var details = new MediaDetails(
                itemId: item.Id,
                itemName: item.Name,
                filePath: item.Path,
                fileSize: GetFileSize(item),
                duration: GetDuration(item),
                container: item.Container,
                videoDetails: videoStream != null ? new VideoDetails(videoStream) : null
            );

            return details;
        }

        // MARK: GetDuration
        private static TimeSpan? GetDuration(BaseItem item)
        {
            return item.RunTimeTicks.HasValue
                ? TimeSpan.FromTicks(item.RunTimeTicks.Value)
                : null;
        }

        // MARK: GetFileSize
        private static long? GetFileSize(BaseItem item)
        {
            try
            {
                if (!string.IsNullOrEmpty(item.Path) && File.Exists(item.Path))
                {
                    return new FileInfo(item.Path).Length;
                }
            }
            catch
            {
                // Ignore file access errors in utility
            }

            return null;
        }
    }

    /// <summary>
    /// Simplified media details container focused on video metadata.
    /// </summary>
    public class MediaDetails
    {
        public Guid ItemId { get; }
        public string? ItemName { get; }
        public string? FilePath { get; }
        public long? FileSize { get; }
        public TimeSpan? Duration { get; }
        public string? Container { get; }
        public VideoDetails? VideoDetails { get; }

        public MediaDetails(
            Guid itemId = default,
            string? itemName = null,
            string? filePath = null,
            long? fileSize = null,
            TimeSpan? duration = null,
            string? container = null,
            VideoDetails? videoDetails = null)
        {
            ItemId = itemId;
            ItemName = itemName;
            FilePath = filePath;
            FileSize = fileSize;
            Duration = duration;
            Container = container;
            VideoDetails = videoDetails;
        }
    }

    /// <summary>
    /// Detailed video metadata for poster generation.
    /// </summary>
    public class VideoDetails
    {
        public string? Codec { get; }
        public string? Profile { get; }
        public double? Level { get; }
        public int? Width { get; }
        public int? Height { get; }
        public string? AspectRatio { get; }
        public float? FrameRate { get; }
        public int? BitDepth { get; }
        public string? ColorSpace { get; }
        public string? ColorTransfer { get; }
        public string? ColorPrimaries { get; }
        public string? ColorRange { get; }
        public VideoRange? VideoRange { get; }
        public VideoRangeType? VideoRangeType { get; }
        public bool IsAVC { get; }
        public bool? IsInterlaced { get; }
        public bool? IsAnamorphic { get; }
        public string? PixelFormat { get; }
        public int? DvProfile { get; }
        public int? DvLevel { get; }
        public int? DvBlSignalCompatibilityId { get; }
        public int? BlPresentFlag { get; }
        public int? ElPresentFlag { get; }
        public int? RpuPresentFlag { get; }
        public string? Path { get; }

        public VideoDetails(MediaStream stream)
        {
            Codec = stream.Codec;
            Profile = stream.Profile;
            Level = stream.Level;
            Width = stream.Width;
            Height = stream.Height;
            AspectRatio = stream.AspectRatio;
            FrameRate = stream.RealFrameRate ?? stream.AverageFrameRate;
            BitDepth = stream.BitDepth;
            ColorSpace = stream.ColorSpace;
            ColorTransfer = stream.ColorTransfer;
            ColorPrimaries = stream.ColorPrimaries;
            ColorRange = stream.ColorRange;
            VideoRange = stream.VideoRange;
            VideoRangeType = stream.VideoRangeType;
            IsAVC = stream.IsAVC ?? false;
            IsInterlaced = stream.IsInterlaced;
            IsAnamorphic = stream.IsAnamorphic;
            PixelFormat = stream.PixelFormat;
            DvProfile = stream.DvProfile;
            DvLevel = stream.DvLevel;
            DvBlSignalCompatibilityId = stream.DvBlSignalCompatibilityId;
            BlPresentFlag = stream.BlPresentFlag;
            ElPresentFlag = stream.ElPresentFlag;
            RpuPresentFlag = stream.RpuPresentFlag;
            Path = stream.Path;
        }
    }
}