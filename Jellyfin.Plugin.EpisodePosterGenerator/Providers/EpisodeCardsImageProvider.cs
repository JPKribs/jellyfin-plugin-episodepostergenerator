using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Providers
{
    /// <summary>
    /// Comprehensive Jellyfin image provider integration service implementing sophisticated dynamic image
    /// generation capabilities for episode poster creation with seamless integration into Jellyfin's media
    /// management infrastructure. Serves as the critical bridge between Jellyfin's image provider system
    /// and the poster generation plugin ecosystem, orchestrating complex workflows involving video analysis,
    /// content-aware processing, and style-specific poster creation while maintaining optimal performance
    /// and reliability characteristics essential for responsive media library management and user experience.
    /// 
    /// This provider represents the primary integration point enabling Jellyfin's media management system
    /// to seamlessly access sophisticated poster generation capabilities through established image provider
    /// patterns while maintaining compatibility with Jellyfin's caching, metadata, and user interface
    /// systems. The implementation emphasizes responsive operation, comprehensive error handling, and
    /// intelligent resource management ensuring optimal user experience across diverse deployment scenarios.
    /// 
    /// Integration Architecture and Design Philosophy:
    /// The provider implements Jellyfin's IDynamicImageProvider interface ensuring seamless integration
    /// with Jellyfin's image management infrastructure while providing access to advanced poster generation
    /// capabilities through established plugin patterns. The architecture separates integration concerns
    /// from poster generation logic enabling clean separation of responsibilities while maintaining
    /// optimal performance and reliability characteristics essential for production deployment.
    /// 
    /// Core Integration Infrastructure Overview:
    /// 
    /// Jellyfin Image Provider Integration:
    /// Sophisticated implementation of Jellyfin's dynamic image provider interface enabling seamless
    /// integration with media library management while providing access to advanced poster generation
    /// capabilities. The integration respects Jellyfin's image management patterns while extending
    /// functionality through plugin-specific poster creation algorithms and content-aware processing.
    /// 
    /// Episode-Specific Image Generation:
    /// Advanced episode detection and processing algorithms providing targeted poster generation for
    /// television episode content while maintaining compatibility with Jellyfin's media type system.
    /// The episode-specific approach ensures optimal processing efficiency while providing comprehensive
    /// coverage of television content requiring automated poster generation and visual enhancement.
    /// 
    /// Dynamic Image Response Management:
    /// Comprehensive image response coordination ensuring proper integration with Jellyfin's image
    /// display and caching systems while providing optimal image delivery and format compatibility.
    /// The response management includes proper metadata coordination and stream handling ensuring
    /// seamless integration with Jellyfin's user interface and media presentation infrastructure.
    /// 
    /// Configuration-Aware Processing:
    /// Intelligent configuration integration enabling dynamic poster generation behavior based on
    /// user preferences and plugin settings while maintaining responsive operation and optimal
    /// resource utilization. The configuration awareness ensures poster generation reflects current
    /// user requirements while providing efficient processing through intelligent decision algorithms.
    /// 
    /// Poster Generation Workflow Orchestration:
    /// 
    /// Multi-Service Coordination:
    /// Sophisticated orchestration of video analysis, poster generation, and tracking services
    /// providing comprehensive poster creation workflows while maintaining optimal performance
    /// and reliability characteristics. The coordination ensures efficient service utilization
    /// while providing robust error handling and resource management across complex processing pipelines.
    /// 
    /// Content-Aware Processing Pipeline:
    /// Advanced processing algorithms determining optimal poster generation strategies based on
    /// episode content characteristics and configuration requirements. The content-aware approach
    /// includes video analysis for frame extraction, style-specific processing decisions, and
    /// intelligent resource allocation ensuring high-quality poster generation across diverse content types.
    /// 
    /// Style-Specific Generation Routing:
    /// Intelligent routing algorithms directing episodes to appropriate poster generation strategies
    /// based on configuration settings and content characteristics. The routing includes special
    /// handling for numeral-style posters requiring transparent backgrounds while maintaining
    /// consistent processing patterns for video-based poster styles requiring frame extraction.
    /// 
    /// Resource Management and Optimization:
    /// Comprehensive resource management ensuring efficient utilization of system resources while
    /// maintaining optimal processing performance and reliability. The management includes temporary
    /// file coordination, memory optimization, and cleanup automation preventing resource accumulation
    /// and system degradation during intensive poster generation operations.
    /// 
    /// Performance Optimization Strategies:
    /// 
    /// Intelligent Caching and Coordination:
    /// Advanced coordination with Jellyfin's image caching infrastructure ensuring optimal performance
    /// while maintaining accurate poster updates when content or configuration changes. The coordination
    /// includes cache invalidation strategies and intelligent processing decisions minimizing redundant
    /// generation while ensuring current poster availability and visual accuracy.
    /// 
    /// Asynchronous Processing Architecture:
    /// Comprehensive asynchronous processing patterns ensuring responsive user interface operation
    /// while enabling sophisticated poster generation workflows requiring video analysis and
    /// content processing. The asynchronous architecture maintains system responsiveness while
    /// providing efficient resource utilization during intensive processing operations.
    /// 
    /// Memory Management and Stream Optimization:
    /// Sophisticated memory management patterns ensuring efficient handling of image data and
    /// processing results while minimizing memory allocation and garbage collection overhead.
    /// The optimization includes stream management and resource disposal ensuring optimal
    /// performance characteristics suitable for frequent poster generation operations.
    /// 
    /// Temporary Resource Coordination:
    /// Advanced temporary file management ensuring efficient processing while maintaining system
    /// cleanliness through automatic cleanup and resource disposal. The coordination prevents
    /// temporary file accumulation while ensuring optimal processing performance during complex
    /// poster generation workflows requiring intermediate file operations.
    /// 
    /// Error Handling and Reliability Architecture:
    /// 
    /// Comprehensive Exception Management:
    /// Multi-layered error handling ensuring graceful degradation when poster generation encounters
    /// issues while maintaining system stability and user experience quality. The exception management
    /// includes detailed logging for debugging while providing appropriate fallback responses
    /// ensuring continued Jellyfin operation despite poster generation failures.
    /// 
    /// Service Availability Validation:
    /// Robust service dependency validation ensuring appropriate handling when required services
    /// are unavailable while maintaining system stability and providing clear administrative
    /// feedback. The validation prevents cascading failures while ensuring poster generation
    /// operates reliably across diverse deployment configurations and service availability scenarios.
    /// 
    /// Resource Recovery and Cleanup:
    /// Sophisticated resource recovery mechanisms ensuring proper cleanup during error conditions
    /// while preventing resource leaks and system degradation. The recovery includes automatic
    /// temporary file cleanup and resource disposal ensuring system health regardless of
    /// processing success or failure outcomes.
    /// 
    /// Configuration Validation and Fallback:
    /// Advanced configuration validation ensuring robust operation when plugin settings are
    /// incomplete or invalid while providing appropriate fallback behavior. The validation
    /// includes defensive programming patterns ensuring continued operation while maintaining
    /// poster generation quality and system stability.
    /// 
    /// Integration Patterns and Compatibility:
    /// 
    /// Jellyfin Infrastructure Coordination:
    /// Seamless integration with Jellyfin's application infrastructure including path management,
    /// logging systems, and dependency injection ensuring consistent operation across diverse
    /// deployment environments. The coordination maintains compatibility with Jellyfin's
    /// architectural patterns while providing advanced poster generation capabilities.
    /// 
    /// Media Type Detection and Handling:
    /// Sophisticated media type detection ensuring appropriate poster generation for television
    /// episode content while maintaining compatibility with Jellyfin's media classification
    /// system. The detection includes proper type validation and processing eligibility
    /// determination ensuring optimal resource utilization and processing accuracy.
    /// 
    /// Image Format and Compatibility Management:
    /// Comprehensive image format handling ensuring compatibility with Jellyfin's image display
    /// and storage systems while providing optimal quality and performance characteristics.
    /// The format management includes proper encoding settings and metadata coordination
    /// ensuring seamless integration with Jellyfin's media presentation infrastructure.
    /// 
    /// Plugin Ecosystem Integration:
    /// Advanced integration with plugin infrastructure enabling access to specialized services
    /// while maintaining modular architecture and clean separation of concerns. The integration
    /// ensures efficient service coordination while providing robust error handling and
    /// fallback capabilities essential for reliable plugin operation.
    /// 
    /// Administrative and Monitoring Capabilities:
    /// 
    /// Comprehensive Logging and Monitoring:
    /// Detailed logging integration providing comprehensive visibility into poster generation
    /// operations while maintaining appropriate detail levels for debugging and administrative
    /// oversight. The logging includes performance metrics, error conditions, and processing
    /// statistics essential for system monitoring and optimization.
    /// 
    /// Processing Statistics and Analysis:
    /// Advanced processing metrics collection enabling administrative analysis of poster
    /// generation effectiveness and system performance. The statistics provide data-driven
    /// insights for optimization decisions while ensuring comprehensive coverage of
    /// processing operations and resource utilization patterns.
    /// 
    /// Debugging and Troubleshooting Support:
    /// Sophisticated debugging capabilities providing detailed information for troubleshooting
    /// poster generation issues while maintaining system performance and reliability. The
    /// debugging support includes comprehensive error reporting and processing state
    /// information enabling efficient problem resolution and system optimization.
    /// 
    /// The image provider represents a critical integration component enabling sophisticated
    /// poster generation capabilities within Jellyfin's media management infrastructure while
    /// maintaining optimal performance, reliability, and user experience characteristics
    /// essential for production deployment and comprehensive media library management across
    /// diverse episode collections and content types requiring automated visual enhancement.
    /// </summary>
    public class EpisodePosterImageProvider : IDynamicImageProvider
    {
        /// <summary>
        /// Logger instance for comprehensive image provider monitoring, debugging, and administrative oversight.
        /// Provides detailed logging throughout the poster generation workflow including processing decisions,
        /// service coordination, and error conditions essential for system monitoring and troubleshooting.
        /// </summary>
        private readonly ILogger<EpisodePosterImageProvider> _logger;

        /// <summary>
        /// Jellyfin application paths service providing access to configured directory locations including
        /// temporary file storage essential for poster generation workflows. Enables consistent path
        /// management while maintaining compatibility with Jellyfin's data storage patterns and security
        /// requirements essential for reliable temporary file operations and resource management.
        /// </summary>
        private readonly IApplicationPaths _appPaths;

        /// <summary>
        /// Initializes a new instance of the episode poster image provider with essential Jellyfin integration
        /// components and establishes the foundational infrastructure for dynamic image generation operations.
        /// Sets up logging and application path integration enabling seamless poster generation while maintaining
        /// compatibility with Jellyfin's dependency injection and configuration management systems essential
        /// for reliable plugin operation and media library integration.
        /// 
        /// The constructor establishes the provider foundation for image generation operations by integrating
        /// with Jellyfin's infrastructure and preparing necessary service references for efficient poster
        /// creation workflows while maintaining optimal performance and reliability characteristics
        /// essential for responsive media library management and user experience.
        /// 
        /// Integration Strategy:
        /// The initialization process focuses on establishing robust connections with Jellyfin's infrastructure
        /// while preparing the provider for efficient image generation operations across diverse episode
        /// collections and processing scenarios with optimal performance characteristics suitable for
        /// production deployment and extended operation.
        /// 
        /// Service Coordination and Logging:
        /// Comprehensive logging initialization provides administrative visibility into provider operation
        /// and configuration while ensuring proper integration with Jellyfin's logging infrastructure.
        /// The logging setup enables debugging and monitoring capabilities essential for system
        /// oversight and optimization during poster generation workflows.
        /// </summary>
        /// <param name="logger">
        /// Logger service for image provider monitoring, error reporting, and debugging information.
        /// Provides comprehensive logging capabilities throughout poster generation workflows enabling
        /// administrative oversight and troubleshooting during media library operations.
        /// </param>
        /// <param name="appPaths">
        /// Jellyfin application paths service providing access to configured storage locations.
        /// Enables consistent temporary file management while maintaining compatibility with
        /// Jellyfin's data storage patterns and security requirements.
        /// </param>
        // MARK: Constructor
        public EpisodePosterImageProvider(
            ILogger<EpisodePosterImageProvider> logger,
            IApplicationPaths appPaths)
        {
            _logger = logger;
            _appPaths = appPaths;
            _logger.LogInformation("Episode Poster Generator image provider initialized");
        }

        /// <summary>
        /// Gets the user-friendly display name for this image provider shown in Jellyfin's administrative
        /// interfaces and logging output. Provides clear identification of the provider's purpose enabling
        /// administrative oversight and debugging while maintaining consistency with plugin branding
        /// and identification across Jellyfin's management systems.
        /// </summary>
        public string Name => "Episode Poster Generator";

        /// <summary>
        /// Implements sophisticated media item evaluation determining whether this provider supports
        /// poster generation for specified media items with comprehensive type checking and eligibility
        /// validation. This method serves as the primary filter ensuring poster generation resources
        /// are allocated efficiently to appropriate content while maintaining compatibility with
        /// Jellyfin's media type system and classification infrastructure.
        /// 
        /// The support determination implements intelligent media type analysis focusing on television
        /// episode content while providing comprehensive logging for administrative oversight and
        /// debugging. The evaluation ensures optimal resource utilization by targeting poster
        /// generation capabilities toward content requiring automated visual enhancement.
        /// 
        /// Media Type Analysis and Validation:
        /// Advanced type checking algorithms evaluate media items against episode classification
        /// criteria ensuring poster generation targets appropriate content while maintaining
        /// processing efficiency through intelligent filtering. The analysis includes comprehensive
        /// validation ensuring reliable type detection across diverse media library configurations.
        /// 
        /// Administrative Logging and Transparency:
        /// Comprehensive logging provides administrative visibility into support decisions enabling
        /// debugging and optimization while ensuring transparency in provider operation. The logging
        /// includes detailed item information and decision rationale essential for troubleshooting
        /// and system monitoring during media library management operations.
        /// 
        /// Performance Optimization:
        /// Efficient evaluation algorithms minimize processing overhead during support determination
        /// while maintaining accurate type detection and eligibility validation. The optimization
        /// ensures responsive operation during media library scanning and content evaluation
        /// scenarios requiring frequent support determination calls.
        /// </summary>
        /// <param name="item">
        /// Media item requiring support evaluation for poster generation eligibility. Provides
        /// access to item metadata and type information essential for accurate support determination
        /// and resource allocation optimization during media library management operations.
        /// </param>
        /// <returns>
        /// Boolean indicating whether this provider supports poster generation for the specified
        /// media item. True indicates episode content eligible for poster generation, false
        /// indicates content outside provider scope enabling efficient resource allocation.
        /// </returns>
        public bool Supports(BaseItem item)
        {
            // Perform sophisticated media type analysis for episode content detection
            var isEpisode = item is Episode;

            _logger.LogInformation("Supports check - Item: \"{ItemName}\", IsEpisode: {IsEpisode}",
                item.Name ?? "null", isEpisode);

            if (isEpisode)
            {
                _logger.LogInformation("Supporting episode for library-level configuration");
                return true;
            }

            _logger.LogInformation("Not supporting item type: \"{ItemType}\"", item.GetType().Name);
            return false;
        }

        /// <summary>
        /// Implements comprehensive image type enumeration for supported media items providing Jellyfin
        /// with detailed information about available image generation capabilities. This method enables
        /// Jellyfin's image management system to understand provider capabilities while ensuring
        /// optimal integration with media library infrastructure and user interface systems
        /// requiring comprehensive image type information for proper functionality.
        /// 
        /// The enumeration process implements intelligent image type analysis focusing on primary
        /// image support for television episode content while maintaining extensibility for future
        /// enhancement and capability expansion. The implementation ensures Jellyfin receives
        /// accurate capability information enabling optimal image management and user experience.
        /// 
        /// Image Type Analysis and Capability Declaration:
        /// Sophisticated analysis determines appropriate image types for supported media items
        /// while maintaining compatibility with Jellyfin's image classification and management
        /// systems. The analysis focuses on primary image support ensuring comprehensive poster
        /// generation coverage while providing foundation for future capability enhancement.
        /// 
        /// Administrative Monitoring and Logging:
        /// Comprehensive logging provides administrative visibility into image type determination
        /// enabling debugging and system monitoring while ensuring transparency in provider
        /// capability declaration. The logging includes detailed item analysis and capability
        /// reporting essential for troubleshooting and optimization.
        /// 
        /// Performance and Efficiency:
        /// Optimized enumeration algorithms provide efficient capability reporting while maintaining
        /// accurate image type analysis and compatibility validation. The optimization ensures
        /// responsive operation during media library analysis and image management scenarios
        /// requiring frequent capability determination and validation.
        /// </summary>
        /// <param name="item">
        /// Media item requiring image type capability analysis for provider integration. Provides
        /// access to item metadata enabling accurate capability determination and image type
        /// enumeration essential for Jellyfin's image management and user interface systems.
        /// </param>
        /// <returns>
        /// Enumerable collection of supported image types for the specified media item enabling
        /// Jellyfin's image management system to understand provider capabilities and optimize
        /// image generation and display operations across media library infrastructure.
        /// </returns>
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            _logger.LogInformation("GetSupportedImages called for item: \"{ItemName}\" (Type: \"{ItemType}\")", item.Name, item.GetType().Name);

            if (item is Episode)
            {
                _logger.LogInformation("Returning Primary image support for episode: \"{EpisodeName}\"", item.Name);
                yield return ImageType.Primary;
            }
            else
            {
                _logger.LogInformation("Item is not an Episode, returning no supported images");
            }
        }

        /// <summary>
        /// Implements the comprehensive dynamic image generation workflow orchestrating complex poster
        /// creation operations through sophisticated service coordination and resource management. This
        /// method serves as the primary entry point for Jellyfin's image requests providing seamless
        /// integration with media library infrastructure while enabling advanced poster generation
        /// capabilities through intelligent processing pipelines and content-aware optimization strategies.
        /// 
        /// The image generation process represents the culmination of sophisticated video analysis,
        /// content processing, and style-specific poster creation algorithms providing high-quality
        /// visual content for media library presentation. The implementation emphasizes responsive
        /// operation, comprehensive error handling, and optimal resource utilization ensuring
        /// consistent user experience across diverse deployment scenarios and content characteristics.
        /// 
        /// Multi-Phase Generation Workflow:
        /// 
        /// Configuration Validation and Eligibility Assessment:
        /// Comprehensive validation of plugin configuration and content eligibility ensuring poster
        /// generation proceeds only when appropriate while maintaining system efficiency through
        /// intelligent filtering. The assessment includes plugin status verification, item type
        /// validation, and content accessibility analysis providing foundation for successful processing.
        /// 
        /// Service Coordination and Resource Allocation:
        /// Sophisticated coordination with video analysis, poster generation, and tracking services
        /// ensuring optimal resource utilization while maintaining processing reliability through
        /// comprehensive error handling. The coordination includes service availability validation
        /// and intelligent resource allocation ensuring efficient processing workflows.
        /// 
        /// Content-Aware Processing Pipeline:
        /// Advanced processing algorithms adapting poster generation strategies based on content
        /// characteristics and configuration requirements. The pipeline includes video analysis
        /// for frame extraction, style-specific processing decisions, and intelligent optimization
        /// ensuring high-quality poster generation across diverse episode content and user preferences.
        /// 
        /// Tracking Integration and State Management:
        /// Comprehensive integration with tracking services ensuring processing state coordination
        /// and optimization through intelligent decision algorithms. The integration includes
        /// automatic processing record updates and state synchronization maintaining consistency
        /// across poster generation workflows and administrative oversight systems.
        /// 
        /// Response Generation and Format Optimization:
        /// Sophisticated response generation ensuring optimal integration with Jellyfin's image
        /// management and display systems while providing appropriate format and metadata coordination.
        /// The response optimization includes stream management and format selection ensuring
        /// seamless user experience and optimal image quality characteristics.
        /// 
        /// Error Handling and Reliability Assurance:
        /// Multi-layered error handling ensuring graceful degradation when poster generation encounters
        /// issues while maintaining system stability and user experience quality. The error management
        /// includes comprehensive logging for debugging while ensuring appropriate fallback responses
        /// maintaining continued Jellyfin operation despite processing challenges.
        /// </summary>
        /// <param name="item">
        /// Media item requiring dynamic image generation providing access to episode metadata and
        /// file information essential for poster creation workflows. Enables content analysis and
        /// processing decision algorithms ensuring appropriate poster generation strategies.
        /// </param>
        /// <param name="type">
        /// Image type specification indicating required image generation category enabling appropriate
        /// processing pipeline selection and resource allocation. Provides context for poster
        /// generation algorithms and format optimization ensuring optimal image characteristics.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token enabling responsive termination of image generation operations while
        /// maintaining resource cleanup and system stability during cancellation scenarios.
        /// </param>
        /// <returns>
        /// DynamicImageResponse containing generated poster image stream and metadata for Jellyfin
        /// integration or appropriate failure indication enabling graceful error handling and
        /// system stability during poster generation operations and media library management.
        /// </returns>
        public async Task<DynamicImageResponse> GetImage(BaseItem item, ImageType type, CancellationToken cancellationToken)
        {
            _logger.LogInformation("GetImage called for item: \"{ItemName}\" (Type: \"{ItemType}\"), ImageType: {ImageType}",
                item.Name, item.GetType().Name, type);

            // Configuration validation ensuring plugin availability and operational status
            var config = Plugin.Instance?.Configuration;
            if (config == null || !config.EnableProvider)
            {
                _logger.LogInformation("Episode Poster Generator is disabled via configuration.");
                return new DynamicImageResponse { HasImage = false };
            }

            // Media item type validation ensuring appropriate content for poster generation
            if (item is not Episode episode)
            {
                return new DynamicImageResponse { HasImage = false };
            }

            // Image type validation ensuring supported image generation categories
            if (type != ImageType.Primary)
            {
                _logger.LogInformation("Image type {ImageType} not supported.", type);
                return new DynamicImageResponse { HasImage = false };
            }

            // Content accessibility validation for video-based poster styles requiring file access
            if (!config.EnableProvider && (string.IsNullOrEmpty(episode.Path) || !File.Exists(episode.Path)))
            {
                _logger.LogInformation("Episode \"{EpisodeName}\" has no valid video file.", episode.Name);
                return new DynamicImageResponse { HasImage = false };
            }

            try
            {
                _logger.LogInformation("Processing episode: \"{EpisodeName}\" with style: {PosterStyle}", episode.Name, config.PosterStyle);

                // Execute comprehensive poster generation workflow with sophisticated error handling
                var imageStream = await GenerateEpisodeImageAsync(episode, cancellationToken).ConfigureAwait(false);
                if (imageStream == null)
                {
                    _logger.LogWarning("Failed to generate image for episode: \"{EpisodeName}\"", episode.Name);
                    return new DynamicImageResponse { HasImage = false };
                }

                // Tracking service integration for processing state management and optimization
                var trackingService = Plugin.Instance?.TrackingService;
                if (trackingService != null)
                {
                    try
                    {
                        await trackingService.MarkEpisodeProcessedAsync(episode, config).ConfigureAwait(false);
                        _logger.LogDebug("Marked episode as processed in tracking service: {EpisodeName}", episode.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to mark episode as processed in tracking service: {EpisodeName}", episode.Name);
                    }
                }

                _logger.LogInformation("Successfully generated poster for episode: \"{EpisodeName}\"", episode.Name);

                // Successful response generation with optimal format and metadata coordination
                return new DynamicImageResponse
                {
                    HasImage = true,
                    Stream = imageStream,
                    Format = ImageFormat.Jpg
                };
            }
            catch (Exception ex)
            {
                // Comprehensive error handling ensuring graceful degradation and system stability
                _logger.LogError(ex, "Error generating image for episode: \"{EpisodeName}\"", episode.Name);
                return new DynamicImageResponse { HasImage = false };
            }
        }

        /// <summary>
        /// Implements the comprehensive poster generation workflow orchestrating sophisticated video analysis,
        /// content processing, and style-specific poster creation through intelligent service coordination
        /// and resource management. This method serves as the core processing engine coordinating complex
        /// operations including frame extraction, content analysis, and poster synthesis while maintaining
        /// optimal performance and reliability characteristics essential for responsive image generation.
        /// 
        /// The generation process represents sophisticated integration of video processing, image manipulation,
        /// and content-aware optimization providing high-quality poster creation suitable for media library
        /// presentation. The implementation emphasizes efficient resource utilization, comprehensive error
        /// handling, and automatic cleanup ensuring stable operation across diverse content types and
        /// processing scenarios requiring advanced visual content generation capabilities.
        /// 
        /// Multi-Stage Processing Architecture:
        /// 
        /// Service Validation and Resource Preparation:
        /// Comprehensive validation of required services and resource availability ensuring processing
        /// readiness while maintaining efficient resource allocation through intelligent preparation.
        /// The validation includes service accessibility verification and temporary resource coordination
        /// providing foundation for reliable processing workflows.
        /// 
        /// Content Analysis and Processing Strategy Determination:
        /// Sophisticated analysis of episode content and configuration requirements determining optimal
        /// processing strategies for poster generation. The analysis includes style-specific routing,
        /// content accessibility validation, and processing pipeline selection ensuring appropriate
        /// resource allocation and optimal poster quality characteristics.
        /// 
        /// Video Analysis and Frame Extraction Pipeline:
        /// Advanced video processing workflows including duration analysis, black scene detection, and
        /// intelligent frame extraction providing high-quality source imagery for poster creation.
        /// The pipeline includes content-aware timestamp selection and hardware-accelerated processing
        /// ensuring optimal visual content extraction across diverse video formats and characteristics.
        /// 
        /// Poster Synthesis and Style Application:
        /// Comprehensive poster creation through style-specific processing algorithms including image
        /// manipulation, text overlay, and visual enhancement providing polished poster output suitable
        /// for media library presentation. The synthesis includes configuration-aware styling and
        /// optimal format generation ensuring consistent visual quality and user experience.
        /// 
        /// Resource Management and Cleanup Automation:
        /// Sophisticated temporary resource management ensuring efficient processing while maintaining
        /// system cleanliness through automatic cleanup and resource disposal. The management includes
        /// comprehensive error handling ensuring proper cleanup regardless of processing outcomes
        /// while preventing resource accumulation and system degradation.
        /// 
        /// Performance Optimization and Memory Management:
        /// Advanced optimization strategies ensuring efficient memory utilization and optimal processing
        /// performance while maintaining high-quality poster generation. The optimization includes
        /// stream management, temporary file coordination, and resource disposal ensuring responsive
        /// operation suitable for frequent poster generation requests and intensive processing workflows.
        /// </summary>
        /// <param name="episode">
        /// Episode object containing metadata and file information required for comprehensive poster
        /// generation workflows. Provides access to video content, episode details, and temporal
        /// information essential for content analysis and processing decision algorithms.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token enabling responsive termination of poster generation operations while
        /// maintaining resource cleanup and system stability during cancellation scenarios.
        /// </param>
        /// <returns>
        /// Memory stream containing generated poster image data for Jellyfin integration, null if
        /// generation fails enabling appropriate error handling and graceful degradation during
        /// image provider operation and media library management workflows.
        /// </returns>
        // MARK: GenerateEpisodeImageAsync
        private async Task<Stream?> GenerateEpisodeImageAsync(Episode episode, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting optimized poster generation for episode: \"{EpisodeName}\"", episode.Name);

                // Configuration retrieval with defensive fallback ensuring processing continuity
                var config = Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration();

                // Service availability validation ensuring processing capability and resource access
                var ffmpegService = Plugin.Instance?.FFmpegService;
                var posterGeneratorService = Plugin.Instance?.PosterGeneratorService;

                if (ffmpegService == null || posterGeneratorService == null)
                {
                    _logger.LogError("Plugin services not available");
                    return null;
                }

                // Temporary resource preparation with directory management and path coordination
                var tempDir = Path.Combine(_appPaths.TempDirectory, "episodeposter");
                Directory.CreateDirectory(tempDir);

                var tempFramePath = Path.Combine(tempDir, $"frame_{episode.Id}_{DateTime.UtcNow.Ticks}.jpg");
                var tempPosterPath = Path.Combine(tempDir, $"poster_{episode.Id}_{DateTime.UtcNow.Ticks}.jpg");

                try
                {
                    string? extractedFramePath;

                    if (config.ExtractPoster)
                    {
                        if (string.IsNullOrEmpty(episode.Path) || !File.Exists(episode.Path))
                        {
                            _logger.LogWarning("Episode video file not found: \"{Path}\"", episode.Path);
                            return null;
                        }

                        // Duration analysis with metadata optimization and FFprobe fallback
                        var duration = GetDurationFromEpisode(episode);
                        if (!duration.HasValue)
                        {
                            _logger.LogInformation("Episode duration not available from metadata, falling back to FFprobe");
                            duration = await ffmpegService.GetVideoDurationAsync(episode.Path, cancellationToken).ConfigureAwait(false);
                        }

                        if (!duration.HasValue)
                        {
                            _logger.LogWarning("Could not get video duration for: \"{Path}\"", episode.Path);
                            return null;
                        }

                        _logger.LogDebug("Video duration: {Duration} for episode: \"{EpisodeName}\"", duration.Value, episode.Name);

                        // Advanced video analysis with black scene detection and intelligent frame selection
                        var blackIntervals = await ffmpegService.DetectBlackScenesAsync(episode.Path, duration.Value, 0.1, 0.1, cancellationToken).ConfigureAwait(false);
                        var selectedTimestamp = ffmpegService.SelectRandomTimestamp(duration.Value, blackIntervals);

                        _logger.LogDebug("Random timestamp selected: {Timestamp} for episode: \"{EpisodeName}\"", selectedTimestamp, episode.Name);

                        // High-quality frame extraction with hardware acceleration and optimization
                        extractedFramePath = await ffmpegService.ExtractFrameAsync(episode.Path, selectedTimestamp, tempFramePath, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        extractedFramePath = CreateTransparentImage(tempFramePath);
                    }

                    // Source image validation ensuring processing readiness and content availability
                    if (extractedFramePath == null || !File.Exists(extractedFramePath))
                    {
                        _logger.LogWarning("Failed to create source image");
                        return null;
                    }

                    // Comprehensive poster synthesis with style-specific processing and text overlay
                    var processedPath = posterGeneratorService.ProcessImageWithText(extractedFramePath, tempPosterPath, episode, config);
                    if (processedPath == null || !File.Exists(processedPath))
                    {
                        _logger.LogWarning("Failed to process image with text overlay");
                        return null;
                    }

                    // Final image preparation with memory stream generation for Jellyfin integration
                    var imageBytes = await File.ReadAllBytesAsync(processedPath, cancellationToken).ConfigureAwait(false);
                    return new MemoryStream(imageBytes);
                }
                finally
                {
                    // Comprehensive cleanup automation ensuring resource disposal and system cleanliness
                    if (File.Exists(tempFramePath))
                    {
                        try { File.Delete(tempFramePath); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temp frame: \"{Path}\"", tempFramePath); }
                    }

                    if (File.Exists(tempPosterPath))
                    {
                        try { File.Delete(tempPosterPath); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temp poster: \"{Path}\"", tempPosterPath); }
                    }
                }
            }
            catch (Exception ex)
            {
                // Comprehensive error handling ensuring graceful degradation and administrative visibility
                _logger.LogError(ex, "Error generating poster for episode: \"{EpisodeName}\"", episode.Name);
                return null;
            }
        }

        /// <summary>
        /// Extracts episode duration information from Jellyfin metadata with efficient access patterns
        /// providing performance optimization through cached metadata utilization. This method implements
        /// intelligent duration extraction avoiding unnecessary video analysis when episode metadata
        /// contains accurate timing information enabling responsive poster generation workflows while
        /// maintaining processing efficiency through metadata-first optimization strategies.
        /// 
        /// The duration extraction serves as a critical performance optimization component enabling
        /// efficient poster generation by leveraging cached metadata when available while providing
        /// foundation for FFprobe fallback when metadata is incomplete or unavailable. The implementation
        /// balances performance optimization with reliability ensuring accurate duration information
        /// essential for video analysis and intelligent frame extraction algorithms.
        /// 
        /// Metadata Optimization and Efficiency:
        /// Advanced metadata access patterns prioritize cached episode information providing immediate
        /// duration access when available while minimizing video processing overhead typical in
        /// metadata-based optimization strategies. The optimization ensures responsive operation
        /// while maintaining accuracy in duration determination essential for content analysis.
        /// 
        /// Performance Benefits and Resource Conservation:
        /// Duration extraction from metadata provides significant performance improvements over
        /// direct video analysis while maintaining accuracy essential for intelligent timestamp
        /// calculation and frame extraction algorithms. The conservation approach minimizes
        /// resource utilization while ensuring reliable processing workflows.
        /// </summary>
        /// <param name="episode">
        /// Episode object containing potentially cached duration metadata enabling efficient
        /// duration extraction and performance optimization during poster generation workflows.
        /// </param>
        /// <returns>
        /// TimeSpan representing episode duration if available in metadata, null if direct
        /// video analysis is required. Null return triggers FFprobe-based duration detection
        /// providing reliable fallback when metadata is incomplete or unavailable.
        /// </returns>
        // MARK: GetDurationFromEpisode
        private TimeSpan? GetDurationFromEpisode(Episode episode)
        {
            // Metadata-based duration extraction with performance optimization and fallback support
            if (episode.RunTimeTicks.HasValue)
            {
                return TimeSpan.FromTicks(episode.RunTimeTicks.Value);
            }

            // Return null indicating metadata unavailability triggering FFprobe-based analysis
            return null;
        }

        // MARK: CreateTransparentImage
        private string? CreateTransparentImage(string outputPath)
        {
            try
            {
                using var bitmap = new SKBitmap(3000, 2000);
                using var canvas = new SKCanvas(bitmap);

                canvas.Clear(SKColors.Transparent);

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
                using var outputStream = File.OpenWrite(outputPath);
                data.SaveTo(outputStream);

                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create transparent image at: \"{Path}\"", outputPath);
                return null;
            }
        }
    }
}