using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Extensions
{
    public static class HardwareAccelerationTypeExtensions
    {
        /// <summary>
        /// Maps a HardwareAccelerationType to the corresponding FFmpeg -hwaccel argument.
        /// Returns an empty string if no hardware acceleration is used.
        /// </summary>
        public static string ToFFmpegArg(this HardwareAccelerationType hwType)
        {
            return hwType switch
            {
                HardwareAccelerationType.none => string.Empty,
                HardwareAccelerationType.amf => "-hwaccel amf",
                HardwareAccelerationType.qsv => "-hwaccel qsv",
                HardwareAccelerationType.nvenc => "-hwaccel cuda",
                HardwareAccelerationType.v4l2m2m => "-hwaccel v4l2m2m",
                HardwareAccelerationType.vaapi => "-hwaccel vaapi",
                HardwareAccelerationType.videotoolbox => "-hwaccel videotoolbox",
                HardwareAccelerationType.rkmpp => "-hwaccel rkmpp",
                _ => string.Empty
            };
        }
    }
}