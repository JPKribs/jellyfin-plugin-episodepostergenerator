using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models
{
    public class PosterSettings
    {
        /// <summary>
        /// Gets the effective episode font path (null when custom font is disabled).
        /// </summary>
        [JsonIgnore]
        public string? EffectiveEpisodeFontPath => EpisodeUseCustomFont ? EpisodeFontPath : null;

        /// <summary>
        /// Gets the effective title font path (null when custom font is disabled).
        /// </summary>
        [JsonIgnore]
        public string? EffectiveTitleFontPath => TitleUseCustomFont ? TitleFontPath : null;

        public bool ExtractPoster { get; set; } = true;

        public bool EnableLetterboxDetection { get; set; } = true;

        public int LetterboxBlackThreshold { get; set; } = 25;

        public float LetterboxConfidence { get; set; } = 85.0f;

        public float ExtractWindowStart { get; set; } = 20.0f;

        public float ExtractWindowEnd { get; set; } = 80.0f;

        public PosterStyle PosterStyle { get; set; } = PosterStyle.Standard;

        public CutoutType CutoutType { get; set; } = CutoutType.Code;

        public bool CutoutBorder { get; set; } = true;

        public Position LogoPosition { get; set; } = Position.Center;

        public Alignment LogoAlignment { get; set; } = Alignment.Center;

        public float LogoHeight { get; set; } = 30.0f;

        public float BrightenHDR { get; set; } = 25.0f;

        public PosterFill PosterFill { get; set; } = PosterFill.Original;

        public string PosterDimensionRatio { get; set; } = "16:9";

        public float PosterSafeArea { get; set; } = 5.0f;

        public bool ShowEpisode { get; set; } = true;

        public string EpisodeFontFamily { get; set; } = "Arial";

        public bool EpisodeUseCustomFont { get; set; } = false;

        public string EpisodeFontPath { get; set; } = string.Empty;

        public string EpisodeFontStyle { get; set; } = "Bold";

        public float EpisodeFontSize { get; set; } = 7.0F;

        public string EpisodeFontColor { get; set; } = "#FFFFFFFF";

        public bool ShowTitle { get; set; } = true;

        public string TitleFontFamily { get; set; } = "Arial";

        public bool TitleUseCustomFont { get; set; } = false;

        public string TitleFontPath { get; set; } = string.Empty;

        public string TitleFontStyle { get; set; } = "Bold";

        public float TitleFontSize { get; set; } = 10.0F;

        public string TitleFontColor { get; set; } = "#FFFFFFFF";

        public string OverlayColor { get; set; } = "#66000000";

        public OverlayGradient OverlayGradient { get; set; } = OverlayGradient.None;

        public string OverlaySecondaryColor { get; set; } = "#66000000";

        public string GraphicPath { get; set; } = string.Empty;

        public float GraphicWidth { get; set; } = 25.0f;

        public float GraphicHeight { get; set; } = 25.0f;

        public Position GraphicPosition { get; set; } = Position.Center;

        public Alignment GraphicAlignment { get; set; } = Alignment.Center;
    }
}
