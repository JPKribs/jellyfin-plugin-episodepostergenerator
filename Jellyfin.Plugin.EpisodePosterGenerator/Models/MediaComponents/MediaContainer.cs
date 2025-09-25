namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    /// <summary>
    /// Media container formats for episode processing
    /// </summary>
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

    /// <summary>
    /// Extension methods for MediaContainer enum
    /// </summary>
    public static class MediaContainerExtensions
    {
        // MARK: FromString
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

        // MARK: FromFileExtension
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