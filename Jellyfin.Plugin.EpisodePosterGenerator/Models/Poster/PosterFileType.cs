namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    /// <summary>
    /// Supported file formats for generated posters
    /// </summary>
    public enum PosterFileType
    {
        JPEG,
        PNG,
        WEBP,
        GIF
    }

    /// <summary>
    /// Extension methods for PosterFileType enum
    /// </summary>
    public static class PosterFileTypeExtensions
    {
        // MARK: GetFileExtension
        public static string GetFileExtension(this PosterFileType fileType)
        {
            return fileType switch
            {
                PosterFileType.JPEG => ".jpg",
                PosterFileType.PNG => ".png",
                PosterFileType.WEBP => ".webp",
                PosterFileType.GIF => ".gif",
                _ => ".jpg"
            };
        }

        // MARK: GetMimeType
        public static string GetMimeType(this PosterFileType fileType)
        {
            return fileType switch
            {
                PosterFileType.JPEG => "image/jpeg",
                PosterFileType.PNG => "image/png",
                PosterFileType.WEBP => "image/webp",
                PosterFileType.GIF => "image/gif",
                _ => "image/jpeg"
            };
        }

        // MARK: FromFileExtension
        public static PosterFileType FromFileExtension(string? extension)
        {
            if (string.IsNullOrEmpty(extension))
                return PosterFileType.JPEG;

            var ext = extension.TrimStart('.').ToLowerInvariant();
            
            return ext switch
            {
                "jpg" or "jpeg" => PosterFileType.JPEG,
                "png" => PosterFileType.PNG,
                "webp" => PosterFileType.WEBP,
                "gif" => PosterFileType.GIF,
                _ => PosterFileType.JPEG
            };
        }

        // MARK: FromMimeType
        public static PosterFileType FromMimeType(string? mimeType)
        {
            if (string.IsNullOrEmpty(mimeType))
                return PosterFileType.JPEG;

            return mimeType.ToLowerInvariant() switch
            {
                "image/jpeg" => PosterFileType.JPEG,
                "image/png" => PosterFileType.PNG,
                "image/webp" => PosterFileType.WEBP,
                "image/gif" => PosterFileType.GIF,
                _ => PosterFileType.JPEG
            };
        }

        // MARK: IsLossless
        public static bool IsLossless(this PosterFileType fileType)
        {
            return fileType switch
            {
                PosterFileType.PNG => true,
                _ => false
            };
        }

        // MARK: SupportsTransparency
        public static bool SupportsTransparency(this PosterFileType fileType)
        {
            return fileType switch
            {
                PosterFileType.PNG => true,
                PosterFileType.WEBP => true,
                PosterFileType.GIF => true,
                _ => false
            };
        }
    }
}