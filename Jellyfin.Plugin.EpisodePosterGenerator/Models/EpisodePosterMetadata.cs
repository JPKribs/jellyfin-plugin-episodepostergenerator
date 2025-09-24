using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Models;

/// <summary>
/// Comprehensive metadata container for episode poster generation
/// </summary>
public class EpisodePosterMetadata
{
    /// <summary>
    /// The episode being processed
    /// </summary>
    public Episode Episode { get; set; } = null!;

    /// <summary>
    /// Episode number within the season
    /// </summary>
    public int? EpisodeNumber { get; set; }

    /// <summary>
    /// Season number
    /// </summary>
    public int? SeasonNumber { get; set; }

    /// <summary>
    /// Episode title
    /// </summary>
    public string? EpisodeTitle { get; set; } = string.Empty;

    /// <summary>
    /// Series name
    /// </summary>
    public string? SeriesName { get; set; } = string.Empty;

    /// <summary>
    /// Video duration for timestamp calculation
    /// </summary>
    public TimeSpan? VideoDuration { get; set; }

    /// <summary>
    /// Video codec information for hardware acceleration decisions
    /// </summary>
    public string? VideoCodec { get; set; }

    /// <summary>
    /// Color space information
    /// </summary>
    public string? ColorSpace { get; set; }

    /// <summary>
    /// Color transfer information
    /// </summary>
    public string? ColorTransfer { get; set; }

    /// <summary>
    /// Pixel format information
    /// </summary>
    public string? PixelFormat { get; set; }

    /// <summary>
    /// Calculated timestamp for frame extraction
    /// </summary>
    public TimeSpan? ExtractionTimestamp { get; set; }

    /// <summary>
    /// Hardware acceleration compatibility information
    /// </summary>
    public HardwareAccelerationInfo? HardwareAccelerationInfo { get; set; }

    /// <summary>
    /// Logo information for series
    /// </summary>
    public LogoInfo? LogoInfo { get; set; }

    /// <summary>
    /// Whether video analysis encountered an error
    /// </summary>
    public bool HasVideoAnalysisError { get; set; }

    /// <summary>
    /// Error message from video analysis
    /// </summary>
    public string? VideoAnalysisError { get; set; }
}

/// <summary>
/// Hardware acceleration compatibility information
/// </summary>
public class HardwareAccelerationInfo
{
    /// <summary>
    /// Whether hardware acceleration is enabled in Jellyfin
    /// </summary>
    public bool IsHardwareAccelerationEnabled { get; set; }

    /// <summary>
    /// Type of hardware acceleration configured
    /// </summary>
    public string HardwareAccelerationType { get; set; } = string.Empty;

    /// <summary>
    /// Codecs enabled for hardware decoding
    /// </summary>
    public IReadOnlyList<string> EnabledDecodingCodecs { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether VAAPI acceleration is available
    /// </summary>
    public bool IsVaapiEnabled { get; set; }

    /// <summary>
    /// Whether CUDA acceleration is available
    /// </summary>
    public bool IsCudaEnabled { get; set; }

    /// <summary>
    /// Whether Intel QSV acceleration is available
    /// </summary>
    public bool IsQsvEnabled { get; set; }
}

/// <summary>
/// Logo information for series
/// </summary>
public class LogoInfo
{
    /// <summary>
    /// Series name
    /// </summary>
    public string? SeriesName { get; set; } = string.Empty;

    /// <summary>
    /// Series identifier
    /// </summary>
    public Guid SeriesId { get; set; }

    /// <summary>
    /// Whether the series has a logo image
    /// </summary>
    public bool HasLogo { get; set; }

    /// <summary>
    /// Path to the logo image file
    /// </summary>
    public string? LogoPath { get; set; }
}

/// <summary>
/// Timestamp source configuration options
/// </summary>
public enum TimestampSource
{
    /// <summary>
    /// Extract frame from the beginning of the episode
    /// </summary>
    Beginning,

    /// <summary>
    /// Extract frame from the middle of the episode
    /// </summary>
    Middle,

    /// <summary>
    /// Extract frame from the end of the episode
    /// </summary>
    End,

    /// <summary>
    /// Extract frame from a random timestamp
    /// </summary>
    Random,

    /// <summary>
    /// Extract frame from a custom timestamp
    /// </summary>
    Custom
}