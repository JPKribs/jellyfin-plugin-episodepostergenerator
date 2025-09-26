using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services;

/// <summary>
/// Service for generating and comparing configuration hashes
/// </summary>
public class ConfigurationHashService
{
    /// <summary>
    /// Cached JSON serializer options for consistent hash generation
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // MARK: ComputeHash
    public string ComputeHash(PluginConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // Create configuration subset excluding EnableProvider and EnableTask
        var hashConfig = new
        {
            config.PosterStyle,
            config.CutoutType,
            config.CutoutBorder,
            config.LogoPosition,
            config.LogoAlignment,
            config.LogoHeight,
            config.ExtractPoster,
            config.BrightenHDR,
            config.PosterFill,
            config.PosterDimensionRatio,
            config.PosterFileType,
            config.PosterSafeArea,
            config.EnableLetterboxDetection,
            config.LetterboxBlackThreshold,
            config.LetterboxConfidence,
            config.ShowEpisode,
            config.EpisodeFontFamily,
            config.EpisodeFontStyle,
            config.EpisodeFontSize,
            config.EpisodeFontColor,
            config.ShowTitle,
            config.TitleFontFamily,
            config.TitleFontStyle,
            config.TitleFontSize,
            config.TitleFontColor,
            config.OverlayColor
        };

        var json = JsonSerializer.Serialize(hashConfig, JsonOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes);
    }

    // MARK: HasConfigurationChanged
    public bool HasConfigurationChanged(PluginConfiguration config, string previousHash)
    {
        if (string.IsNullOrEmpty(previousHash))
            return true;

        var currentHash = ComputeHash(config);
        return !string.Equals(currentHash, previousHash, StringComparison.OrdinalIgnoreCase);
    }

    // MARK: GetCurrentHash
    public string GetCurrentHash(PluginConfiguration config)
    {
        return ComputeHash(config);
    }
}