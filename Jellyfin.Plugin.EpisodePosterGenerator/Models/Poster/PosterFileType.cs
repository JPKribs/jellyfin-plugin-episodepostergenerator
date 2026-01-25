namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    public enum PosterFileType
    {
        JPEG,
        PNG,
        WEBP
    }

    public static class PosterFileTypeExtensions
    {
        // GetFileExtension
        // Returns the file extension for the specified poster file type.
        public static string GetFileExtension(this PosterFileType fileType)
        {
            return fileType switch
            {
                PosterFileType.JPEG => ".jpg",
                PosterFileType.PNG => ".png",
                PosterFileType.WEBP => ".webp",
                _ => ".jpg"
            };
        }

        // GetMimeType
        // Returns the MIME type for the specified poster file type.
        public static string GetMimeType(this PosterFileType fileType)
        {
            return fileType switch
            {
                PosterFileType.JPEG => "image/jpeg",
                PosterFileType.PNG => "image/png",
                PosterFileType.WEBP => "image/webp",
                _ => "image/jpeg"
            };
        }

        // FromFileExtension
        // Converts a file extension to the corresponding PosterFileType.
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
                _ => PosterFileType.JPEG
            };
        }

        // FromMimeType
        // Converts a MIME type to the corresponding PosterFileType.
        public static PosterFileType FromMimeType(string? mimeType)
        {
            if (string.IsNullOrEmpty(mimeType))
                return PosterFileType.JPEG;

            return mimeType.ToLowerInvariant() switch
            {
                "image/jpeg" => PosterFileType.JPEG,
                "image/png" => PosterFileType.PNG,
                "image/webp" => PosterFileType.WEBP,
                _ => PosterFileType.JPEG
            };
        }

        // IsLossless
        // Determines if the specified file type uses lossless compression.
        public static bool IsLossless(this PosterFileType fileType)
        {
            return fileType switch
            {
                PosterFileType.PNG => true,
                _ => false
            };
        }

        // SupportsTransparency
        // Determines if the specified file type supports transparency.
        public static bool SupportsTransparency(this PosterFileType fileType)
        {
            return fileType switch
            {
                PosterFileType.PNG => true,
                PosterFileType.WEBP => true,
                _ => false
            };
        }
    }
}
