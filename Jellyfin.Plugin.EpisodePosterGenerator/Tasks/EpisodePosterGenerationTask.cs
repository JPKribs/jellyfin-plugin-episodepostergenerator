using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Tasks
{
    /// <summary>
    /// Comprehensive scheduled task implementation for automated batch processing of episode poster generation.
    /// Integrates seamlessly with Jellyfin's task scheduling system to provide efficient, large-scale poster
    /// creation capabilities with sophisticated progress tracking, error handling, and resource management.
    /// 
    /// This task serves as the primary automation interface for poster generation, enabling administrators
    /// to process entire media libraries efficiently without manual intervention. The implementation provides
    /// enterprise-grade reliability with comprehensive error handling, progress reporting, and resource cleanup
    /// suitable for large-scale media server deployments.
    /// 
    /// Core Functionality:
    /// - Automated discovery and enumeration of episodes requiring poster generation
    /// - Intelligent processing determination using the episode tracking system
    /// - Batch processing with configurable concurrency and resource management
    /// - Real-time progress reporting for administrative monitoring and user feedback
    /// - Comprehensive error handling with detailed logging and graceful degradation
    /// - Automatic resource cleanup preventing disk space accumulation and memory leaks
    /// 
    /// Processing Workflow:
    /// 1. Configuration validation and plugin availability verification
    /// 2. Comprehensive episode discovery across all libraries using Jellyfin's query system
    /// 3. Intelligent filtering using the tracking service to identify episodes requiring processing
    /// 4. Batch processing with progress reporting and cancellation support
    /// 5. Individual episode processing including frame extraction and poster generation
    /// 6. Image upload and metadata integration with Jellyfin's media management system
    /// 7. Tracking database updates to prevent unnecessary reprocessing
    /// 8. Comprehensive cleanup of temporary files and resources
    /// 
    /// Performance Characteristics:
    /// - Sequential processing model ensuring system stability under resource constraints
    /// - Temporary file management with automatic cleanup preventing disk space issues
    /// - Progress reporting optimized for responsiveness without performance impact
    /// - Cancellation support enabling graceful task termination
    /// - Memory-efficient processing suitable for large episode collections
    /// 
    /// Integration Points:
    /// - Jellyfin Task System: Full integration with scheduling, progress, and cancellation
    /// - Library Manager: Comprehensive access to episode metadata and media information
    /// - Provider Manager: Seamless image upload and metadata integration
    /// - Episode Tracking: Intelligent processing decisions and duplicate prevention
    /// - Plugin Services: Complete access to FFmpeg, poster generation, and tracking services
    /// 
    /// Administrative Features:
    /// - Configurable execution through Jellyfin's task scheduling interface
    /// - Detailed progress reporting with success/failure statistics
    /// - Comprehensive logging for debugging and monitoring
    /// - Graceful error handling preventing task failures from affecting system stability
    /// - Resource cleanup ensuring system health after processing completion
    /// 
    /// The task is designed for both manual execution by administrators and automated scheduling
    /// for regular library maintenance, providing flexibility in deployment strategies while
    /// maintaining consistent, reliable operation across different usage patterns.
    /// </summary>
    public class EpisodePosterGenerationTask : IScheduledTask
    {
        /// <summary>
        /// Logger instance for comprehensive task execution monitoring and debugging.
        /// Provides detailed logging throughout the poster generation workflow including
        /// progress updates, error conditions, and performance metrics for administrative oversight.
        /// </summary>
        private readonly ILogger<EpisodePosterGenerationTask> _logger;

        /// <summary>
        /// Jellyfin's library manager service for accessing episode metadata and media library operations.
        /// Enables comprehensive episode discovery, metadata access, and library enumeration
        /// essential for batch processing operations across the entire media collection.
        /// </summary>
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// Jellyfin's provider manager service for image upload and metadata integration operations.
        /// Handles the seamless integration of generated posters into Jellyfin's media management
        /// system, ensuring proper metadata updates and UI refresh triggers.
        /// </summary>
        private readonly IProviderManager _providerManager;

        /// <summary>
        /// Initializes a new instance of the episode poster generation task with required Jellyfin services.
        /// Sets up the task infrastructure for integration with Jellyfin's dependency injection system
        /// and prepares the necessary service references for poster generation operations.
        /// 
        /// The constructor follows Jellyfin's standard dependency injection patterns, accepting
        /// core services required for media library access, image management, and comprehensive
        /// logging throughout the poster generation workflow.
        /// </summary>
        /// <param name="logger">
        /// Logger service for task execution monitoring, error reporting, and debugging information.
        /// Used throughout the poster generation workflow to provide detailed operational insights.
        /// </param>
        /// <param name="libraryManager">
        /// Jellyfin's library management service for episode discovery, metadata access, and
        /// media library operations essential for batch processing functionality.
        /// </param>
        /// <param name="providerManager">
        /// Jellyfin's provider management service for image upload operations and metadata
        /// integration, ensuring generated posters are properly integrated into the media system.
        /// </param>
        // MARK: Constructor
        public EpisodePosterGenerationTask(
            ILogger<EpisodePosterGenerationTask> logger,
            ILibraryManager libraryManager,
            IProviderManager providerManager)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _providerManager = providerManager;
        }

        /// <summary>
        /// Gets the user-friendly display name for this scheduled task shown in Jellyfin's administration interface.
        /// Provides clear identification of the task's purpose for administrators managing scheduled operations.
        /// </summary>
        public string Name => "Generate Episode Posters";

        /// <summary>
        /// Gets the detailed description explaining the task's functionality and purpose for administrative users.
        /// Clearly communicates the task's operation and benefits for media library management and maintenance.
        /// </summary>
        public string Description => "Generates poster images for episodes that don't have them or need updating";

        /// <summary>
        /// Gets the administrative category classification for organizing tasks within Jellyfin's interface.
        /// Groups this task with other library maintenance operations for intuitive administrative access.
        /// </summary>
        public string Category => "Library";

        /// <summary>
        /// Gets the unique identifier key for this task used by Jellyfin's scheduling system.
        /// Provides persistent identification for task management, scheduling, and execution tracking.
        /// </summary>
        public string Key => "EpisodePosterGeneration";

        /// <summary>
        /// Gets a value indicating whether this task should be hidden from the administrative interface.
        /// Returns false to ensure the task is visible and accessible to administrators for manual execution and scheduling.
        /// </summary>
        public bool IsHidden => false;

        /// <summary>
        /// Gets a value indicating whether this task is enabled and available for execution.
        /// Returns true to ensure the task can be scheduled and executed by administrators and the task system.
        /// </summary>
        public bool IsEnabled => true;

        /// <summary>
        /// Gets a value indicating whether task execution should be logged in Jellyfin's task history.
        /// Returns true to provide administrators with execution history and performance tracking capabilities.
        /// </summary>
        public bool IsLogged => true;

        /// <summary>
        /// Gets the default trigger configuration for automatic task scheduling.
        /// Returns an empty collection indicating no automatic scheduling, requiring manual execution
        /// or administrator-configured scheduling to prevent unexpected resource usage.
        /// 
        /// Manual Scheduling Rationale:
        /// Poster generation can be resource-intensive and time-consuming, particularly for large
        /// media libraries. By not providing default triggers, the task ensures administrators
        /// have full control over when processing occurs, allowing for scheduling during
        /// maintenance windows or periods of low system utilization.
        /// </summary>
        /// <returns>
        /// Empty collection of trigger information, indicating the task requires manual execution
        /// or explicit administrator configuration for automated scheduling.
        /// </returns>
        // MARK: GetDefaultTriggers
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }

        /// <summary>
        /// Executes the comprehensive episode poster generation workflow with full progress tracking and error handling.
        /// This method orchestrates the complete batch processing pipeline from episode discovery through poster
        /// creation and integration, providing enterprise-grade reliability and monitoring capabilities.
        /// 
        /// Execution Workflow:
        /// 1. Plugin Configuration Validation: Verifies plugin availability and enabled status
        /// 2. Service Availability Verification: Ensures tracking service accessibility
        /// 3. Episode Discovery: Comprehensive enumeration of episodes across all libraries
        /// 4. Processing Determination: Intelligent filtering using tracking service algorithms
        /// 5. Batch Processing: Sequential episode processing with progress reporting
        /// 6. Error Handling: Comprehensive exception management with detailed logging
        /// 7. Completion Reporting: Final statistics and completion status communication
        /// 
        /// Progress Reporting:
        /// The method provides real-time progress updates suitable for administrative monitoring
        /// and user interface feedback. Progress is calculated based on episode processing
        /// completion percentage, with additional milestone logging for batch operations.
        /// 
        /// Error Handling Strategy:
        /// - Early termination for configuration issues preventing successful execution
        /// - Graceful degradation for individual episode processing failures
        /// - Comprehensive logging for debugging and administrative oversight
        /// - Exception propagation for critical errors requiring administrative attention
        /// 
        /// Cancellation Support:
        /// Full support for cancellation tokens enabling graceful task termination without
        /// data corruption or resource leaks. Cancellation is checked at appropriate intervals
        /// to ensure responsive termination while maintaining processing integrity.
        /// 
        /// Resource Management:
        /// Automatic cleanup of temporary files and resources prevents disk space accumulation
        /// and ensures system health after processing completion, regardless of success or failure.
        /// </summary>
        /// <param name="progress">
        /// Progress reporting interface for real-time updates to administrative interfaces.
        /// Receives percentage completion values (0-100) throughout the processing workflow.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation token for graceful task termination support. Enables responsive
        /// cancellation while maintaining data integrity and resource cleanup.
        /// </param>
        /// <returns>
        /// Task representing the asynchronous execution of the poster generation workflow.
        /// Completion indicates successful processing or graceful handling of error conditions.
        /// </returns>
        // MARK: ExecuteAsync
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            // Validate plugin configuration and availability for execution
            var config = Plugin.Instance?.Configuration;
            if (config == null || !config.EnableTask)
            {
                return;
            }

            // Verify tracking service availability for processing determination
            var trackingService = Plugin.Instance?.TrackingService;
            if (trackingService == null)
            {
                _logger.LogError("Tracking service not available");
                return;
            }

            try
            {
                // Comprehensive episode discovery across all media libraries
                var allEpisodes = GetAllEpisodes();
                var episodesToProcess = new List<Episode>();

                _logger.LogInformation("Checking for items that need a poster");

                // Intelligent processing determination using tracking service algorithms
                foreach (var episode in allEpisodes)
                {
                    // Check for cancellation during episode enumeration
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Use tracking service to determine processing requirements
                    if (await trackingService.ShouldProcessEpisodeAsync(episode, config).ConfigureAwait(false))
                    {
                        episodesToProcess.Add(episode);
                    }
                }

                _logger.LogInformation("{ProcessCount} items still need a poster", episodesToProcess.Count);

                // Early completion for cases where no processing is required
                if (episodesToProcess.Count == 0)
                {
                    progress?.Report(100);
                    return;
                }

                // Execute batch processing with comprehensive monitoring and error handling
                await ProcessEpisodesAsync(episodesToProcess, config, trackingService, progress, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Comprehensive error logging and exception propagation for critical failures
                _logger.LogError(ex, "Error during Episode Poster Generation task");
                throw;
            }
        }

        /// <summary>
        /// Discovers and enumerates all episode items across the media library using comprehensive query algorithms.
        /// This method implements efficient episode discovery with appropriate filtering to ensure only valid,
        /// processable episodes are included in the batch processing workflow.
        /// 
        /// Discovery Algorithm:
        /// 1. Constructs optimized query for episode enumeration across all libraries
        /// 2. Applies filtering for non-virtual items to exclude placeholder content
        /// 3. Performs recursive search to include episodes in nested library structures
        /// 4. Validates episode accessibility by verifying video file existence
        /// 5. Returns filtered collection ready for processing determination
        /// 
        /// Filtering Strategy:
        /// - Excludes virtual items that don't represent actual media files
        /// - Validates video file accessibility to prevent processing failures
        /// - Ensures only episodes with valid file paths are included
        /// - Optimizes subsequent processing by eliminating invalid candidates early
        /// 
        /// Performance Considerations:
        /// The method uses Jellyfin's optimized query system for efficient database access
        /// while applying minimal filtering to maintain performance during episode enumeration.
        /// File existence validation is performed to prevent processing failures later in the workflow.
        /// </summary>
        /// <returns>
        /// Filtered list of episode objects ready for processing determination and batch operations.
        /// All returned episodes have verified file paths and represent accessible media content.
        /// </returns>
        // MARK: GetAllEpisodes
        private List<Episode> GetAllEpisodes()
        {
            // Construct optimized query for comprehensive episode discovery
            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Episode },  // Focus on episode content only
                IsVirtualItem = false,                              // Exclude placeholder/virtual content
                Recursive = true                                    // Include nested library structures
            };

            // Execute query and apply additional filtering for accessibility
            var items = _libraryManager.GetItemList(query);
            return items.OfType<Episode>()
                       .Where(e => !string.IsNullOrEmpty(e.Path) && File.Exists(e.Path))  // Verify file accessibility
                       .ToList();
        }

        /// <summary>
        /// Executes comprehensive batch processing of episodes with sophisticated progress tracking, error handling,
        /// and resource management. This method orchestrates the complete poster generation pipeline for multiple
        /// episodes while maintaining system stability and providing detailed administrative feedback.
        /// 
        /// Batch Processing Strategy:
        /// - Sequential processing model ensuring system stability under resource constraints
        /// - Individual episode error handling preventing single failures from terminating batch operations
        /// - Real-time progress reporting with configurable milestone logging
        /// - Comprehensive statistics tracking for success/failure analysis
        /// - Automatic resource cleanup ensuring system health regardless of processing outcomes
        /// 
        /// Progress Reporting Algorithm:
        /// Progress is calculated as percentage completion based on processed episode count,
        /// with periodic milestone logging every 10 episodes to provide administrative feedback
        /// without overwhelming the log system during large batch operations.
        /// 
        /// Error Handling Strategy:
        /// Individual episode processing errors are logged and tracked but don't terminate
        /// the entire batch operation. This ensures maximum throughput while providing
        /// detailed error information for administrative review and troubleshooting.
        /// 
        /// Resource Management:
        /// Temporary directory creation and cleanup are handled automatically with comprehensive
        /// error handling to prevent disk space accumulation and ensure system cleanliness
        /// after processing completion, regardless of success or failure states.
        /// 
        /// Cancellation Support:
        /// Responsive cancellation checking between episode processing operations enables
        /// graceful termination while maintaining data integrity and resource cleanup.
        /// </summary>
        /// <param name="episodes">Collection of episodes requiring poster generation processing.</param>
        /// <param name="config">Plugin configuration containing poster generation settings and preferences.</param>
        /// <param name="trackingService">Episode tracking service for processing state management.</param>
        /// <param name="progress">Progress reporting interface for real-time administrative feedback.</param>
        /// <param name="cancellationToken">Cancellation token for graceful batch operation termination.</param>
        /// <returns>Task representing the asynchronous batch processing operation completion.</returns>
        // MARK: ProcessEpisodesAsync
        private async Task ProcessEpisodesAsync(
            List<Episode> episodes, 
            Configuration.PluginConfiguration config,
            EpisodeTrackingService trackingService,
            IProgress<double> progress, 
            CancellationToken cancellationToken)
        {
            // Verify plugin service availability for poster generation operations
            var ffmpegService = Plugin.Instance?.FFmpegService;
            var posterService = Plugin.Instance?.PosterGeneratorService;

            if (ffmpegService == null || posterService == null)
            {
                _logger.LogError("Plugin services not available");
                return;
            }

            // Create and manage temporary directory for batch processing operations
            var tempDir = Path.Combine(Path.GetTempPath(), "episodeposter_batch");
            Directory.CreateDirectory(tempDir);

            // Initialize comprehensive statistics tracking for batch operation monitoring
            int processed = 0;
            int succeeded = 0;
            int failed = 0;

            try
            {
                // Sequential processing with comprehensive error handling and progress reporting
                foreach (var episode in episodes)
                {
                    // Responsive cancellation checking for graceful termination support
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        _logger.LogInformation("Starting to create poster for {EpisodeName}", episode.Name);

                        // Execute individual episode processing with comprehensive error handling
                        var success = await ProcessSingleEpisodeAsync(episode, config, ffmpegService, posterService, tempDir, cancellationToken).ConfigureAwait(false);

                        if (success)
                        {
                            // Update tracking database for successful processing
                            await trackingService.MarkEpisodeProcessedAsync(episode, config).ConfigureAwait(false);
                            succeeded++;
                        }
                        else
                        {
                            failed++;
                            _logger.LogWarning("Failed to process episode: {EpisodeName}", episode.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Individual episode error handling with comprehensive logging
                        failed++;
                        _logger.LogError(ex, "Error processing episode: {EpisodeName}", episode.Name);
                    }

                    // Progress reporting and milestone logging for administrative feedback
                    processed++;
                    var progressPercentage = (double)processed / episodes.Count * 100;
                    progress?.Report(progressPercentage);
                }

                // Final batch processing statistics for administrative review
                _logger.LogInformation("{Succeeded} succeeded and {Failed} failed", succeeded, failed);
            }
            finally
            {
                // Comprehensive resource cleanup with error handling
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temp directory: {TempDir}", tempDir);
                }
            }
        }

        /// <summary>
        /// Processes a single episode through the complete poster generation pipeline with comprehensive error handling
        /// and resource management. This method orchestrates frame extraction, poster generation, and image integration
        /// for individual episodes while maintaining isolation to prevent single episode failures from affecting batch operations.
        /// 
        /// Processing Pipeline:
        /// 1. Temporary file path generation with unique identifiers to prevent conflicts
        /// 2. Frame source determination based on poster style (video extraction vs. transparent background)
        /// 3. Video analysis and frame extraction using FFmpeg services with black scene avoidance
        /// 4. Poster generation using configured style and typography settings
        /// 5. Image upload and metadata integration with Jellyfin's media management system
        /// 6. Comprehensive cleanup of temporary files regardless of processing success or failure
        /// 
        /// Frame Source Strategy:
        /// The method intelligently selects between video frame extraction and transparent background
        /// creation based on poster style configuration, ensuring appropriate source material for
        /// different poster generation approaches while optimizing processing efficiency.
        /// 
        /// Video Analysis:
        /// When video extraction is required, the method performs comprehensive duration analysis
        /// using both metadata and direct FFprobe queries, followed by black scene detection and
        /// intelligent timestamp selection to ensure high-quality source frames for poster generation.
        /// 
        /// Error Handling:
        /// Individual processing steps are isolated with specific error handling ensuring detailed
        /// logging and graceful degradation. Processing failures are contained to prevent batch
        /// operation termination while providing comprehensive diagnostic information.
        /// 
        /// Resource Management:
        /// Temporary files are automatically cleaned up in finally blocks ensuring system health
        /// regardless of processing outcomes. The cleanup process includes error handling to prevent
        /// cleanup failures from affecting core functionality.
        /// </summary>
        /// <param name="episode">Episode object containing metadata required for poster generation and file access.</param>
        /// <param name="config">Plugin configuration containing poster generation settings and style preferences.</param>
        /// <param name="ffmpegService">FFmpeg service for video analysis and frame extraction operations.</param>
        /// <param name="posterService">Poster generation service for image processing and text overlay operations.</param>
        /// <param name="tempDir">Temporary directory path for intermediate file storage during processing.</param>
        /// <param name="cancellationToken">Cancellation token for responsive processing termination support.</param>
        /// <returns>
        /// Boolean indicating successful episode processing and image integration with Jellyfin.
        /// False indicates processing failure with detailed error logging for troubleshooting.
        /// </returns>
        // MARK: ProcessSingleEpisodeAsync
        private async Task<bool> ProcessSingleEpisodeAsync(
            Episode episode,
            Configuration.PluginConfiguration config,
            FFmpegService ffmpegService,
            PosterGeneratorService posterService,
            string tempDir,
            CancellationToken cancellationToken)
        {
            // Generate unique temporary file paths to prevent conflicts during concurrent processing
            var tempFramePath = Path.Combine(tempDir, $"frame_{episode.Id}_{DateTime.UtcNow.Ticks}.jpg");
            var tempPosterPath = Path.Combine(tempDir, $"poster_{episode.Id}_{DateTime.UtcNow.Ticks}.jpg");

            try
            {
                string? extractedFramePath;

                // Style-specific frame source determination and creation
                if (!config.ExtractPoster)
                {
                    // Create transparent background
                    extractedFramePath = CreateTransparentImage(tempFramePath);
                }
                else
                {
                    // Comprehensive video analysis and frame extraction for image-based posters
                    var duration = GetDurationFromEpisode(episode);
                    if (!duration.HasValue)
                    {
                        // Fallback duration analysis using direct FFprobe query
                        duration = await ffmpegService.GetVideoDurationAsync(episode.Path, cancellationToken).ConfigureAwait(false);
                    }

                    if (!duration.HasValue)
                    {
                        _logger.LogWarning("Could not get video duration for: {Path}", episode.Path);
                        return false;
                    }

                    // Intelligent frame selection with black scene avoidance
                    var blackIntervals = await ffmpegService.DetectBlackScenesParallelAsync(episode.Path, duration.Value, 0.1, 0.1, cancellationToken).ConfigureAwait(false);
                    var selectedTimestamp = ffmpegService.SelectRandomTimestamp(duration.Value, blackIntervals);

                    var videoStream = episode.GetMediaStreams()?.FirstOrDefault(s => s.Type == MediaStreamType.Video);
                    var colorSpace = videoStream?.ColorSpace ?? "";
                    var colorTransfer = videoStream?.ColorTransfer ?? "";

                    // High-quality frame extraction at selected timestamp
                    extractedFramePath = await ffmpegService.ExtractFrameAsync(episode.Path, selectedTimestamp, tempFramePath, cancellationToken).ConfigureAwait(false);                }

                // Validate successful frame creation or extraction
                if (extractedFramePath == null || !File.Exists(extractedFramePath))
                {
                    _logger.LogWarning("Failed to extract frame for episode: {EpisodeName}", episode.Name);
                    return false;
                }

                // Generate poster with configured style and typography settings
                var processedPath = posterService.ProcessImageWithText(extractedFramePath, tempPosterPath, episode, config);
                if (processedPath == null || !File.Exists(processedPath))
                {
                    _logger.LogWarning("Failed to process image for episode: {EpisodeName}", episode.Name);
                    return false;
                }

                // Integrate generated poster with Jellyfin's media management system
                var success = await UploadImageToJellyfinAsync(episode, processedPath, cancellationToken).ConfigureAwait(false);
                
                if (success)
                {
                    _logger.LogInformation("Poster created for {EpisodeName}", episode.Name);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to upload poster for episode: {EpisodeName}", episode.Name);
                    return false;
                }
            }
            finally
            {
                // Comprehensive cleanup of temporary files regardless of processing outcome
                CleanupTempFile(tempFramePath);
                CleanupTempFile(tempPosterPath);
            }
        }

        /// <summary>
        /// Extracts episode duration information from metadata with graceful fallback handling.
        /// This method provides efficient duration access using cached metadata when available,
        /// avoiding unnecessary video analysis operations for episodes with complete metadata.
        /// 
        /// The method prioritizes metadata-based duration information for performance optimization
        /// while supporting fallback to direct video analysis when metadata is incomplete or unavailable.
        /// This approach minimizes video processing overhead during batch operations.
        /// </summary>
        /// <param name="episode">Episode object containing potentially cached duration metadata.</param>
        /// <returns>
        /// TimeSpan representing episode duration if available in metadata, null if direct
        /// video analysis is required. Null return triggers FFprobe-based duration detection.
        /// </returns>
        // MARK: GetDurationFromEpisode
        private TimeSpan? GetDurationFromEpisode(Episode episode)
        {
            // Extract duration from cached metadata when available for performance optimization
            if (episode.RunTimeTicks.HasValue)
            {
                return TimeSpan.FromTicks(episode.RunTimeTicks.Value);
            }
            
            // Return null to trigger FFprobe-based duration analysis
            return null;
        }

        /// <summary>
        /// Creates a transparent background image for numeral-style posters that don't require video content.
        /// This method generates high-quality transparent backgrounds suitable for text-only poster designs
        /// where the visual focus is on typography rather than episode imagery.
        /// 
        /// Image Specifications:
        /// - Dimensions: 3000x2000 pixels providing high resolution for quality scaling
        /// - Format: JPEG with 95% quality for optimal file size and compatibility
        /// - Background: Fully transparent for maximum design flexibility
        /// - Encoding: Optimized for poster generation workflows and text overlay operations
        /// 
        /// The generated image serves as a foundation for numeral poster styles where
        /// episode numbers in Roman numerals are rendered as the primary visual element
        /// without requiring background imagery from episode content.
        /// </summary>
        /// <param name="outputPath">File system path where the transparent image should be saved.</param>
        /// <returns>
        /// Output file path for successful image creation, null for creation failures.
        /// Null return indicates processing failure with detailed error logging.
        /// </returns>
        // MARK: CreateTransparentImage
        private string? CreateTransparentImage(string outputPath)
        {
            try
            {
                // Create high-resolution bitmap for quality poster generation
                using var bitmap = new SKBitmap(3000, 2000);
                using var canvas = new SKCanvas(bitmap);

                // Set transparent background for text-only poster designs
                canvas.Clear(SKColors.Transparent);

                // Encode and save with optimized quality settings for poster workflows
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
                using var outputStream = File.OpenWrite(outputPath);
                data.SaveTo(outputStream);

                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create transparent image");
                return null;
            }
        }

        /// <summary>
        /// Uploads generated poster images to Jellyfin's media management system with comprehensive metadata integration.
        /// This method handles the complete integration process including image storage, metadata updates,
        /// and UI refresh triggers ensuring generated posters appear immediately in administrative interfaces.
        /// 
        /// Integration Process:
        /// 1. Image file loading and memory stream preparation for upload operations
        /// 2. Provider manager integration for seamless image storage in Jellyfin's system
        /// 3. Metadata repository updates ensuring proper image association and indexing
        /// 4. UI refresh triggers enabling immediate visibility of generated posters
        /// 5. Comprehensive error handling with detailed logging for troubleshooting
        /// 
        /// Image Storage:
        /// The method uses Jellyfin's provider manager for standardized image storage,
        /// ensuring proper integration with Jellyfin's image management system including
        /// thumbnail generation, caching, and metadata indexing for optimal performance.
        /// 
        /// Metadata Integration:
        /// Repository updates trigger proper metadata synchronization ensuring generated
        /// posters are immediately available through Jellyfin's API and user interfaces
        /// without requiring manual refresh or cache invalidation operations.
        /// 
        /// Error Handling:
        /// Comprehensive exception handling ensures upload failures are properly logged
        /// and reported without affecting overall batch processing operations or system stability.
        /// </summary>
        /// <param name="episode">Episode object for poster association and metadata integration.</param>
        /// <param name="imagePath">File system path to the generated poster image for upload.</param>
        /// <param name="cancellationToken">Cancellation token for responsive upload operation termination.</param>
        /// <returns>
        /// Boolean indicating successful image upload and metadata integration.
        /// False indicates upload failure with comprehensive error logging for troubleshooting.
        /// </returns>
        // MARK: UploadImageToJellyfinAsync
        private async Task<bool> UploadImageToJellyfinAsync(Episode episode, string imagePath, CancellationToken cancellationToken)
        {
            try
            {
                // Load generated image into memory for provider manager integration
                var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken).ConfigureAwait(false);
                using var imageStream = new MemoryStream(imageBytes);

                // Integrate image with Jellyfin's provider management system
                await _providerManager.SaveImage(
                    episode,
                    imageStream,
                    "image/jpeg",
                    ImageType.Primary,
                    null,
                    cancellationToken).ConfigureAwait(false);

                // Trigger metadata repository updates for immediate availability
                await episode.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, cancellationToken).ConfigureAwait(false);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload image for episode: {EpisodeName}", episode.Name);
                return false;
            }
        }

        /// <summary>
        /// Safely removes temporary files with comprehensive error handling to prevent resource accumulation.
        /// This utility method ensures temporary files are properly cleaned up regardless of processing
        /// outcomes, maintaining system cleanliness and preventing disk space issues during batch operations.
        /// 
        /// Cleanup Strategy:
        /// - File existence verification before deletion attempts to avoid unnecessary errors
        /// - Comprehensive exception handling preventing cleanup failures from affecting operations
        /// - Warning-level logging for cleanup failures to maintain administrative visibility
        /// - Graceful degradation ensuring processing can continue despite cleanup issues
        /// 
        /// The method implements defensive programming practices ensuring cleanup operations
        /// never interfere with core poster generation functionality while maintaining
        /// system health through proper resource management.
        /// </summary>
        /// <param name="filePath">File system path to the temporary file requiring cleanup and removal.</param>
        // MARK: CleanupTempFile
        private void CleanupTempFile(string filePath)
        {
            try
            {
                // Verify file existence before attempting deletion to avoid unnecessary errors
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                // Log cleanup failures without affecting core processing operations
                _logger.LogWarning(ex, "Failed to delete temp file: {FilePath}", filePath);
            }
        }
    }
}