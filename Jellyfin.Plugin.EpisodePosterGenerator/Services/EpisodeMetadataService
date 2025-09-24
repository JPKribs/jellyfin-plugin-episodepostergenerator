using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services;

/// <summary>
/// Service responsible for gathering all metadata required for episode poster generation
/// </summary>
public class EpisodeMetadataService
{
    /// <summary>
    /// Logger for metadata gathering operations
    /// </summary>
    private readonly ILogger<EpisodeMetadataService> _logger;

    /// <summary>
    /// FFmpeg service for video analysis and frame extraction
    /// </summary>
    private readonly FFmpegService _ffmpegService;

    /// <summary>
    /// Server configuration manager for hardware acceleration settings
    /// </summary>
    private readonly IServerConfigurationManager _configurationManager;

    // MARK: Constructor
    public EpisodeMetadataService(
        ILogger<EpisodeMetadataService> logger,
        FFmpegService ffmpegService,
        IServerConfigurationManager configurationManager)
    {
        _logger = logger;
        _ffmpegService = ffmpegService;
        _configurationManager = configurationManager;
    }

    // MARK: GatherEpisodeMetadataAsync
    public async Task<EpisodePosterMetadata> GatherEpisodeMetadataAsync(
        Episode episode, 
        PluginConfiguration config, 
        CancellationToken cancellationToken = default)
    {
        var metadata = new EpisodePosterMetadata
        {
            Episode = episode,
            EpisodeNumber = episode.IndexNumber ?? 0,
            SeasonNumber = episode.ParentIndexNumber ?? 0,
            EpisodeTitle = episode.Name ?? "Unknown Episode",
            SeriesName = episode.Series?.Name ?? "Unknown Series"
        };

        // Gather video properties if frame extraction is needed
        if (ShouldExtractFrame(config))
        {
            await GatherVideoPropertiesAsync(metadata, config, cancellationToken).ConfigureAwait(false);
        }

        // Gather hardware acceleration compatibility
        await GatherHardwareAccelerationDataAsync(metadata, cancellationToken).ConfigureAwait(false);

        // Gather logo information if needed
        if (ShouldGatherLogoData(config))
        {
            await GatherLogoDataAsync(metadata, cancellationToken).ConfigureAwait(false);
        }

        return metadata;
    }

    // MARK: ShouldExtractFrame
    private static bool ShouldExtractFrame(PluginConfiguration config)
    {
        return config.PosterStyle != PosterStyle.Logo || config.AllowImageExtraction;
    }

    // MARK: ShouldGatherLogoData
    private static bool ShouldGatherLogoData(PluginConfiguration config)
    {
        return config.PosterStyle == PosterStyle.Logo;
    }

    // MARK: GatherVideoPropertiesAsync
    private async Task GatherVideoPropertiesAsync(
        EpisodePosterMetadata metadata, 
        PluginConfiguration config, 
        CancellationToken cancellationToken)
    {
        try
        {
            var episode = metadata.Episode;
            if (string.IsNullOrEmpty(episode.Path))
            {
                _logger.LogWarning("Episode path is empty for {EpisodeName}", episode.Name);
                return;
            }

            // Get video duration for timestamp calculation
            var duration = await _ffmpegService.GetVideoDurationAsync(episode.Path, cancellationToken).ConfigureAwait(false);
            metadata.VideoDuration = duration;

            // Get video codec for hardware acceleration decisions
            var codec = await _ffmpegService.GetVideoCodecAsync(episode.Path, cancellationToken).ConfigureAwait(false);
            metadata.VideoCodec = codec;

            // Get color properties for HDR/SDR processing
            var (colorSpace, colorTransfer, pixelFormat) = await _ffmpegService.GetVideoColorPropertiesAsync(episode.Path, cancellationToken).ConfigureAwait(false);
            metadata.ColorSpace = colorSpace;
            metadata.ColorTransfer = colorTransfer;
            metadata.PixelFormat = pixelFormat;

            // Calculate optimal timestamp for frame extraction
            metadata.ExtractionTimestamp = CalculateExtractionTimestamp(duration, config);

            _logger.LogDebug("Gathered video properties for {EpisodeName}: Duration={Duration}, Codec={Codec}", 
                episode.Name, duration, codec);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to gather video properties for episode {EpisodeName}", metadata.Episode.Name);
            metadata.HasVideoAnalysisError = true;
            metadata.VideoAnalysisError = ex.Message;
        }
    }

    // MARK: CalculateExtractionTimestamp
    private static TimeSpan CalculateExtractionTimestamp(TimeSpan duration, PluginConfiguration config)
    {
        return config.TimestampSource switch
        {
            TimestampSource.Beginning => TimeSpan.FromSeconds(config.BeginningOffset),
            TimestampSource.Middle => TimeSpan.FromTicks(duration.Ticks / 2),
            TimestampSource.End => duration.Subtract(TimeSpan.FromSeconds(config.EndOffset)),
            TimestampSource.Random => GenerateRandomTimestamp(duration),
            TimestampSource.Custom => TimeSpan.FromSeconds(config.CustomTimestamp),
            _ => TimeSpan.FromTicks(duration.Ticks / 2)
        };
    }

    // MARK: GenerateRandomTimestamp
    private static TimeSpan GenerateRandomTimestamp(TimeSpan duration)
    {
        var random = new Random();
        var totalSeconds = (int)duration.TotalSeconds;
        var randomSeconds = random.Next(30, Math.Max(31, totalSeconds - 30));
        return TimeSpan.FromSeconds(randomSeconds);
    }

    // MARK: GatherHardwareAccelerationDataAsync
    private async Task GatherHardwareAccelerationDataAsync(
        EpisodePosterMetadata metadata, 
        CancellationToken cancellationToken)
    {
        try
        {
            var encoding = _configurationManager.GetEncodingOptions();
            
            metadata.HardwareAccelerationInfo = new HardwareAccelerationInfo
            {
                IsHardwareAccelerationEnabled = encoding.EnableHardwareDecoding,
                HardwareAccelerationType = encoding.HardwareAccelerationType?.ToString() ?? "None",
                EnabledDecodingCodecs = encoding.EnableDecodingColorDepth10Hevc ? new[] { "hevc_10bit" } : Array.Empty<string>(),
                IsVaapiEnabled = encoding.EnableHardwareDecoding && encoding.HardwareAccelerationType.HasValue,
                IsCudaEnabled = encoding.HardwareAccelerationType?.ToString()?.Contains("Nvidia") == true,
                IsQsvEnabled = encoding.HardwareAccelerationType?.ToString()?.Contains("Intel") == true
            };

            _logger.LogDebug("Hardware acceleration status: Enabled={Enabled}, Type={Type}", 
                metadata.HardwareAccelerationInfo.IsHardwareAccelerationEnabled,
                metadata.HardwareAccelerationInfo.HardwareAccelerationType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to gather hardware acceleration information");
            metadata.HardwareAccelerationInfo = new HardwareAccelerationInfo
            {
                IsHardwareAccelerationEnabled = false,
                HardwareAccelerationType = "Unknown"
            };
        }

        await Task.CompletedTask;
    }

    // MARK: GatherLogoDataAsync
    private async Task GatherLogoDataAsync(
        EpisodePosterMetadata metadata, 
        CancellationToken cancellationToken)
    {
        try
        {
            var series = metadata.Episode.Series;
            if (series == null)
            {
                _logger.LogDebug("No series information available for logo gathering");
                return;
            }

            metadata.LogoInfo = new LogoInfo
            {
                SeriesName = series.Name,
                SeriesId = series.Id,
                HasLogo = series.HasImage(MediaBrowser.Model.Entities.ImageType.Logo, 0),
                LogoPath = series.HasImage(MediaBrowser.Model.Entities.ImageType.Logo, 0) 
                    ? series.GetImagePath(MediaBrowser.Model.Entities.ImageType.Logo, 0) 
                    : null
            };

            _logger.LogDebug("Logo info gathered for {SeriesName}: HasLogo={HasLogo}", 
                series.Name, metadata.LogoInfo.HasLogo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to gather logo information for episode {EpisodeName}", metadata.Episode.Name);
        }

        await Task.CompletedTask;
    }
}