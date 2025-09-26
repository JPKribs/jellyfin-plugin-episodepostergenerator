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
    // MARK: ComputeHash
    public string ComputeHash(PluginConfiguration config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

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
            config.LetterboxDetection,
            config.CroppingThreshold,
            config.CroppingSensitivity,
            config.VideoTimestamp,
            config.ShowSeriesLogo,
            config.ShowEpisodeTitle,
            config.ShowEpisodeNumber,
            config.ShowSeason,
            config.ShowEpisode,
            config.TitleFontFamily,
            config.TitleFontSize,
            config.TitleColor,
            config.TitleAlpha,
            config.TitleBorderSize,
            config.TitleBorderColor,
            config.TitleBorderAlpha,
            config.TitleShadowSize,
            config.TitleShadowColor,
            config.TitleShadowAlpha,
            config.NumberFontFamily,
            config.NumberFontSize,
            config.NumberColor,
            config.NumberAlpha,
            config.NumberBorderSize,
            config.NumberBorderColor,
            config.NumberBorderAlpha,
            config.NumberShadowSize,
            config.NumberShadowColor,
            config.NumberShadowAlpha,
            config.BackgroundType,
            config.BackgroundColor,
            config.BackgroundAlpha,
            config.BackgroundBlurRadius,
            config.BackgroundDarkenAmount,
            config.BorderType,
            config.BorderSize,
            config.BorderColor,
            config.BorderAlpha,
            config.BorderRadius,
            config.EnableHardwareAcceleration,
            config.HardwareAccelerationType,
            config.QualityLevel,
            config.MaxConcurrentOperations,
            config.TimeoutSeconds
        };

        var json = JsonSerializer.Serialize(hashConfig, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
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