namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    public enum MediaContainer
    {
        Unknown,
        AVI,
        FLV,
        M4V,
        MKV,
        MOV,
        MP4,
        MPEGTS,
        TS,
        ThreeG2,
        ThreeGP,
        WEBM
    }

    public static class MediaContainerExtensions
    {
        // FromString
        // Converts a container format string to the corresponding MediaContainer enum value.
        public static MediaContainer FromString(string? containerString)
        {
            if (string.IsNullOrEmpty(containerString))
                return MediaContainer.Unknown;

            return containerString.ToLowerInvariant() switch
            {
                "avi" => MediaContainer.AVI,
                "flv" => MediaContainer.FLV,
                "m4v" => MediaContainer.M4V,
                "mkv" => MediaContainer.MKV,
                "mov" => MediaContainer.MOV,
                "mp4" => MediaContainer.MP4,
                "mpegts" or "mpeg-ts" => MediaContainer.MPEGTS,
                "ts" => MediaContainer.TS,
                "3g2" => MediaContainer.ThreeG2,
                "3gp" => MediaContainer.ThreeGP,
                "webm" => MediaContainer.WEBM,
                _ => MediaContainer.Unknown
            };
        }

        // FromFileExtension
        // Determines the media container type from a file path's extension.
        public static MediaContainer FromFileExtension(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return MediaContainer.Unknown;

            var extension = System.IO.Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();

            return extension switch
            {
                "avi" => MediaContainer.AVI,
                "flv" => MediaContainer.FLV,
                "m4v" => MediaContainer.M4V,
                "mkv" => MediaContainer.MKV,
                "mov" => MediaContainer.MOV,
                "mp4" => MediaContainer.MP4,
                "ts" => MediaContainer.TS,
                "mpg" or "mpeg" => MediaContainer.MPEGTS,
                "3g2" => MediaContainer.ThreeG2,
                "3gp" => MediaContainer.ThreeGP,
                "webm" => MediaContainer.WEBM,
                _ => MediaContainer.Unknown
            };
        }
    }
}
