namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    public enum VideoCodec
    {
        Unknown,
        AV1,
        DV,
        Dirac,
        FFV1,
        FLV1,
        H261,
        H263,
        H264,
        HEVC,
        MJPEG,
        MPEG1Video,
        MPEG2Video,
        MPEG4,
        MSMPEG4v1,
        MSMPEG4v2,
        MSMPEG4v3,
        ProRes,
        Theora,
        VC1,
        VP8,
        VP9,
        WMV1,
        WMV2,
        WMV3
    }

    public static class VideoCodecExtensions
    {
        // FromString
        // Converts a codec string identifier to the corresponding VideoCodec enum value.
        public static VideoCodec FromString(string? codecString)
        {
            if (string.IsNullOrEmpty(codecString))
                return VideoCodec.Unknown;

            return codecString.ToLowerInvariant() switch
            {
                "av01" or "av1" => VideoCodec.AV1,
                "dv" or "dvhe" or "dvh1" => VideoCodec.DV,
                "dirac" => VideoCodec.Dirac,
                "ffv1" => VideoCodec.FFV1,
                "flv1" => VideoCodec.FLV1,
                "h261" => VideoCodec.H261,
                "h263" => VideoCodec.H263,
                "h264" or "avc" or "avc1" => VideoCodec.H264,
                "hevc" or "h265" or "hev1" or "hvc1" => VideoCodec.HEVC,
                "mjpeg" => VideoCodec.MJPEG,
                "mpeg1video" => VideoCodec.MPEG1Video,
                "mpeg2video" => VideoCodec.MPEG2Video,
                "mpeg4" => VideoCodec.MPEG4,
                "msmpeg4v1" => VideoCodec.MSMPEG4v1,
                "msmpeg4v2" => VideoCodec.MSMPEG4v2,
                "msmpeg4v3" => VideoCodec.MSMPEG4v3,
                "prores" => VideoCodec.ProRes,
                "theora" => VideoCodec.Theora,
                "vc1" or "vc-1" => VideoCodec.VC1,
                "vp8" => VideoCodec.VP8,
                "vp9" => VideoCodec.VP9,
                "wmv1" => VideoCodec.WMV1,
                "wmv2" => VideoCodec.WMV2,
                "wmv3" => VideoCodec.WMV3,
                _ => VideoCodec.Unknown
            };
        }
    }
}
