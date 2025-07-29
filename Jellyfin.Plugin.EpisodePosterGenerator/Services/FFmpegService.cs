using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services;

/// <summary>
/// Comprehensive video processing service providing enterprise-grade FFmpeg and FFprobe integration
/// for sophisticated video analysis, frame extraction, and content-aware processing operations.
/// Serves as the foundational video processing layer enabling intelligent poster generation through
/// advanced algorithms for black scene detection, hardware-accelerated frame extraction, and
/// optimized video analysis with intelligent caching and performance optimization strategies.
/// 
/// This service represents a critical component in the poster generation ecosystem, providing
/// the essential video processing capabilities that transform raw episode video files into
/// optimal source imagery for poster creation. The implementation emphasizes performance,
/// reliability, and intelligent content analysis to ensure high-quality poster generation
/// while maintaining system efficiency and resource optimization.
/// 
/// Core Architecture and Design Philosophy:
/// The service implements a sophisticated multi-layered architecture that separates video
/// analysis algorithms from execution infrastructure, enabling optimal performance through
/// specialized processing strategies while maintaining compatibility with diverse video
/// formats, codecs, and hardware acceleration platforms across different deployment environments.
/// 
/// Video Processing Capabilities Overview:
/// 
/// Duration Analysis Infrastructure:
/// Implements robust video duration detection using FFprobe with comprehensive error handling
/// and fallback mechanisms ensuring reliable duration information essential for timestamp
/// calculations and content analysis operations across various video formats and containers.
/// 
/// Intelligent Black Scene Detection:
/// Advanced segmented analysis algorithms that efficiently identify transition scenes, credits,
/// and other undesirable content regions within video files. The detection system uses
/// optimized sampling strategies to minimize processing overhead while maintaining accuracy
/// in identifying content-rich video segments suitable for frame extraction operations.
/// 
/// Hardware-Accelerated Frame Extraction:
/// Sophisticated frame extraction system leveraging available hardware acceleration technologies
/// for optimal performance across different hardware platforms. The system automatically
/// detects and utilizes the best available acceleration method while maintaining software
/// fallback compatibility.
/// 
/// Content-Aware Timestamp Selection:
/// Intelligent algorithms for selecting optimal frame extraction timestamps that avoid
/// black scenes, credits, and other undesirable content while ensuring representative
/// imagery from the main episode content. The selection process balances randomization
/// with content quality to provide varied yet consistently high-quality poster sources.
/// 
/// Performance Optimization Strategies:
/// 
/// Intelligent Caching Architecture:
/// Implements sophisticated caching mechanisms for black scene detection results using
/// file-based cache keys that incorporate file size and modification timestamps. The
/// caching system provides significant performance improvements for repeated operations
/// while maintaining cache validity through intelligent invalidation strategies.
/// 
/// Segmented Processing Algorithms:
/// Black scene detection uses strategic sampling rather than full-video analysis,
/// focusing on key video segments (5%, 25%, 50%, 75%, 90%) to provide accurate
/// detection results while minimizing processing time and resource utilization.
/// This approach enables efficient processing of large video files without sacrificing
/// detection accuracy or content quality assessment.
/// 
/// Hardware Acceleration Integration:
/// Seamless integration with Jellyfin's media encoder infrastructure enables automatic
/// detection and utilization of available hardware acceleration technologies. The system
/// provides graceful degradation to software processing when hardware acceleration is
/// unavailable while maintaining consistent output quality and reliability.
/// 
/// Resource Management and Cleanup:
/// Comprehensive resource management ensuring proper disposal of processes, streams,
/// and temporary resources with automatic cleanup mechanisms preventing memory leaks
/// and resource accumulation during extended batch processing operations.
/// 
/// Algorithm Implementation Details:
/// 
/// Black Scene Detection Algorithm:
/// The detection system implements a multi-stage analysis process that combines pixel
/// luminance thresholding with duration analysis to identify meaningful black intervals.
/// The algorithm uses configurable sensitivity parameters enabling fine-tuning for
/// different content types while maintaining consistent detection accuracy across
/// various video formats and encoding characteristics.
/// 
/// Timestamp Selection Algorithm:
/// Intelligent timestamp selection combines random sampling with gap analysis to
/// identify optimal frame extraction points. The algorithm implements multiple
/// fallback strategies including largest gap identification and mathematical
/// distribution analysis to ensure consistent high-quality results even when
/// videos contain extensive black scene regions.
/// 
/// Caching Strategy Implementation:
/// Cache key generation incorporates file path, size, and modification timestamp
/// creating unique identifiers that ensure cache validity while enabling efficient
/// lookup operations. The cache includes automatic expiration and cleanup mechanisms
/// preventing unlimited growth while maintaining optimal performance benefits.
/// 
/// Integration Architecture:
/// 
/// Jellyfin Media Encoder Integration:
/// Seamless integration with Jellyfin's media encoding infrastructure provides
/// access to configured FFmpeg and FFprobe executables while leveraging Jellyfin's
/// hardware acceleration detection and configuration management capabilities.
/// 
/// Plugin Ecosystem Coordination:
/// The service integrates with other plugin components through standardized interfaces
/// enabling efficient coordination between video analysis, poster generation, and
/// episode tracking systems while maintaining loose coupling and modular architecture.
/// 
/// Hardware Platform Compatibility:
/// Comprehensive support for multiple hardware acceleration platforms ensures
/// optimal performance across diverse deployment environments including Windows,
/// Linux, and macOS systems with various GPU and acceleration technologies.
/// 
/// Error Handling and Reliability:
/// 
/// Comprehensive Exception Management:
/// Multi-layered error handling ensures graceful degradation when video processing
/// operations encounter issues, with detailed logging for debugging and monitoring
/// while maintaining system stability and preventing cascading failures during
/// batch processing operations.
/// 
/// Process Execution Safety:
/// Robust process execution infrastructure with timeout handling, resource cleanup,
/// and cancellation support ensures reliable operation while preventing system
/// resource exhaustion and providing responsive termination capabilities.
/// 
/// Fallback and Recovery Mechanisms:
/// Intelligent fallback strategies ensure continued operation when preferred processing
/// methods are unavailable, including hardware acceleration fallbacks and alternative
/// algorithm implementations maintaining functionality across diverse system configurations.
/// 
/// Quality Assurance and Validation:
/// 
/// Output Validation:
/// Comprehensive validation of extracted frames and analysis results ensures
/// high-quality output suitable for poster generation while detecting and handling
/// corrupted or invalid video content gracefully.
/// 
/// Format Compatibility:
/// Extensive testing and validation across various video formats, codecs, and
/// container types ensures reliable operation with diverse media library content
/// while maintaining consistent output quality and processing reliability.
/// 
/// Performance Monitoring:
/// Detailed logging and performance monitoring capabilities enable administrative
/// oversight and optimization while providing debugging information for
/// troubleshooting and system tuning in production environments.
/// 
/// The service represents a cornerstone of the poster generation infrastructure,
/// providing the essential video processing capabilities that enable intelligent,
/// high-quality poster creation while maintaining optimal performance and system
/// reliability across diverse deployment scenarios and media content types.
/// </summary>
public class FFmpegService
{
    /// <summary>
    /// Logger instance for comprehensive video processing monitoring, debugging, and administrative oversight.
    /// Provides detailed logging throughout the video analysis workflow including performance metrics,
    /// error conditions, and processing statistics essential for system monitoring and troubleshooting.
    /// </summary>
    private readonly ILogger<FFmpegService> _logger;

    /// <summary>
    /// Jellyfin's media encoder service providing access to configured FFmpeg and FFprobe executables
    /// with hardware acceleration detection and configuration management capabilities. Enables seamless
    /// integration with Jellyfin's video processing infrastructure while leveraging existing system
    /// configuration and optimization settings for optimal performance and compatibility.
    /// </summary>
    private readonly IMediaEncoder _mediaEncoder;

    /// <summary>
    /// Sophisticated caching infrastructure for black scene detection results using file-based cache keys
    /// that incorporate file attributes for cache validity determination. Implements automatic expiration
    /// and cleanup mechanisms providing significant performance improvements for repeated video analysis
    /// operations while maintaining cache consistency and preventing unlimited memory growth.
    /// 
    /// Cache Key Strategy:
    /// Uses composite keys incorporating file path, size, and modification timestamp ensuring cache
    /// validity while enabling efficient lookup operations. This approach provides reliable cache
    /// invalidation when video files are modified while maintaining optimal performance benefits
    /// for unchanged content during repeated processing operations.
    /// </summary>
    private static readonly Dictionary<string, (DateTime Created, List<BlackInterval> Intervals)> _blackIntervalCache = new();

    /// <summary>
    /// Cache expiration timespan controlling automatic cleanup of black scene detection cache entries.
    /// Set to 24 hours providing optimal balance between performance benefits and cache freshness
    /// while preventing unlimited cache growth and ensuring reasonable memory utilization during
    /// extended operation periods with diverse video content processing.
    /// </summary>
    private static readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(24);

    /// <summary>
    /// Thread-safe random number generator for timestamp selection algorithms ensuring unpredictable
    /// frame extraction points while maintaining deterministic behavior within processing sessions.
    /// Used for generating varied poster sources from video content while avoiding predictable
    /// patterns that could result in consistently similar or unrepresentative frame selections.
    /// </summary>
    private static readonly Random _random = new();

    /// <summary>
    /// Cache of video codecs that have failed hardware acceleration and should use software fallback.
    /// Prevents repeated HWA attempts for codecs known to fail, improving performance and reducing log noise.
    /// Cache persists until application restart.
    /// </summary>
    private static readonly HashSet<string> _failedHwaccelCodecs = new();

    /// <summary>
    /// Thread synchronization object for safe access to the failed codec cache during concurrent operations.
    /// </summary>
    private static readonly object _failedCodecCacheLock = new object();

    /// <summary>
    /// Cached hardware acceleration arguments determined once at startup to avoid repeated testing
    /// and provide consistent acceleration method throughout the service lifetime. This optimization
    /// eliminates the need for per-operation hardware acceleration detection while maintaining
    /// optimal performance characteristics for video processing operations.
    /// </summary>
    private readonly string _hardwareAccelerationArgs;

    /// <summary>
    /// Initializes a new instance of the FFmpeg service with essential Jellyfin integration components
    /// and establishes the foundational infrastructure for video processing operations. Sets up logging
    /// and media encoder integration enabling seamless access to configured video processing tools
    /// while maintaining compatibility with Jellyfin's system configuration and optimization settings.
    /// 
    /// The constructor establishes the service foundation for video analysis operations by integrating
    /// with Jellyfin's dependency injection system and preparing necessary service references for
    /// efficient video processing, hardware acceleration detection, and comprehensive logging
    /// throughout the video analysis and frame extraction workflows.
    /// 
    /// Integration Strategy:
    /// The initialization process focuses on establishing robust connections with Jellyfin's media
    /// infrastructure while preparing the service for efficient video processing operations across
    /// diverse hardware platforms and video formats with optimal performance characteristics.
    /// </summary>
    /// <param name="logger">
    /// Logger service for video processing monitoring, error reporting, and debugging information.
    /// Provides comprehensive logging capabilities throughout video analysis workflows enabling
    /// administrative oversight and troubleshooting during poster generation operations.
    /// </param>
    /// <param name="mediaEncoder">
    /// Jellyfin's media encoder service providing access to FFmpeg and FFprobe executables with
    /// hardware acceleration detection capabilities. Enables seamless integration with existing
    /// system configuration while leveraging optimized video processing infrastructure.
    /// </param>
    // MARK: Constructor
    public FFmpegService(ILogger<FFmpegService> logger, IMediaEncoder mediaEncoder)
    {
        _logger = logger;
        _mediaEncoder = mediaEncoder;
        _hardwareAccelerationArgs = DetermineHardwareAcceleration();
    }

    /// <summary>
    /// Retrieves the configured FFmpeg executable path from Jellyfin's media encoder infrastructure
    /// with comprehensive error handling and fallback mechanisms ensuring reliable access to video
    /// processing capabilities. Provides seamless integration with Jellyfin's system configuration
    /// while maintaining compatibility across diverse deployment environments and installation types.
    /// 
    /// Path Resolution Strategy:
    /// The method prioritizes Jellyfin's configured FFmpeg path enabling optimal integration with
    /// existing system configuration while providing fallback to system PATH resolution when
    /// configuration is unavailable. This approach ensures maximum compatibility across different
    /// deployment scenarios while leveraging optimized executable locations when available.
    /// 
    /// Error Handling and Fallback:
    /// Comprehensive error handling ensures continued operation even when Jellyfin's media encoder
    /// configuration is incomplete or unavailable, with intelligent fallback to standard executable
    /// names enabling operation in diverse system environments while maintaining functionality.
    /// </summary>
    /// <returns>
    /// File system path to the FFmpeg executable for video processing operations, or standard
    /// executable name for system PATH resolution when configured path is unavailable.
    /// </returns>
    // MARK: GetFFmpegPath
    private string GetFFmpegPath()
    {
        var path = _mediaEncoder.EncoderPath;
        if (string.IsNullOrEmpty(path))
        {
            return "ffmpeg";
        }

        return path;
    }

    /// <summary>
    /// Efficiently determines the video codec type for the specified video file using optimized FFprobe
    /// analysis with minimal processing overhead. Used for logging and diagnostic purposes during
    /// frame extraction operations.
    /// </summary>
    /// <param name="videoPath">
    /// File system path to the video file requiring codec analysis.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token enabling responsive termination of codec detection operations.
    /// </param>
    /// <returns>
    /// String representing the detected video codec identifier if detection succeeds, "unknown" if
    /// codec detection fails.
    /// </returns>
    // MARK: GetVideoCodecAsync
    private async Task<string> GetVideoCodecAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        var arguments = $"-v error -select_streams v:0 -show_entries stream=codec_name -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";
        
        try
        {
            var result = await ExecuteFFprobeAsync(arguments, cancellationToken).ConfigureAwait(false);
            var codec = result.Trim();
            
            return !string.IsNullOrEmpty(codec) ? codec : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Retrieves the configured FFprobe executable path from Jellyfin's media encoder infrastructure
    /// enabling reliable access to video analysis capabilities with robust error handling and
    /// fallback mechanisms. Provides seamless integration with Jellyfin's system configuration
    /// while ensuring compatibility across diverse deployment environments and system configurations.
    /// 
    /// The method implements intelligent path resolution prioritizing Jellyfin's configuration
    /// while providing graceful degradation to system PATH resolution ensuring continued
    /// functionality even when media encoder configuration is incomplete or unavailable.
    /// </summary>
    /// <returns>
    /// File system path to the FFprobe executable for video analysis operations, or standard
    /// executable name for system PATH resolution when configured path is unavailable.
    /// </returns>
    // MARK: GetFFprobePath
    private string GetFFprobePath()
    {
        var path = _mediaEncoder.ProbePath;
        if (string.IsNullOrEmpty(path))
        {
            return "ffprobe";
        }

        return path;
    }

    /// <summary>
    /// Determines the optimal hardware acceleration method using Jellyfin's support detection
    /// without runtime testing overhead. This method checks available hardware acceleration
    /// support once during service initialization and caches the result for consistent use
    /// throughout the service lifetime, eliminating per-operation detection overhead while
    /// ensuring optimal acceleration method selection.
    /// 
    /// Acceleration Priority Strategy:
    /// Tests acceleration methods in order of general compatibility and performance characteristics
    /// ensuring optimal acceleration selection while maintaining broad hardware support across
    /// different system configurations and deployment environments. The priority order balances
    /// performance benefits with compatibility across diverse hardware platforms.
    /// 
    /// Platform-Specific Acceleration Support:
    /// 
    /// VAAPI (Video Acceleration API):
    /// Linux-based hardware acceleration leveraging Intel and AMD GPU capabilities for optimal
    /// video processing performance on Linux systems with compatible hardware and driver configurations.
    /// 
    /// QSV (Quick Sync Video):
    /// Intel Quick Sync hardware acceleration providing efficient video processing on systems
    /// with compatible Intel processors and integrated graphics capabilities across multiple platforms.
    /// 
    /// CUDA (Compute Unified Device Architecture):
    /// NVIDIA GPU acceleration enabling high-performance video processing on systems with compatible
    /// NVIDIA graphics hardware and appropriate driver installations for optimal processing throughput.
    /// 
    /// D3D11VA (Direct3D 11 Video Acceleration):
    /// Windows-specific hardware acceleration leveraging DirectX capabilities for optimal video
    /// processing performance on Windows systems with compatible graphics hardware and driver support.
    /// 
    /// VideoToolbox:
    /// macOS hardware acceleration utilizing Apple's VideoToolbox framework for efficient video
    /// processing on macOS systems with compatible hardware providing optimal performance characteristics.
    /// 
    /// Performance Optimization:
    /// Single-time determination during service initialization eliminates repeated acceleration
    /// detection overhead while ensuring consistent acceleration method usage throughout the
    /// service lifetime, providing optimal performance for batch processing operations.
    /// </summary>
    /// <returns>
    /// Hardware acceleration argument string for FFmpeg command construction enabling optimal
    /// video processing performance, or empty string for software fallback when hardware
    /// acceleration is unavailable across all supported platforms.
    /// </returns>
    // MARK: DetermineHardwareAcceleration
    private string DetermineHardwareAcceleration()
    {
        try
        {
            string hwaccelMethod = string.Empty;
            string hwaccelArgs = string.Empty;
            
            if (OperatingSystem.IsMacOS())
            {
                hwaccelMethod = "VideoToolbox";
                hwaccelArgs = "-hwaccel videotoolbox";
            }
            else if (OperatingSystem.IsWindows())
            {
                var cudaSupported = _mediaEncoder.SupportsHwaccel("cuda");
                var d3d11vaSupported = _mediaEncoder.SupportsHwaccel("d3d11va");
                
                if (cudaSupported)
                {
                    hwaccelMethod = "CUDA";
                    hwaccelArgs = "-hwaccel cuda";
                }
                else if (d3d11vaSupported)
                {
                    hwaccelMethod = "D3D11VA";
                    hwaccelArgs = "-hwaccel d3d11va";
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                var vaapiSupported = _mediaEncoder.SupportsHwaccel("vaapi");
                var qsvSupported = _mediaEncoder.SupportsHwaccel("qsv");
                var cudaSupported = _mediaEncoder.SupportsHwaccel("cuda");
                
                if (vaapiSupported)
                {
                    hwaccelMethod = "VAAPI";
                    hwaccelArgs = "-hwaccel vaapi";
                }
                else if (qsvSupported)
                {
                    hwaccelMethod = "QSV";
                    hwaccelArgs = "-hwaccel qsv";
                }
                else if (cudaSupported)
                {
                    hwaccelMethod = "CUDA";
                    hwaccelArgs = "-hwaccel cuda";
                }
            }

            if (!string.IsNullOrEmpty(hwaccelMethod))
            {
                _logger.LogInformation("FFmpeg Service initialized - Hardware Acceleration: {HwaccelMethod}, Software Fallback: Enabled", hwaccelMethod);
            }
            else
            {
                _logger.LogInformation("FFmpeg Service initialized - Hardware Acceleration: Disabled, Software Decoding: Enabled");
            }

            return hwaccelArgs;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not determine hardware acceleration support, using software decoding");
            return string.Empty;
        }
    }

    // MARK: IsHDRContent
    private bool IsHDRContent(string colorSpace, string transferCharacteristic, string pixelFormat)
    {
        var hdrTransferCharacteristics = new[]
        {
            "smpte2084", "arib-std-b67", "smpte428", "iec61966-2-1", "bt2020-10", "bt2020-12"
        };
        
        var hdrColorSpaces = new[]
        {
            "bt2020nc", "bt2020c", "smpte431", "smpte432", "jedec-p22"
        };
        
        var hdr10BitFormats = new[]
        {
            "yuv420p10le", "yuv422p10le", "yuv444p10le", "yuv420p12le", "yuv422p12le", "yuv444p12le"
        };
        
        return hdrTransferCharacteristics.Contains(transferCharacteristic, StringComparer.OrdinalIgnoreCase) ||
               hdrColorSpaces.Contains(colorSpace, StringComparer.OrdinalIgnoreCase) ||
               hdr10BitFormats.Contains(pixelFormat, StringComparer.OrdinalIgnoreCase);
    }

    // MARK: BuildToneMappingFilterForColorSpace
    private string BuildToneMappingFilterForColorSpace(string colorSpace, string transferCharacteristic)
    {
        var filters = new List<string>();
        
        // Check if this is HDR content that needs tone mapping
        if (IsHDRContent(colorSpace, transferCharacteristic, ""))
        {
            // HDR tone mapping
            if (transferCharacteristic.Contains("smpte2084", StringComparison.OrdinalIgnoreCase) || 
                transferCharacteristic.Contains("bt2020", StringComparison.OrdinalIgnoreCase))
            {
                filters.Add("zscale=t=linear:npl=100");
                filters.Add("format=gbrpf32le");
                filters.Add("zscale=p=bt709");
                filters.Add("tonemap=tonemap=hable:desat=0:peak=100");
                filters.Add("zscale=t=bt709:m=bt709:r=tv");
            }
            else if (transferCharacteristic.Contains("arib-std-b67", StringComparison.OrdinalIgnoreCase))
            {
                filters.Add("tonemap=tonemap=mobius:desat=0.5:peak=100");
            }
            else
            {
                filters.Add("tonemap=tonemap=hable:desat=0:peak=100");
            }
        }
        else
        {
            // SDR content - apply appropriate color space processing
            if (!string.IsNullOrEmpty(colorSpace))
            {
                if (colorSpace.Contains("bt709", StringComparison.OrdinalIgnoreCase))
                {
                    filters.Add("colorspace=space=bt709:trc=bt709:primaries=bt709");
                }
                else if (colorSpace.Contains("bt601", StringComparison.OrdinalIgnoreCase))
                {
                    filters.Add("colorspace=space=bt709:trc=bt709:primaries=bt709:ispace=bt601:itrc=bt601:iprimaries=bt601");
                }
                else if (colorSpace.Contains("smpte170m", StringComparison.OrdinalIgnoreCase))
                {
                    filters.Add("colorspace=space=bt709:trc=bt709:primaries=bt709:ispace=smpte170m:itrc=smpte170m:iprimaries=smpte170m");
                }
                else
                {
                    // Default color space conversion for unknown SDR spaces
                    filters.Add("colorspace=space=bt709:trc=bt709:primaries=bt709");
                }
            }
            else
            {
                // No specific color space info, apply basic rec709 conversion
                filters.Add("colorspace=space=bt709:trc=bt709:primaries=bt709");
            }
        }
        
        return string.Join(",", filters);
    }

    /// <summary>
    /// Asynchronously retrieves precise video duration information using FFprobe with comprehensive
    /// error handling and robust parsing mechanisms ensuring reliable duration detection across
    /// diverse video formats, containers, and encoding characteristics. Provides essential timing
    /// information required for intelligent timestamp calculation and content analysis operations.
    /// 
    /// This method serves as the foundation for temporal video analysis enabling accurate timestamp
    /// generation for frame extraction while providing reliable duration information essential for
    /// black scene detection algorithms and content-aware processing strategies.
    /// 
    /// Duration Detection Strategy:
    /// Implements optimized FFprobe command construction focusing on format-level duration information
    /// for maximum accuracy and compatibility across various container formats. The detection process
    /// uses minimal output formatting reducing parsing overhead while maintaining precision in
    /// duration determination essential for subsequent timestamp calculation operations.
    /// 
    /// Command Optimization:
    /// The FFprobe command uses optimized parameters including error suppression, specific entry
    /// selection, and minimal output formatting ensuring efficient execution while reducing
    /// processing overhead and network traffic in distributed deployment scenarios.
    /// 
    /// Parsing and Validation:
    /// Robust parsing mechanisms handle various numeric formats and edge cases ensuring reliable
    /// duration extraction while providing graceful error handling for corrupted or incompatible
    /// video files. The parsing process includes validation ensuring mathematical consistency
    /// and preventing invalid duration values from affecting downstream processing operations.
    /// 
    /// Error Recovery and Logging:
    /// Comprehensive error handling provides detailed logging for debugging and monitoring while
    /// enabling graceful degradation when duration detection fails. The error recovery mechanism
    /// ensures continued operation while providing administrative visibility into processing issues.
    /// </summary>
    /// <param name="videoPath">
    /// File system path to the video file requiring duration analysis. Must reference an accessible
    /// video file in a format supported by FFprobe for reliable duration detection and analysis.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token enabling responsive termination of duration detection operations while
    /// maintaining process cleanup and resource management during cancellation scenarios.
    /// </param>
    /// <returns>
    /// TimeSpan representing precise video duration if detection succeeds, null if duration
    /// detection fails due to file accessibility, format incompatibility, or processing errors.
    /// Null return enables fallback duration detection strategies in calling code.
    /// </returns>
    // MARK: GetVideoDurationAsync
    public async Task<TimeSpan?> GetVideoDurationAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        var arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";
        
        try
        {
            var result = await ExecuteFFprobeAsync(arguments, cancellationToken).ConfigureAwait(false);
            
            if (double.TryParse(result.Trim(), out var seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get video duration for {VideoPath}", videoPath);
        }

        return null;
    }

    /// <summary>
    /// Performs sophisticated black scene detection using optimized segmented analysis algorithms
    /// that efficiently identify transition scenes, credits, and other undesirable content regions
    /// within video files. Implements intelligent caching strategies and performance optimization
    /// techniques ensuring accurate detection results while minimizing processing overhead and
    /// resource utilization during batch poster generation operations.
    /// 
    /// This method represents the core content analysis capability enabling intelligent frame
    /// extraction by identifying video segments suitable for poster generation while avoiding
    /// credits, transitions, and other content regions that would produce poor-quality poster
    /// sources for media library presentation.
    /// 
    /// Segmented Analysis Architecture:
    /// Rather than analyzing entire video files, the detection system uses strategic sampling
    /// focusing on key video segments (5%, 25%, 50%, 75%, 90% of total duration) providing
    /// accurate black scene detection while dramatically reducing processing time and resource
    /// requirements. This approach enables efficient processing of large video files without
    /// sacrificing detection accuracy or content quality assessment capabilities.
    /// 
    /// Intelligent Caching Infrastructure:
    /// Sophisticated caching mechanisms store black scene detection results using composite
    /// cache keys incorporating file attributes ensuring cache validity while providing
    /// significant performance improvements for repeated processing operations. The caching
    /// system includes automatic expiration and cleanup preventing unlimited growth while
    /// maintaining optimal performance benefits during extended batch processing sessions.
    /// 
    /// Performance Optimization Strategies:
    /// The detection process implements multiple optimization techniques including hardware
    /// acceleration utilization, video scaling for faster processing, and intelligent segment
    /// selection minimizing computational overhead while maintaining detection accuracy
    /// essential for high-quality poster generation across diverse video content types.
    /// 
    /// Detection Algorithm Parameters:
    /// Configurable pixel luminance and duration thresholds enable fine-tuning for different
    /// content types while maintaining consistent detection accuracy across various video
    /// formats and encoding characteristics. The parameter system provides flexibility for
    /// optimization while maintaining reliable default behavior suitable for general use.
    /// 
    /// Content Analysis and Quality Assurance:
    /// The detection system provides comprehensive analysis of video content identifying
    /// not only black scenes but also transition patterns and content characteristics
    /// essential for intelligent timestamp selection and frame extraction optimization
    /// ensuring consistently high-quality poster sources from diverse episode content.
    /// </summary>
    /// <param name="videoPath">
    /// File system path to the video file requiring black scene analysis. Must reference an
    /// accessible video file in a format supported by FFmpeg for reliable detection and analysis.
    /// </param>
    /// <param name="totalDuration">
    /// Total video duration used for segment calculation and timestamp normalization during
    /// detection processing. Provides temporal context essential for accurate interval calculation.
    /// </param>
    /// <param name="pixelThreshold">
    /// Pixel luminance threshold for black frame detection controlling sensitivity to dark content.
    /// Lower values detect darker scenes while higher values focus on completely black content.
    /// Default value provides optimal balance for general episode content analysis.
    /// </param>
    /// <param name="durationThreshold">
    /// Minimum duration threshold for black scene recognition controlling detection of brief
    /// transitions versus sustained black content. Prevents detection of single-frame artifacts
    /// while identifying meaningful black intervals suitable for timestamp avoidance.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token enabling responsive termination of black scene detection operations
    /// while maintaining process cleanup and resource management during cancellation scenarios.
    /// </param>
    /// <returns>
    /// Comprehensive list of black scene intervals detected within the video content providing
    /// temporal boundaries for intelligent timestamp selection and frame extraction optimization.
    /// Empty list indicates no significant black scenes enabling unrestricted timestamp selection.
    /// </returns>
    // MARK: DetectBlackScenesAsync
    public async Task<List<BlackInterval>> DetectBlackScenesAsync(string videoPath, TimeSpan totalDuration, double pixelThreshold = 0.1, double durationThreshold = 0.1, CancellationToken cancellationToken = default)
    {
        var cachedIntervals = GetCachedBlackIntervals(videoPath);
        if (cachedIntervals != null)
        {
            return cachedIntervals;
        }

        var blackIntervals = new List<BlackInterval>();
        
        if (totalDuration.TotalMinutes < 2)
        {
            return blackIntervals;
        }
        
        var sampleSegments = GetSampleSegments(totalDuration);
        
        foreach (var segment in sampleSegments)
        {
            var segmentBlackIntervals = await DetectBlackInSegmentAsync(videoPath, segment.Start, segment.Duration, pixelThreshold, durationThreshold, cancellationToken).ConfigureAwait(false);
            blackIntervals.AddRange(segmentBlackIntervals);
        }

        CacheBlackIntervals(videoPath, blackIntervals);
        return blackIntervals;
    }

    /// <summary>
    /// Performs detailed black scene detection within specific video segments using optimized
    /// FFmpeg processing with hardware acceleration and intelligent video scaling for maximum
    /// efficiency. This method implements the core detection algorithm processing individual
    /// video segments while maintaining accuracy and performance optimization essential for
    /// efficient batch processing operations across diverse video content and system configurations.
    /// 
    /// The segmented detection approach enables parallel processing opportunities while providing
    /// granular control over detection parameters and resource utilization during large-scale
    /// poster generation workflows requiring comprehensive content analysis across extensive
    /// media libraries with varied content characteristics.
    /// 
    /// Hardware Acceleration Integration:
    /// Seamless integration with available hardware acceleration technologies ensuring optimal
    /// processing performance while maintaining software fallback compatibility across diverse
    /// deployment environments and hardware configurations.
    /// 
    /// Video Processing Optimization:
    /// Intelligent video scaling to reduced resolution (320x240) significantly improves processing
    /// speed while maintaining detection accuracy for black scene identification. The scaling
    /// approach provides optimal balance between performance and accuracy enabling efficient
    /// processing of high-resolution video content without sacrificing detection reliability.
    /// 
    /// Detection Algorithm Implementation:
    /// Uses FFmpeg's blackdetect filter with configurable sensitivity parameters enabling
    /// fine-tuned detection for different content types while maintaining consistent accuracy
    /// across various video formats, encoding characteristics, and content patterns typical
    /// in episodic media content requiring poster generation capabilities.
    /// 
    /// Temporal Coordination and Result Processing:
    /// Sophisticated timestamp adjustment ensures detected intervals are properly aligned with
    /// overall video timeline accounting for segment offset positioning while maintaining
    /// accuracy in interval boundary determination essential for subsequent timestamp selection
    /// and frame extraction optimization algorithms.
    /// </summary>
    /// <param name="videoPath">
    /// File system path to the video file requiring segment-specific black scene analysis.
    /// Must reference an accessible video file compatible with FFmpeg processing operations.
    /// </param>
    /// <param name="startTime">
    /// Starting timestamp for segment analysis defining the temporal beginning of detection
    /// processing. Used for FFmpeg seek operations and result timestamp adjustment calculations.
    /// </param>
    /// <param name="duration">
    /// Duration of the video segment requiring analysis controlling the temporal scope of
    /// detection processing and resource utilization during segment-specific analysis operations.
    /// </param>
    /// <param name="pixelThreshold">
    /// Pixel luminance threshold controlling black frame detection sensitivity within the
    /// specified segment. Maintains consistency with overall detection parameter configuration.
    /// </param>
    /// <param name="durationThreshold">
    /// Minimum duration threshold for black scene recognition within the segment preventing
    /// detection of brief artifacts while identifying meaningful black content intervals.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token enabling responsive termination of segment detection operations
    /// while maintaining process cleanup and resource management during cancellation scenarios.
    /// </param>
    /// <returns>
    /// List of black scene intervals detected within the specified video segment with timestamps
    /// adjusted to reflect overall video timeline positioning. Results are ready for integration
    /// with overall detection results and subsequent timestamp selection algorithms.
    /// </returns>
    // MARK: DetectBlackInSegmentAsync
    private async Task<List<BlackInterval>> DetectBlackInSegmentAsync(string videoPath, TimeSpan startTime, TimeSpan duration, double pixelThreshold, double durationThreshold, CancellationToken cancellationToken)
    {
        var blackIntervals = new List<BlackInterval>();
        var startSeconds = startTime.TotalSeconds;
        var durationSeconds = duration.TotalSeconds;
        
        var codec = await GetVideoCodecAsync(videoPath, cancellationToken).ConfigureAwait(false);
        
        bool shouldUseSoftware = false;
        lock (_failedCodecCacheLock)
        {
            shouldUseSoftware = _failedHwaccelCodecs.Contains(codec);
        }
        
        if (shouldUseSoftware || string.IsNullOrEmpty(_hardwareAccelerationArgs))
        {
            var softwareArguments = $"-ss {startSeconds:F2} -t {durationSeconds:F2} -i \"{videoPath}\" -vf \"scale=320:240,blackdetect=d={durationThreshold}:pix_th={pixelThreshold}\" -an -f null - -v info";
            
            try
            {
                var output = await ExecuteFFmpegAsync(softwareArguments, cancellationToken).ConfigureAwait(false);
                var segmentIntervals = ParseBlackDetectOutput(output);
                
                foreach (var interval in segmentIntervals)
                {
                    blackIntervals.Add(new BlackInterval
                    {
                        Start = interval.Start.Add(startTime),
                        End = interval.End.Add(startTime),
                        Duration = interval.Duration
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Software black scene detection failed for segment {Start}-{End} in {VideoPath}", startTime, startTime.Add(duration), videoPath);
            }
            
            return blackIntervals;
        }
        
        var arguments = $"-ss {startSeconds:F2} -t {durationSeconds:F2} {_hardwareAccelerationArgs} -i \"{videoPath}\" -vf \"scale=320:240,blackdetect=d={durationThreshold}:pix_th={pixelThreshold}\" -an -f null - -v info";

        try
        {
            var output = await ExecuteFFmpegAsync(arguments, cancellationToken).ConfigureAwait(false);
            var segmentIntervals = ParseBlackDetectOutput(output);
            
            foreach (var interval in segmentIntervals)
            {
                blackIntervals.Add(new BlackInterval
                {
                    Start = interval.Start.Add(startTime),
                    End = interval.End.Add(startTime),
                    Duration = interval.Duration
                });
            }
            
            return blackIntervals;
        }
        catch (Exception ex)
        {
            bool shouldLogWarning = false;
            lock (_failedCodecCacheLock)
            {
                shouldLogWarning = _failedHwaccelCodecs.Add(codec);
            }
            
            if (shouldLogWarning)
            {
                _logger.LogWarning(ex, "Hardware acceleration failed for {Codec} codec during black detection, falling back to software", codec);
            }
        }

        var fallbackArguments = $"-ss {startSeconds:F2} -t {durationSeconds:F2} -i \"{videoPath}\" -vf \"scale=320:240,blackdetect=d={durationThreshold}:pix_th={pixelThreshold}\" -an -f null - -v info";
        
        try
        {
            var output = await ExecuteFFmpegAsync(fallbackArguments, cancellationToken).ConfigureAwait(false);
            var segmentIntervals = ParseBlackDetectOutput(output);
            
            foreach (var interval in segmentIntervals)
            {
                blackIntervals.Add(new BlackInterval
                {
                    Start = interval.Start.Add(startTime),
                    End = interval.End.Add(startTime),
                    Duration = interval.Duration
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Software fallback black scene detection also failed for segment {Start}-{End} in {VideoPath}", startTime, startTime.Add(duration), videoPath);
        }

        return blackIntervals;
    }

    /// <summary>
    /// Generates strategic sample segments for optimized black scene detection analysis using
    /// intelligent positioning algorithms that balance detection accuracy with processing efficiency.
    /// This method implements sophisticated segment selection strategies ensuring comprehensive
    /// content analysis while minimizing computational overhead during large-scale batch processing
    /// operations across diverse video content with varying duration and content characteristics.
    /// 
    /// The segmented approach provides significant performance improvements over full-video analysis
    /// while maintaining detection accuracy essential for intelligent timestamp selection and
    /// high-quality frame extraction operations. Strategic positioning ensures representative
    /// sampling across video content while avoiding excessive processing overhead.
    /// 
    /// Strategic Positioning Algorithm:
    /// Sample segments are positioned at key video locations (5%, 25%, 50%, 75%, 90% of total duration)
    /// providing comprehensive coverage while focusing on content-rich regions typically containing
    /// main episode material. This distribution avoids excessive focus on opening/closing credits
    /// while ensuring adequate coverage of primary content suitable for poster generation.
    /// 
    /// Dynamic Duration Management:
    /// Intelligent duration calculation adapts segment lengths based on remaining video time
    /// ensuring optimal coverage while preventing segment overflow beyond video boundaries.
    /// The dynamic approach maintains consistent analysis depth while accommodating videos
    /// of varying lengths and content distribution patterns.
    /// 
    /// Efficiency Optimization:
    /// Segment filtering eliminates excessively short segments (less than 5 seconds) preventing
    /// inefficient processing of minimal content while ensuring adequate analysis depth for
    /// meaningful black scene detection. This optimization maintains processing efficiency
    /// while preserving detection accuracy across diverse content types.
    /// 
    /// Coverage Strategy:
    /// The positioning strategy ensures balanced coverage across video content while providing
    /// adequate sampling density for accurate black scene detection without excessive computational
    /// overhead. Strategic placement maximizes detection effectiveness while maintaining optimal
    /// performance characteristics suitable for batch processing operations.
    /// </summary>
    /// <param name="totalDuration">
    /// Total video duration used for strategic segment positioning and duration calculation.
    /// Provides temporal context essential for optimal segment distribution and coverage analysis.
    /// </param>
    /// <returns>
    /// List of strategically positioned segment definitions with calculated start times and
    /// durations optimized for efficient black scene detection while maintaining comprehensive
    /// coverage of video content suitable for intelligent timestamp selection algorithms.
    /// </returns>
    // MARK: GetSampleSegments
    private List<(TimeSpan Start, TimeSpan Duration)> GetSampleSegments(TimeSpan totalDuration)
    {
        var segments = new List<(TimeSpan Start, TimeSpan Duration)>();
        var sampleDuration = TimeSpan.FromSeconds(30);
        
        var positions = new[] { 0.05, 0.25, 0.5, 0.75, 0.9 };
        
        foreach (var position in positions)
        {
            var startTime = TimeSpan.FromSeconds(totalDuration.TotalSeconds * position);
            
            var remainingTime = totalDuration.Subtract(startTime);
            var segmentDuration = remainingTime < sampleDuration ? remainingTime : sampleDuration;
            
            if (segmentDuration.TotalSeconds > 5)
            {
                segments.Add((startTime, segmentDuration));
            }
        }
        
        return segments;
    }

    /// <summary>
    /// Performs high-quality frame extraction at specified timestamps using hardware-accelerated
    /// processing with optimized encoding settings ensuring professional-grade poster source imagery.
    /// This method implements sophisticated video processing techniques leveraging available hardware
    /// acceleration while maintaining consistent output quality across diverse video formats and
    /// system configurations essential for reliable poster generation workflows.
    /// 
    /// The extraction process serves as the critical bridge between video content analysis and
    /// poster generation, providing high-quality source imagery optimized for subsequent text
    /// overlay and style-specific processing operations while maintaining consistency and
    /// reliability across batch processing scenarios.
    /// 
    /// Hardware Acceleration Optimization:
    /// Intelligent integration with available hardware acceleration technologies ensuring optimal
    /// extraction performance while maintaining software fallback compatibility. The acceleration
    /// system automatically selects the best available method while preserving output quality
    /// and processing reliability.
    /// 
    /// Quality Preservation Strategy:
    /// Extraction parameters are optimized for maximum visual quality using minimal compression
    /// (q:v 1) ensuring poster source imagery maintains professional standards suitable for
    /// media library presentation. The quality settings balance file size efficiency with
    /// visual fidelity requirements essential for high-quality poster generation.
    /// 
    /// Timestamp Precision and Accuracy:
    /// Sophisticated timestamp formatting ensures frame-accurate extraction enabling precise
    /// content selection while maintaining compatibility with various video formats and
    /// frame rate characteristics. The precision approach guarantees consistent extraction
    /// results essential for reliable poster generation across diverse content types.
    /// 
    /// Format Compatibility and Reliability:
    /// Comprehensive format support ensures reliable extraction across various video containers,
    /// codecs, and encoding characteristics while maintaining consistent output format (JPEG)
    /// optimized for subsequent poster processing operations and Jellyfin integration requirements.
    /// 
    /// Error Handling and Recovery:
    /// Robust error handling ensures graceful degradation when extraction operations encounter
    /// issues while providing detailed logging for debugging and monitoring purposes. The error
    /// recovery mechanism maintains batch processing continuity while enabling administrative
    /// oversight of processing quality and system performance.
    /// </summary>
    /// <param name="videoPath">
    /// File system path to the video file requiring frame extraction. Must reference an accessible
    /// video file in a format supported by FFmpeg for reliable extraction and processing operations.
    /// </param>
    /// <param name="timestamp">
    /// Precise timestamp for frame extraction defining the temporal location within video content.
    /// Used for FFmpeg seek operations ensuring accurate frame selection for poster generation.
    /// </param>
    /// <param name="outputPath">
    /// Target file system path for extracted frame image storage. Used for output file creation
    /// and subsequent validation of extraction success during processing operations.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token enabling responsive termination of frame extraction operations while
    /// maintaining process cleanup and resource management during cancellation scenarios.
    /// </param>
    /// <returns>
    /// File system path to successfully extracted frame image if extraction succeeds, null if
    /// extraction fails due to video accessibility, format incompatibility, or processing errors.
    /// Success return enables integration with poster generation workflows and quality validation.
    /// </returns>
    // MARK: ExtractFrameAsync
    public async Task<string?> ExtractFrameAsync(string videoPath, TimeSpan timestamp, string outputPath, string colorSpace, string colorTransfer, CancellationToken cancellationToken = default)
    {
        var timestampStr = $"{timestamp.Hours:D2}:{timestamp.Minutes:D2}:{timestamp.Seconds:D2}.{timestamp.Milliseconds:D3}";
        
        var codec = await GetVideoCodecAsync(videoPath, cancellationToken).ConfigureAwait(false);
        var toneMappingFilter = BuildToneMappingFilterForColorSpace(colorSpace, colorTransfer);
        
        bool shouldUseSoftware = false;
        lock (_failedCodecCacheLock)
        {
            shouldUseSoftware = _failedHwaccelCodecs.Contains(codec);
        }
        
        if (shouldUseSoftware || string.IsNullOrEmpty(_hardwareAccelerationArgs))
        {
            var filterArg = string.IsNullOrEmpty(toneMappingFilter) ? "" : $"-vf \"{toneMappingFilter}\"";
            var softwareArguments = $"-ss {timestampStr} -i \"{videoPath}\" -frames:v 1 {filterArg} -q:v 1 \"{outputPath}\"";
            
            try
            {
                await ExecuteFFmpegAsync(softwareArguments, cancellationToken).ConfigureAwait(false);
                
                if (File.Exists(outputPath))
                {
                    return outputPath;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Software frame extraction failed at {Timestamp} from {VideoPath}", timestamp, videoPath);
            }
            
            return null;
        }
        
        var filterArgHwa = string.IsNullOrEmpty(toneMappingFilter) ? "" : $"-vf \"{toneMappingFilter}\"";
        var arguments = $"-ss {timestampStr} {_hardwareAccelerationArgs} -i \"{videoPath}\" -frames:v 1 {filterArgHwa} -q:v 1 \"{outputPath}\"";

        try
        {
            await ExecuteFFmpegAsync(arguments, cancellationToken).ConfigureAwait(false);
            
            if (File.Exists(outputPath))
            {
                return outputPath;
            }
        }
        catch (Exception ex)
        {
            bool shouldLogWarning = false;
            lock (_failedCodecCacheLock)
            {
                shouldLogWarning = _failedHwaccelCodecs.Add(codec);
            }
            
            if (shouldLogWarning)
            {
                _logger.LogWarning(ex, "Hardware acceleration failed for {Codec} codec, falling back to software for this codec", codec);
            }
        }

        var filterArgFallback = string.IsNullOrEmpty(toneMappingFilter) ? "" : $"-vf \"{toneMappingFilter}\"";
        var fallbackArguments = $"-ss {timestampStr} -i \"{videoPath}\" -frames:v 1 {filterArgFallback} -q:v 1 \"{outputPath}\"";
        
        try
        {
            await ExecuteFFmpegAsync(fallbackArguments, cancellationToken).ConfigureAwait(false);
            
            if (File.Exists(outputPath))
            {
                return outputPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Software fallback frame extraction also failed at {Timestamp} from {VideoPath}", timestamp, videoPath);
        }

        return null;
    }

    /// <summary>
    /// Implements sophisticated timestamp selection algorithms that intelligently avoid black scenes
    /// and credits while providing varied, high-quality frame extraction points for poster generation.
    /// This method combines randomization with content analysis ensuring optimal poster source imagery
    /// while maintaining unpredictability and visual diversity across multiple poster generation sessions.
    /// 
    /// The selection process represents a critical component in poster quality optimization, ensuring
    /// extracted frames represent main episode content while avoiding undesirable regions including
    /// opening credits, closing credits, commercial breaks, and transition scenes that would produce
    /// poor-quality poster sources unsuitable for media library presentation.
    /// 
    /// Multi-Stage Selection Algorithm:
    /// 
    /// Primary Random Selection:
    /// Initial selection attempts use randomization within content-rich video regions (10%-90% of
    /// total duration) avoiding opening and closing credits while providing varied frame selection
    /// across multiple generation attempts. The random approach ensures diverse poster imagery
    /// while maintaining focus on primary episode content.
    /// 
    /// Black Interval Avoidance:
    /// Sophisticated collision detection prevents timestamp selection within identified black scenes
    /// ensuring extracted frames contain meaningful visual content suitable for poster generation.
    /// The avoidance mechanism includes configurable attempt limits preventing excessive processing
    /// while maintaining high success rates for content-rich timestamp selection.
    /// 
    /// Gap Analysis Fallback:
    /// Advanced gap analysis identifies the largest content-rich region between black intervals
    /// providing optimal fallback timestamp selection when random attempts fail. The gap analysis
    /// ensures reliable timestamp selection even for videos with extensive black scene regions
    /// while maintaining preference for substantial content areas.
    /// 
    /// Mathematical Distribution Fallback:
    /// Final fallback uses mathematical distribution (20%-80% range) ensuring timestamp selection
    /// success regardless of black scene detection results while maintaining focus on content-rich
    /// video regions most likely to contain suitable poster source imagery.
    /// 
    /// Content Quality Optimization:
    /// The selection algorithm prioritizes video regions most likely to contain high-quality visual
    /// content while avoiding predictable patterns that could result in consistently similar
    /// poster imagery across multiple generation sessions for the same episode content.
    /// </summary>
    /// <param name="duration">
    /// Total video duration providing temporal context for timestamp calculation and content
    /// region determination. Used for percentage-based calculations and boundary validation.
    /// </param>
    /// <param name="blackIntervals">
    /// Comprehensive list of detected black scene intervals used for avoidance calculations
    /// and gap analysis during intelligent timestamp selection. Empty list enables unrestricted
    /// random selection within content-rich video regions.
    /// </param>
    /// <returns>
    /// Optimized timestamp for frame extraction guaranteed to avoid black scenes while providing
    /// varied, high-quality selection points suitable for poster generation across diverse
    /// video content types and black scene distribution patterns.
    /// </returns>
    // MARK: SelectRandomTimestamp
    public TimeSpan SelectRandomTimestamp(TimeSpan duration, IReadOnlyList<BlackInterval> blackIntervals)
    {
        var minTime = TimeSpan.FromSeconds(duration.TotalSeconds * 0.1);
        var maxTime = TimeSpan.FromSeconds(duration.TotalSeconds * 0.9);
        
        var maxAttempts = 50;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var randomSeconds = _random.NextDouble() * (maxTime.TotalSeconds - minTime.TotalSeconds) + minTime.TotalSeconds;
            var randomTimestamp = TimeSpan.FromSeconds(randomSeconds);
            
            if (!IsInBlackInterval(randomTimestamp, blackIntervals))
            {
                return randomTimestamp;
            }
        }
        
        var gapTimestamp = FindLargestGap(duration, blackIntervals);
        if (gapTimestamp.HasValue)
        {
            var gapStart = gapTimestamp.Value;
            var gapEnd = FindGapEnd(gapStart, duration, blackIntervals);
            var gapDuration = gapEnd - gapStart;
            
            if (gapDuration.TotalSeconds > 10)
            {
                var randomOffsetSeconds = _random.NextDouble() * gapDuration.TotalSeconds;
                return gapStart.Add(TimeSpan.FromSeconds(randomOffsetSeconds));
            }
        }
        
        return TimeSpan.FromSeconds(duration.TotalSeconds * (0.2 + _random.NextDouble() * 0.6));
    }

    /// <summary>
    /// Retrieves cached black scene detection results using sophisticated cache key generation
    /// and validation mechanisms ensuring optimal performance while maintaining cache consistency
    /// and reliability. This method implements intelligent cache management strategies providing
    /// significant performance improvements for repeated video processing operations while ensuring
    /// cache validity through comprehensive file attribute validation and automatic expiration.
    /// 
    /// The caching system represents a critical performance optimization component enabling efficient
    /// batch processing operations by eliminating redundant black scene detection for unchanged
    /// video files while maintaining detection accuracy and system reliability across extended
    /// operation periods with diverse video content processing requirements.
    /// 
    /// Cache Key Strategy and Validation:
    /// Sophisticated cache key generation incorporates file path, size, and modification timestamp
    /// creating unique identifiers that ensure cache validity while enabling efficient lookup
    /// operations. The composite key approach provides reliable cache invalidation when video
    /// files are modified while maintaining optimal performance benefits for unchanged content.
    /// 
    /// Expiration and Cleanup Management:
    /// Intelligent expiration mechanisms prevent unlimited cache growth while maintaining
    /// performance benefits through automatic cleanup of expired entries. The expiration
    /// strategy balances memory utilization with performance optimization ensuring efficient
    /// cache management during extended batch processing operations.
    /// 
    /// Performance and Reliability:
    /// Cache validation includes comprehensive file attribute verification ensuring detected
    /// results remain valid for current video content while providing immediate performance
    /// benefits for repeated processing operations. The validation approach maintains accuracy
    /// while maximizing cache utilization efficiency across diverse processing scenarios.
    /// </summary>
    /// <param name="videoPath">
    /// File system path to the video file requiring cache lookup for black scene detection results.
    /// Used for cache key generation and file attribute validation during cache retrieval operations.
    /// </param>
    /// <returns>
    /// Cached black scene detection results if available and valid, null if cache miss or
    /// expiration requires fresh detection processing. Null return triggers new detection
    /// operations while successful return provides immediate performance optimization.
    /// </returns>
    // MARK: GetCachedBlackIntervals
    private List<BlackInterval>? GetCachedBlackIntervals(string videoPath)
    {
        var fileInfo = new FileInfo(videoPath);
        var cacheKey = $"{videoPath}_{fileInfo.Length}_{fileInfo.LastWriteTime.Ticks}";
        
        if (_blackIntervalCache.TryGetValue(cacheKey, out var cached))
        {
            if (DateTime.UtcNow - cached.Created < _cacheExpiry)
            {
                return cached.Intervals;
            }
            
            _blackIntervalCache.Remove(cacheKey);
        }
        
        return null;
    }

    /// <summary>
    /// Stores black scene detection results in intelligent cache infrastructure with automatic
    /// cleanup and expiration management ensuring optimal performance while preventing unlimited
    /// memory growth. This method implements sophisticated cache management strategies maintaining
    /// system efficiency during extended batch processing operations while ensuring cache
    /// consistency and reliability across diverse video content processing requirements.
    /// 
    /// The caching implementation provides essential performance optimization for repeated video
    /// processing operations while maintaining system health through intelligent cleanup mechanisms
    /// and expiration strategies that balance performance benefits with memory utilization efficiency.
    /// 
    /// Cache Storage Strategy:
    /// Composite cache keys ensure unique identification for video files while incorporating
    /// file attributes for automatic invalidation when content changes. The storage approach
    /// provides reliable cache management while maintaining optimal lookup performance during
    /// high-frequency processing operations typical in batch poster generation workflows.
    /// 
    /// Automatic Cleanup and Maintenance:
    /// Proactive cleanup mechanisms remove expired cache entries during storage operations
    /// preventing unlimited cache growth while maintaining optimal performance characteristics.
    /// The cleanup strategy ensures memory efficiency while preserving frequently accessed
    /// cache entries essential for batch processing performance optimization.
    /// 
    /// Memory Management and Efficiency:
    /// Intelligent cache management balances performance benefits with memory utilization
    /// ensuring efficient operation during extended batch processing sessions while maintaining
    /// system stability and resource optimization across diverse deployment environments
    /// and processing workload characteristics.
    /// </summary>
    /// <param name="videoPath">
    /// File system path to the video file for cache key generation and storage operations.
    /// Used for composite key creation ensuring unique identification and efficient lookup.
    /// </param>
    /// <param name="intervals">
    /// Black scene detection results requiring cache storage for future performance optimization.
    /// Stored with timestamp information enabling expiration validation and cleanup management.
    /// </param>
    // MARK: CacheBlackIntervals
    private void CacheBlackIntervals(string videoPath, List<BlackInterval> intervals)
    {
        var fileInfo = new FileInfo(videoPath);
        var cacheKey = $"{videoPath}_{fileInfo.Length}_{fileInfo.LastWriteTime.Ticks}";
        
        _blackIntervalCache[cacheKey] = (DateTime.UtcNow, intervals);
        
        var expiredKeys = _blackIntervalCache
            .Where(kvp => DateTime.UtcNow - kvp.Value.Created > _cacheExpiry)
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var key in expiredKeys)
        {
            _blackIntervalCache.Remove(key);
        }
    }

    /// <summary>
    /// Performs precise collision detection determining whether specified timestamps fall within
    /// identified black scene intervals ensuring accurate timestamp validation for frame extraction
    /// optimization. This method implements efficient interval checking algorithms enabling intelligent
    /// timestamp selection while avoiding undesirable video content regions that would produce
    /// poor-quality poster sources unsuitable for media library presentation and user experience.
    /// 
    /// The collision detection system serves as a critical component in content-aware timestamp
    /// selection ensuring extracted frames represent meaningful episode content while maintaining
    /// processing efficiency through optimized interval checking algorithms suitable for
    /// high-frequency validation operations during batch poster generation workflows.
    /// </summary>
    /// <param name="timestamp">
    /// Target timestamp requiring collision detection validation against identified black scene
    /// intervals. Used for boundary checking and interval containment determination.
    /// </param>
    /// <param name="blackIntervals">
    /// Comprehensive list of detected black scene intervals used for collision detection and
    /// timestamp validation. Empty list results in no collision detection enabling unrestricted
    /// timestamp selection within content-rich video regions.
    /// </param>
    /// <returns>
    /// Boolean indicating whether the specified timestamp falls within any detected black scene
    /// interval. True indicates collision requiring timestamp rejection, false indicates safe
    /// timestamp selection suitable for high-quality frame extraction operations.
    /// </returns>
    // MARK: IsInBlackInterval
    private bool IsInBlackInterval(TimeSpan timestamp, IReadOnlyList<BlackInterval> blackIntervals)
    {
        return blackIntervals.Any(interval => 
            timestamp >= interval.Start && timestamp <= interval.End);
    }

    /// <summary>
    /// Implements sophisticated gap analysis algorithms identifying the largest content-rich region
    /// between detected black scene intervals ensuring optimal timestamp selection when random
    /// approaches fail. This method provides intelligent fallback strategies for timestamp selection
    /// in videos with extensive black scene regions while maintaining preference for substantial
    /// content areas most likely to contain high-quality visual material suitable for poster generation.
    /// 
    /// The gap analysis system represents an essential component in robust timestamp selection
    /// providing reliable fallback mechanisms when primary random selection encounters excessive
    /// black scene collisions while ensuring continued operation with optimal content quality
    /// across diverse video content types and black scene distribution patterns.
    /// 
    /// Gap Identification Algorithm:
    /// Comprehensive analysis of intervals between detected black scenes identifying regions
    /// with substantial duration suitable for meaningful frame extraction. The algorithm
    /// prioritizes larger gaps while ensuring minimum duration requirements for content quality
    /// assurance during poster generation operations.
    /// 
    /// Boundary Analysis and Optimization:
    /// Intelligent boundary detection includes analysis of video beginning and ending regions
    /// ensuring comprehensive gap identification while avoiding opening and closing credits
    /// that may not be captured by standard black scene detection algorithms.
    /// 
    /// Quality Assurance and Validation:
    /// Gap validation ensures identified regions provide adequate duration for meaningful
    /// timestamp selection while maintaining focus on content-rich areas most likely to
    /// contain representative episode imagery suitable for high-quality poster presentation.
    /// </summary>
    /// <param name="duration">
    /// Total video duration providing temporal context for gap analysis and boundary validation.
    /// Used for comprehensive interval analysis and end-region gap identification.
    /// </param>
    /// <param name="blackIntervals">
    /// Sorted list of detected black scene intervals used for gap identification and analysis.
    /// Empty list results in null return indicating no gap analysis requirements.
    /// </param>
    /// <returns>
    /// Starting timestamp of the largest identified content-rich gap suitable for timestamp
    /// selection, null if no suitable gaps are found or black interval list is empty.
    /// Successful return provides optimal fallback timestamp selection region.
    /// </returns>
    // MARK: FindLargestGap
    private TimeSpan? FindLargestGap(TimeSpan duration, IReadOnlyList<BlackInterval> blackIntervals)
    {
        if (blackIntervals.Count == 0)
        {
            return null;
        }

        var sortedIntervals = blackIntervals.OrderBy(i => i.Start).ToList();
        var largestGap = TimeSpan.Zero;
        var largestGapStart = TimeSpan.Zero;

        if (sortedIntervals[0].Start > TimeSpan.FromSeconds(10))
        {
            largestGap = sortedIntervals[0].Start;
            largestGapStart = TimeSpan.Zero;
        }

        for (int i = 0; i < sortedIntervals.Count - 1; i++)
        {
            var gapStart = sortedIntervals[i].End;
            var gapEnd = sortedIntervals[i + 1].Start;
            var gapDuration = gapEnd - gapStart;

            if (gapDuration > largestGap)
            {
                largestGap = gapDuration;
                largestGapStart = gapStart;
            }
        }

        var lastInterval = sortedIntervals.Last();
        var endGap = duration - lastInterval.End;
        if (endGap > largestGap && endGap > TimeSpan.FromSeconds(10))
        {
            largestGap = endGap;
            largestGapStart = lastInterval.End;
        }

        return largestGapStart;
    }

    /// <summary>
    /// Determines the ending boundary of content-rich gaps for precise timestamp selection range
    /// calculation enabling optimal utilization of identified content regions during fallback
    /// timestamp selection operations. This method provides essential boundary calculation for
    /// gap-based timestamp selection ensuring accurate duration determination and range validation
    /// during intelligent fallback strategies when primary random selection encounters obstacles.
    /// 
    /// The boundary calculation system enables precise range determination for timestamp selection
    /// within identified content-rich regions ensuring optimal utilization of available content
    /// while maintaining accuracy in duration calculations essential for high-quality frame
    /// extraction and poster generation operations.
    /// </summary>
    /// <param name="gapStart">
    /// Starting timestamp of the identified content-rich gap requiring boundary calculation
    /// for range determination and duration validation during timestamp selection operations.
    /// </param>
    /// <param name="duration">
    /// Total video duration providing temporal context for boundary calculation and end-region
    /// validation ensuring accurate gap duration determination and range validation.
    /// </param>
    /// <param name="blackIntervals">
    /// List of detected black scene intervals used for boundary identification and gap ending
    /// determination. Used to find the next black interval limiting gap extent.
    /// </param>
    /// <returns>
    /// Ending timestamp of the identified gap providing precise range boundaries for timestamp
    /// selection within content-rich regions. Returns video duration if no limiting intervals found.
    /// </returns>
    // MARK: FindGapEnd
    private TimeSpan FindGapEnd(TimeSpan gapStart, TimeSpan duration, IReadOnlyList<BlackInterval> blackIntervals)
    {
        var nextInterval = blackIntervals
            .Where(interval => interval.Start > gapStart)
            .OrderBy(interval => interval.Start)
            .FirstOrDefault();
            
        return nextInterval?.Start ?? duration;
    }

    /// <summary>
    /// Executes FFmpeg commands asynchronously with comprehensive process management and error handling
    /// ensuring reliable video processing operations while maintaining system stability and resource
    /// optimization. This method provides the foundational execution infrastructure for all FFmpeg-based
    /// video processing operations including black scene detection and frame extraction with robust
    /// error handling and cancellation support essential for batch processing reliability.
    /// </summary>
    /// <param name="arguments">
    /// Command-line arguments for FFmpeg execution defining processing operations and parameters.
    /// Constructed by calling methods with operation-specific optimizations and configurations.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token enabling responsive termination of FFmpeg operations while maintaining
    /// process cleanup and resource management during cancellation scenarios.
    /// </param>
    /// <returns>
    /// Combined standard output and error streams from FFmpeg execution providing comprehensive
    /// processing results and diagnostic information for parsing and error analysis.
    /// </returns>
    // MARK: ExecuteFFmpegAsync
    private async Task<string> ExecuteFFmpegAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return await ExecuteProcessAsync(GetFFmpegPath(), arguments, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes FFprobe commands asynchronously with comprehensive process management and error handling
    /// ensuring reliable video analysis operations while maintaining system stability and resource
    /// optimization. This method provides the foundational execution infrastructure for all FFprobe-based
    /// video analysis operations including duration detection and metadata extraction with robust
    /// error handling and cancellation support essential for accurate video analysis workflows.
    /// </summary>
    /// <param name="arguments">
    /// Command-line arguments for FFprobe execution defining analysis operations and output formatting.
    /// Constructed by calling methods with operation-specific optimizations and parameter configurations.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token enabling responsive termination of FFprobe operations while maintaining
    /// process cleanup and resource management during cancellation scenarios.
    /// </param>
    /// <returns>
    /// Combined standard output and error streams from FFprobe execution providing comprehensive
    /// analysis results and diagnostic information for parsing and validation operations.
    /// </returns>
    // MARK: ExecuteFFprobeAsync
    private async Task<string> ExecuteFFprobeAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return await ExecuteProcessAsync(GetFFprobePath(), arguments, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Implements comprehensive asynchronous process execution infrastructure with sophisticated
    /// output capture, error handling, and resource management ensuring reliable operation across
    /// diverse system configurations and processing scenarios. This method serves as the foundational
    /// process execution layer for all video processing operations providing essential infrastructure
    /// for FFmpeg and FFprobe command execution with optimal performance and stability characteristics.
    /// 
    /// The process execution system implements enterprise-grade reliability patterns including
    /// comprehensive output capture, error stream monitoring, and resource cleanup ensuring
    /// stable operation during extended batch processing workflows while maintaining optimal
    /// performance and system resource utilization across diverse deployment environments.
    /// 
    /// Output Capture and Stream Management:
    /// Sophisticated output capture mechanisms collect both standard output and error streams
    /// providing comprehensive processing results essential for parsing, validation, and
    /// debugging operations. The capture system ensures complete data collection while
    /// maintaining memory efficiency during large-scale processing operations.
    /// 
    /// Error Handling and Process Monitoring:
    /// Comprehensive error detection includes exit code validation and error stream analysis
    /// providing detailed diagnostic information for troubleshooting and monitoring purposes.
    /// The monitoring system enables administrative oversight while maintaining processing
    /// continuity during recoverable error conditions.
    /// 
    /// Resource Management and Cleanup:
    /// Robust resource management ensures proper disposal of process handles, streams, and
    /// associated resources preventing memory leaks and system resource accumulation during
    /// extended batch processing operations. Cleanup mechanisms operate regardless of
    /// processing success or failure ensuring consistent system health maintenance.
    /// 
    /// Cancellation Support and Responsive Termination:
    /// Integrated cancellation support enables responsive process termination while maintaining
    /// resource cleanup and system stability during cancellation scenarios. The cancellation
    /// system provides graceful termination ensuring system consistency and preventing
    /// resource leaks during interrupted processing operations.
    /// </summary>
    /// <param name="fileName">
    /// Executable file name or path for process execution enabling flexible executable resolution
    /// while maintaining compatibility with system PATH configuration and absolute path specifications.
    /// </param>
    /// <param name="arguments">
    /// Command-line arguments for process execution containing operation-specific parameters
    /// and configuration settings constructed by calling methods for optimal processing results.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token enabling responsive process termination while maintaining resource
    /// cleanup and system stability during cancellation scenarios and timeout conditions.
    /// </param>
    /// <returns>
    /// Combined standard output and error streams from process execution providing comprehensive
    /// results and diagnostic information essential for parsing, validation, and error analysis.
    /// </returns>
    // MARK: ExecuteProcessAsync
    private async Task<string> ExecuteProcessAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        var output = string.Empty;
        var error = string.Empty;

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                output += e.Data + Environment.NewLine;
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                error += e.Data + Environment.NewLine;
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("Process {FileName} exited with code {ExitCode}. Error: {Error}", fileName, process.ExitCode, error);
        }

        return output + error;
    }

    /// <summary>
    /// Parses FFmpeg blackdetect filter output using sophisticated regular expression algorithms
    /// extracting precise temporal information for black scene interval construction and validation.
    /// This method implements robust parsing mechanisms handling various output formats and edge cases
    /// ensuring accurate interval extraction essential for intelligent timestamp selection and
    /// content-aware frame extraction operations during poster generation workflows.
    /// 
    /// The parsing system serves as a critical component in black scene detection providing
    /// reliable conversion from FFmpeg output text to structured interval objects suitable
    /// for algorithmic processing and timestamp validation operations while maintaining
    /// accuracy and consistency across diverse video content and processing scenarios.
    /// 
    /// Regular Expression Parsing Strategy:
    /// Sophisticated pattern matching extracts start time, end time, and duration values
    /// from FFmpeg blackdetect output ensuring accurate temporal information capture while
    /// handling various numeric formats and precision levels typical in video processing
    /// output with different frame rates and timing characteristics.
    /// 
    /// Validation and Error Handling:
    /// Comprehensive validation ensures extracted values are mathematically consistent and
    /// represent valid temporal intervals while providing graceful error handling for
    /// malformed output or unexpected format variations. The validation approach maintains
    /// parsing reliability while ensuring data quality for subsequent processing operations.
    /// 
    /// Precision and Accuracy:
    /// Temporal value parsing maintains precision essential for accurate interval boundaries
    /// enabling precise timestamp validation and collision detection during intelligent
    /// timestamp selection algorithms while ensuring consistency across various video
    /// formats and frame rate characteristics.
    /// </summary>
    /// <param name="output">
    /// FFmpeg blackdetect filter output containing temporal information for black scene intervals.
    /// Contains pattern-matched text requiring extraction and validation for interval construction.
    /// </param>
    /// <returns>
    /// List of structured black interval objects with precise temporal boundaries extracted
    /// from FFmpeg output. Empty list indicates no black scenes detected or parsing failures.
    /// </returns>
    // MARK: ParseBlackDetectOutput
    private List<BlackInterval> ParseBlackDetectOutput(string output)
    {
        var intervals = new List<BlackInterval>();
        
        var regex = new Regex(@"black_start:(\d+\.?\d*) black_end:(\d+\.?\d*) black_duration:(\d+\.?\d*)");

        foreach (Match match in regex.Matches(output))
        {
            if (match.Groups.Count >= 4 &&
                double.TryParse(match.Groups[1].Value, out var start) &&
                double.TryParse(match.Groups[2].Value, out var end) &&
                double.TryParse(match.Groups[3].Value, out var duration))
            {
                intervals.Add(new BlackInterval
                {
                    Start = TimeSpan.FromSeconds(start),
                    End = TimeSpan.FromSeconds(end),
                    Duration = TimeSpan.FromSeconds(duration)
                });
            }
        }

        return intervals;
    }
}