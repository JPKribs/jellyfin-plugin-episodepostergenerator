using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services;

public class ConfigurationHashService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // ComputeHash
    // Computes a SHA256 hash of the poster settings for change detection.
    public string ComputeHash(PosterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Canvas state is encoded into the legacy "extractPoster" slot so that upgrading from
        // pre-10.11.23 does not needlessly reprocess every episode. The two states that existed
        // before (Extract / None with no backdrop) reproduce the old boolean hash exactly, so
        // unchanged configs keep their stored hash. SeriesBackdrop and an enabled backdrop are
        // genuinely new output states and intentionally change the hash to trigger reprocessing.
        object canvasState = (settings.CanvasSource, settings.GenerateBackdrop) switch
        {
            (CanvasSource.Extract, false) => true,
            (CanvasSource.None, false) => false,
            _ => $"{settings.CanvasSource}:{settings.GenerateBackdrop}"
        };

        var hashConfig = new
        {
            ExtractPoster = canvasState,
            settings.EnableLetterboxDetection,
            settings.LetterboxBlackThreshold,
            settings.LetterboxConfidence,
            settings.ExtractWindowStart,
            settings.ExtractWindowEnd,
            settings.PosterStyle,
            settings.CutoutType,
            settings.CutoutBorder,
            settings.LogoPosition,
            settings.LogoAlignment,
            settings.LogoHeight,
            settings.BrightenHDR,
            settings.PosterFill,
            settings.PosterDimensionRatio,
            settings.PosterSafeArea,
            settings.ShowEpisode,
            settings.EpisodeFontFamily,
            settings.EpisodeFontStyle,
            settings.EpisodeFontSize,
            settings.EpisodeFontColor,
            settings.ShowTitle,
            settings.TitleFontFamily,
            settings.TitleFontStyle,
            settings.TitleFontSize,
            settings.TitleFontColor,
            settings.OverlayColor,
            settings.OverlayGradient,
            settings.OverlaySecondaryColor,
            settings.GraphicPath,
            settings.GraphicWidth,
            settings.GraphicHeight,
            settings.GraphicPosition,
            settings.GraphicAlignment
        };

        var json = JsonSerializer.Serialize(hashConfig, JsonOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes);
    }

    // HasConfigurationChanged
    // Determines if the configuration has changed by comparing hashes.
    public bool HasConfigurationChanged(PosterSettings settings, string previousHash)
    {
        if (string.IsNullOrEmpty(previousHash))
            return true;

        var currentHash = ComputeHash(settings);
        return !string.Equals(currentHash, previousHash, StringComparison.OrdinalIgnoreCase);
    }

    // GetCurrentHash
    // Returns the current hash for the provided poster settings.
    public string GetCurrentHash(PosterSettings settings)
    {
        return ComputeHash(settings);
    }
}
