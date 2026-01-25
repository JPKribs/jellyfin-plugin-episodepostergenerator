using MediaBrowser.Model.Entities;
using Jellyfin.Data.Enums;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Extensions
{
    public static class HardwareAccelerationExtensions
    {
        // ToFFmpegArg
        // Converts a HardwareAccelerationType to its corresponding FFmpeg command-line argument.
        public static string ToFFmpegArg(this HardwareAccelerationType hwAccel)
        {
            return hwAccel switch
            {
                HardwareAccelerationType.qsv => "-hwaccel qsv",
                HardwareAccelerationType.nvenc => "-hwaccel cuda",
                HardwareAccelerationType.amf => "-hwaccel vaapi",
                HardwareAccelerationType.vaapi => "-hwaccel vaapi",
                HardwareAccelerationType.videotoolbox => "-hwaccel videotoolbox",
                HardwareAccelerationType.v4l2m2m => "-hwaccel v4l2m2m",
                HardwareAccelerationType.rkmpp => "-hwaccel rkmpp",
                _ => string.Empty
            };
        }
    }
}
