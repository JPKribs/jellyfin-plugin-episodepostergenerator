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

        var hashConfig = new
        {
            settings.ExtractPoster,
            settings.EnableHWA,
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
            settings.PosterFileType,
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
