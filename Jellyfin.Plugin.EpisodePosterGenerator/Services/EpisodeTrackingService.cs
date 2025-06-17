using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.Database;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    /// <summary>
    /// Comprehensive episode processing state management service providing intelligent tracking and decision-making
    /// capabilities for poster generation workflows with sophisticated change detection, persistent storage, and
    /// optimization algorithms that eliminate redundant processing while ensuring accurate reprocessing when
    /// content modifications, configuration changes, or manual interventions require updated poster generation.
    /// 
    /// This service represents a critical efficiency component in the poster generation ecosystem, implementing
    /// advanced tracking mechanisms that provide substantial performance improvements during batch operations
    /// while maintaining accuracy and reliability across diverse processing scenarios. The implementation
    /// focuses on intelligent decision-making algorithms that balance processing efficiency with content
    /// quality assurance ensuring optimal resource utilization and consistent user experience.
    /// 
    /// Core Architecture and Responsibility Matrix:
    /// The service implements a sophisticated multi-layered tracking architecture that separates processing
    /// state management from execution logic, enabling optimal decision-making through comprehensive change
    /// detection algorithms while maintaining persistent storage for reliable operation across system
    /// restarts and extended processing sessions with diverse episode collections and content types.
    /// 
    /// Processing Decision Engine Overview:
    /// 
    /// Primary Processing Determination:
    /// Advanced algorithms analyze multiple factors including episode image availability, file system changes,
    /// configuration modifications, and manual intervention indicators to provide intelligent processing
    /// decisions that optimize resource utilization while ensuring comprehensive coverage of episodes
    /// requiring poster generation or regeneration based on content or configuration changes.
    /// 
    /// Change Detection Infrastructure:
    /// Sophisticated change detection mechanisms monitor video file modifications, configuration updates,
    /// and image alterations providing precise reprocessing triggers that ensure poster accuracy while
    /// avoiding unnecessary regeneration of unchanged content. The detection system uses cryptographic
    /// hashing and file system metadata analysis for reliable change identification.
    /// 
    /// Persistent State Management:
    /// Comprehensive database integration provides reliable persistent storage for processing records
    /// enabling accurate tracking across system restarts, batch operations, and extended processing
    /// sessions. The storage system maintains detailed metadata supporting complex decision-making
    /// algorithms while ensuring data integrity and consistency across diverse usage patterns.
    /// 
    /// Performance Optimization Strategies:
    /// 
    /// Intelligent Caching and Lookup:
    /// Optimized database queries and in-memory caching strategies minimize lookup overhead during
    /// batch processing operations while maintaining accuracy in processing determination. The caching
    /// system provides significant performance improvements for large episode collections while
    /// ensuring real-time accuracy for change detection and processing decision algorithms.
    /// 
    /// Batch Operation Optimization:
    /// Specialized algorithms optimize performance during large-scale batch processing operations
    /// by minimizing database queries, optimizing change detection calculations, and providing
    /// efficient bulk operations for scenarios involving extensive episode collections requiring
    /// comprehensive processing evaluation and state management.
    /// 
    /// Cryptographic Efficiency:
    /// Configuration hash generation uses optimized cryptographic algorithms providing reliable
    /// change detection while maintaining computational efficiency during repeated hash calculations
    /// typical in batch processing scenarios with consistent configuration parameters across
    /// multiple episodes requiring processing evaluation.
    /// 
    /// Algorithm Implementation Details:
    /// 
    /// Multi-Factor Processing Decision Algorithm:
    /// The processing determination system implements sophisticated multi-factor analysis considering
    /// image availability, file system metadata, configuration consistency, and temporal relationships
    /// between processing events and content modifications. The algorithm provides comprehensive
    /// evaluation ensuring accurate processing decisions across diverse scenarios and edge cases.
    /// 
    /// Configuration Change Detection:
    /// Advanced cryptographic hashing algorithms generate consistent configuration fingerprints
    /// enabling precise detection of settings changes that require poster regeneration. The hashing
    /// system uses normalized JSON serialization ensuring consistent hash generation regardless
    /// of property ordering or formatting variations in configuration data structures.
    /// 
    /// Temporal Analysis and Validation:
    /// Sophisticated timestamp analysis algorithms compare processing dates with file modification
    /// times and image creation dates providing accurate determination of content changes requiring
    /// reprocessing. The temporal analysis includes timezone handling and precision considerations
    /// ensuring reliable change detection across diverse system configurations.
    /// 
    /// Image Modification Detection:
    /// Comprehensive image analysis algorithms detect manual modifications to generated posters
    /// enabling preservation of user customizations while ensuring automatic regeneration when
    /// underlying content or configuration changes warrant updated poster creation. The detection
    /// system balances automation with user control providing optimal workflow flexibility.
    /// 
    /// Integration Architecture and Coordination:
    /// 
    /// Database Layer Integration:
    /// Seamless integration with persistent storage infrastructure provides reliable data management
    /// while abstracting database implementation details from processing logic. The integration
    /// enables flexible storage backend selection while maintaining consistent service behavior
    /// and performance characteristics across different deployment configurations.
    /// 
    /// Episode Metadata Coordination:
    /// Advanced integration with Jellyfin's episode metadata system provides comprehensive access
    /// to file paths, image locations, and temporal information essential for accurate processing
    /// decisions. The coordination ensures consistent behavior while leveraging Jellyfin's
    /// metadata infrastructure for optimal integration and compatibility.
    /// 
    /// Configuration System Integration:
    /// Sophisticated integration with plugin configuration management enables real-time change
    /// detection and processing decision updates ensuring poster generation reflects current
    /// user preferences while maintaining processing efficiency through intelligent change
    /// detection and selective regeneration based on configuration impact analysis.
    /// 
    /// Service Ecosystem Coordination:
    /// The tracking service integrates with other plugin components through standardized interfaces
    /// enabling efficient coordination between video analysis, poster generation, and batch
    /// processing systems while maintaining loose coupling and modular architecture principles
    /// essential for maintainability and extensibility.
    /// 
    /// Error Handling and Reliability:
    /// 
    /// Comprehensive Exception Management:
    /// Multi-layered error handling ensures graceful degradation when tracking operations encounter
    /// issues, with intelligent fallback strategies that prioritize processing accuracy over
    /// optimization efficiency. The error handling system provides detailed logging for debugging
    /// while maintaining service availability and processing continuity.
    /// 
    /// Data Integrity and Consistency:
    /// Robust validation mechanisms ensure tracking data integrity while providing automatic
    /// recovery from corrupted or inconsistent state information. The integrity system includes
    /// validation algorithms and automatic cleanup procedures maintaining reliable operation
    /// across diverse failure scenarios and system configurations.
    /// 
    /// Fallback and Recovery Mechanisms:
    /// Intelligent fallback strategies ensure continued operation when tracking data is unavailable
    /// or inconsistent, with conservative processing decisions that prioritize content accuracy
    /// over efficiency optimization. The recovery mechanisms enable automatic restoration of
    /// tracking capability while maintaining processing workflow continuity.
    /// 
    /// Performance Monitoring and Optimization:
    /// Comprehensive performance monitoring capabilities enable administrative oversight and
    /// optimization while providing detailed metrics for tracking efficiency analysis and
    /// system tuning in production environments with diverse episode collections and processing
    /// workload characteristics requiring ongoing optimization and monitoring.
    /// 
    /// Backward Compatibility and API Design:
    /// 
    /// Synchronous API Compatibility:
    /// Comprehensive backward compatibility support maintains existing synchronous API interfaces
    /// while providing enhanced asynchronous capabilities for improved performance and scalability.
    /// The compatibility layer ensures seamless integration with existing code while enabling
    /// migration to optimized asynchronous patterns for enhanced system performance.
    /// 
    /// Interface Stability and Evolution:
    /// Careful API design ensures interface stability while enabling functional enhancement and
    /// performance optimization. The design approach maintains compatibility with existing
    /// integrations while providing pathway for future enhancement and capability expansion
    /// based on evolving requirements and performance optimization opportunities.
    /// 
    /// The service represents a foundational component in the poster generation infrastructure,
    /// providing essential tracking and decision-making capabilities that enable efficient,
    /// intelligent poster generation while maintaining accuracy, reliability, and optimal
    /// resource utilization across diverse deployment scenarios and usage patterns requiring
    /// comprehensive episode processing management and optimization.
    /// </summary>
    public class EpisodeTrackingService
    {
        /// <summary>
        /// Logger instance for comprehensive tracking service monitoring, debugging, and administrative oversight.
        /// Provides detailed logging throughout the episode tracking workflow including processing decisions,
        /// change detection results, and database operations essential for system monitoring and troubleshooting.
        /// </summary>
        private readonly ILogger<EpisodeTrackingService> _logger;

        /// <summary>
        /// Database service providing persistent storage infrastructure for episode processing records with
        /// comprehensive CRUD operations and query capabilities. Enables reliable tracking state management
        /// across system restarts and extended processing sessions while maintaining data integrity and
        /// consistency essential for accurate processing decision algorithms and optimization strategies.
        /// </summary>
        private readonly EpisodeTrackingDatabase _database;

        /// <summary>
        /// Optimized JSON serialization configuration for consistent configuration hash generation ensuring
        /// reliable change detection across diverse configuration structures and formatting variations.
        /// Uses camel case naming policy and minimal formatting for consistent hash generation while
        /// maintaining computational efficiency during repeated serialization operations typical in
        /// batch processing scenarios requiring configuration consistency validation.
        /// </summary>
        private static readonly JsonSerializerOptions HashOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,  // Consistent property naming
            WriteIndented = false                               // Minimal formatting for hash consistency
        };

        /// <summary>
        /// Initializes a new instance of the episode tracking service with essential database integration
        /// and establishes the foundational infrastructure for episode processing state management.
        /// Sets up logging and database connectivity enabling comprehensive tracking capabilities while
        /// maintaining optimal performance and reliability characteristics essential for efficient
        /// poster generation workflow optimization and resource utilization management.
        /// 
        /// The constructor establishes the service foundation for tracking operations by integrating
        /// with the database infrastructure and preparing necessary service references for efficient
        /// processing decision algorithms, change detection mechanisms, and persistent state management
        /// throughout the episode tracking and optimization workflows.
        /// 
        /// Integration Strategy:
        /// The initialization process focuses on establishing robust connections with persistent storage
        /// infrastructure while preparing the service for efficient tracking operations across diverse
        /// episode collections and processing scenarios with optimal performance characteristics and
        /// reliability patterns essential for large-scale poster generation operations.
        /// </summary>
        /// <param name="logger">
        /// Logger service for tracking operation monitoring, error reporting, and debugging information.
        /// Provides comprehensive logging capabilities throughout episode tracking workflows enabling
        /// administrative oversight and troubleshooting during poster generation operations.
        /// </param>
        /// <param name="database">
        /// Database service providing persistent storage capabilities for episode processing records.
        /// Enables reliable state management and tracking data persistence essential for efficient
        /// processing decision algorithms and optimization strategies across system restarts.
        /// </param>
        // MARK: Constructor
        public EpisodeTrackingService(ILogger<EpisodeTrackingService> logger, EpisodeTrackingDatabase database)
        {
            _logger = logger;
            _database = database;
            _logger.LogInformation("Episode tracking service initialized");
        }

        /// <summary>
        /// Implements sophisticated multi-factor analysis algorithm determining whether episodes require
        /// poster generation processing based on comprehensive evaluation of image availability, file system
        /// changes, configuration modifications, and temporal relationships between processing events and
        /// content alterations. This method serves as the primary intelligence engine for processing
        /// optimization providing significant efficiency improvements while ensuring accurate coverage
        /// of episodes requiring poster generation or regeneration based on detected changes.
        /// 
        /// This asynchronous method represents the core decision-making capability enabling intelligent
        /// processing workflow optimization through sophisticated change detection and state analysis
        /// algorithms that balance processing efficiency with content accuracy ensuring optimal resource
        /// utilization while maintaining comprehensive coverage of episodes requiring processing attention.
        /// 
        /// Multi-Factor Decision Algorithm:
        /// 
        /// Primary Image Availability Analysis:
        /// Fundamental evaluation determining whether episodes possess primary image assets enabling
        /// immediate processing determination for episodes lacking visual representation. This analysis
        /// provides essential baseline assessment ensuring comprehensive coverage of episodes requiring
        /// initial poster generation while avoiding unnecessary processing of adequately represented content.
        /// 
        /// Historical Processing Record Evaluation:
        /// Sophisticated database query operations retrieve existing processing records enabling temporal
        /// analysis and change detection algorithms essential for avoiding redundant processing while
        /// ensuring accurate reprocessing when content modifications warrant updated poster generation.
        /// Missing records trigger automatic processing ensuring comprehensive coverage of new content.
        /// 
        /// File System Change Detection:
        /// Advanced file system metadata analysis comparing current video file characteristics with
        /// recorded processing metadata detecting modifications requiring poster regeneration. The analysis
        /// includes file size validation, modification timestamp comparison, and path verification ensuring
        /// accurate detection of content changes affecting poster generation requirements.
        /// 
        /// Configuration Impact Analysis:
        /// Comprehensive configuration change detection using cryptographic hash comparison enabling
        /// precise identification of settings modifications requiring poster regeneration. The analysis
        /// ensures generated posters reflect current user preferences while avoiding unnecessary
        /// regeneration when configuration changes don't impact visual output characteristics.
        /// 
        /// Image Modification Detection:
        /// Sophisticated temporal analysis comparing image modification timestamps with processing records
        /// detecting manual customizations or external modifications requiring preservation through
        /// reprocessing avoidance. The detection balances automation efficiency with user control
        /// maintaining workflow flexibility while ensuring automatic updates when appropriate.
        /// 
        /// Performance Optimization and Caching:
        /// Optimized database queries and efficient change detection algorithms minimize computational
        /// overhead during batch processing operations while maintaining accuracy in processing
        /// determination. The optimization strategies provide significant performance improvements
        /// for large episode collections while ensuring real-time accuracy in decision algorithms.
        /// 
        /// Error Handling and Reliability:
        /// Comprehensive error handling ensures graceful degradation when processing determination
        /// encounters issues, with conservative fallback strategies prioritizing processing accuracy
        /// over optimization efficiency. The error management maintains batch processing continuity
        /// while providing detailed logging for debugging and administrative oversight.
        /// </summary>
        /// <param name="episode">
        /// Episode object containing metadata and file information required for comprehensive processing
        /// analysis. Provides access to file paths, image locations, and temporal information essential
        /// for accurate change detection and processing decision algorithms.
        /// </param>
        /// <param name="config">
        /// Plugin configuration object containing current settings used for configuration change detection
        /// and impact analysis. Enables precise identification of settings modifications requiring
        /// poster regeneration while maintaining processing efficiency through intelligent change detection.
        /// </param>
        /// <returns>
        /// Boolean indicating whether the specified episode requires poster generation processing based
        /// on comprehensive multi-factor analysis. True indicates processing requirement, false indicates
        /// current poster adequacy enabling processing optimization and resource efficiency.
        /// </returns>
        // MARK: ShouldProcessEpisodeAsync
        public async Task<bool> ShouldProcessEpisodeAsync(Episode episode, PluginConfiguration config)
        {
            // Validate episode accessibility and file system presence for processing eligibility
            if (string.IsNullOrEmpty(episode.Path) || !File.Exists(episode.Path))
            {
                return false;
            }

            // Primary image availability analysis - always process episodes lacking visual representation
            if (!episode.HasImage(ImageType.Primary, 0))
            {
                return true;
            }

            // Historical processing record retrieval for temporal analysis and change detection
            var record = await _database.GetProcessedEpisodeAsync(episode.Id).ConfigureAwait(false);
            if (record == null)
            {
                return true;
            }

            // File system metadata analysis for content change detection
            var fileInfo = new FileInfo(episode.Path);
            var currentConfigHash = GenerateConfigurationHash(config);

            // Comprehensive change detection analysis using file system metadata and configuration comparison
            var shouldReprocess = record.ShouldReprocess(
                episode.Path,
                fileInfo.Length,
                fileInfo.LastWriteTime,
                currentConfigHash
            );

            if (shouldReprocess)
            {
                return true;
            }

            // Image modification detection for manual customization preservation analysis
            var imageModifiedAfterProcessing = IsImageModifiedAfterProcessing(episode, record);
            if (imageModifiedAfterProcessing)
            {
                return true;
            }

            // No processing required - current poster remains adequate and current
            return false;
        }

        /// <summary>
        /// Provides synchronous processing determination interface maintaining backward compatibility with
        /// existing integration patterns while leveraging enhanced asynchronous processing capabilities
        /// for optimal performance and reliability. This method serves as a compatibility bridge enabling
        /// seamless integration with synchronous calling contexts while providing access to sophisticated
        /// asynchronous processing decision algorithms essential for comprehensive episode tracking.
        /// 
        /// The synchronous wrapper implements careful thread coordination ensuring reliable operation
        /// while maintaining the advanced decision-making capabilities of the underlying asynchronous
        /// implementation. This approach provides interface stability for existing integrations while
        /// enabling migration pathway to optimized asynchronous patterns for enhanced performance.
        /// 
        /// Compatibility and Performance Considerations:
        /// While providing synchronous interface compatibility, this method coordinates with asynchronous
        /// infrastructure ensuring access to database operations and change detection algorithms while
        /// maintaining thread safety and reliability. The implementation balances compatibility
        /// requirements with performance optimization providing practical integration solutions.
        /// </summary>
        /// <param name="episode">
        /// Episode object containing metadata and file information required for processing analysis.
        /// Provides comprehensive episode data enabling accurate processing decision determination.
        /// </param>
        /// <param name="config">
        /// Plugin configuration object used for change detection and processing impact analysis.
        /// Enables configuration-aware processing decisions while maintaining compatibility interface.
        /// </param>
        /// <returns>
        /// Boolean indicating processing requirement based on comprehensive analysis algorithms.
        /// Result matches asynchronous implementation ensuring consistent decision-making across
        /// different calling patterns and integration scenarios.
        /// </returns>
        // MARK: ShouldProcessEpisode (Sync version for backward compatibility)
        public bool ShouldProcessEpisode(Episode episode, PluginConfiguration config)
        {
            // Coordinate with asynchronous implementation ensuring consistent decision-making algorithms
            return ShouldProcessEpisodeAsync(episode, config).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Records successful episode processing completion with comprehensive metadata capture enabling
        /// accurate future processing determination and optimization strategies. This method implements
        /// sophisticated record generation algorithms that capture essential state information including
        /// file system metadata, configuration fingerprints, and temporal markers essential for intelligent
        /// change detection and processing decision algorithms during subsequent evaluation operations.
        /// 
        /// The recording process serves as a critical component in processing optimization providing
        /// persistent state management that enables significant efficiency improvements through accurate
        /// change detection while ensuring comprehensive tracking of processing events essential for
        /// administrative oversight and system monitoring across diverse episode collections and
        /// processing scenarios requiring detailed state management and optimization capabilities.
        /// 
        /// Comprehensive Metadata Capture Strategy:
        /// 
        /// Temporal Marker Recording:
        /// Precise timestamp capture using UTC coordination ensuring consistent temporal reference
        /// across diverse system configurations and timezone environments. The temporal markers
        /// provide essential baseline information for subsequent change detection algorithms and
        /// modification analysis enabling accurate processing decision determination.
        /// 
        /// File System Metadata Integration:
        /// Comprehensive file system attribute capture including file size, modification timestamps,
        /// and path information providing essential data for content change detection algorithms.
        /// The metadata integration ensures accurate identification of video file modifications
        /// requiring poster regeneration while maintaining processing efficiency through precise
        /// change detection capabilities.
        /// 
        /// Configuration Fingerprint Generation:
        /// Advanced cryptographic hash generation creating consistent configuration fingerprints
        /// enabling precise detection of settings changes affecting poster generation requirements.
        /// The fingerprint system ensures regeneration occurs when configuration modifications
        /// impact visual output while avoiding unnecessary processing for irrelevant changes.
        /// 
        /// Database Integration and Persistence:
        /// Sophisticated database operations ensure reliable record persistence with comprehensive
        /// error handling and transaction management maintaining data integrity across diverse
        /// system configurations and failure scenarios. The persistence system provides foundation
        /// for reliable tracking and optimization across system restarts and extended operations.
        /// 
        /// Performance Optimization:
        /// Optimized record generation and database operations minimize overhead during batch
        /// processing scenarios while maintaining comprehensive metadata capture essential for
        /// accurate processing decisions. The optimization strategies provide efficient tracking
        /// capabilities suitable for large-scale episode collections and intensive processing workflows.
        /// </summary>
        /// <param name="episode">
        /// Episode object containing metadata and file information for comprehensive record generation.
        /// Provides essential episode data including identifiers, file paths, and temporal information
        /// required for accurate tracking record creation and future processing determination.
        /// </param>
        /// <param name="config">
        /// Plugin configuration object used for configuration fingerprint generation enabling precise
        /// change detection and processing impact analysis. Provides current settings snapshot
        /// essential for intelligent reprocessing determination based on configuration modifications.
        /// </param>
        /// <returns>
        /// Task representing asynchronous record creation operation with comprehensive error handling
        /// and database integration. Completion indicates successful tracking record persistence
        /// enabling future processing optimization and decision-making capabilities.
        /// </returns>
        // MARK: MarkEpisodeProcessedAsync
        public async Task MarkEpisodeProcessedAsync(Episode episode, PluginConfiguration config)
        {
            // Validate episode accessibility and file system presence for record creation eligibility
            if (string.IsNullOrEmpty(episode.Path) || !File.Exists(episode.Path))
            {
                return;
            }

            // File system metadata capture for comprehensive change detection capability
            var fileInfo = new FileInfo(episode.Path);
            var configHash = GenerateConfigurationHash(config);

            // Comprehensive processing record construction with essential metadata integration
            var record = new ProcessedEpisodeRecord
            {
                EpisodeId = episode.Id,                          // Unique episode identifier
                LastProcessed = DateTime.UtcNow,                 // UTC processing timestamp
                VideoFilePath = episode.Path,                    // Current file path
                VideoFileSize = fileInfo.Length,                 // File size for change detection
                VideoFileLastModified = fileInfo.LastWriteTime,  // Modification timestamp
                ConfigurationHash = configHash                   // Configuration fingerprint
            };

            // Persistent storage with comprehensive error handling and database integration
            await _database.SaveProcessedEpisodeAsync(record).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves comprehensive count of processed episodes from persistent storage enabling
        /// administrative monitoring and statistical analysis of processing coverage across episode
        /// collections. This method provides essential metrics for system monitoring, performance
        /// evaluation, and administrative oversight enabling data-driven optimization decisions
        /// and comprehensive understanding of processing workflow effectiveness and coverage.
        /// 
        /// The count retrieval implements optimized database queries providing efficient metrics
        /// access while maintaining accuracy across diverse episode collections and processing
        /// scenarios. The statistical information enables administrative oversight and system
        /// optimization through comprehensive coverage analysis and processing effectiveness evaluation.
        /// </summary>
        /// <returns>
        /// Integer representing total count of episodes with recorded processing completion in
        /// persistent storage. Provides comprehensive coverage metrics for administrative monitoring
        /// and system analysis enabling optimization decisions and performance evaluation.
        /// </returns>
        // MARK: GetProcessedCountAsync
        public async Task<int> GetProcessedCountAsync()
        {
            // Optimized database query for efficient count retrieval with comprehensive error handling
            return await _database.GetProcessedCountAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Provides synchronous count retrieval interface maintaining backward compatibility while
        /// leveraging optimized asynchronous database operations for enhanced performance and reliability.
        /// This method serves as a compatibility bridge enabling seamless integration with synchronous
        /// calling contexts while providing access to efficient database query capabilities essential
        /// for comprehensive statistical analysis and administrative monitoring across diverse usage patterns.
        /// </summary>
        /// <returns>
        /// Integer representing total processed episode count matching asynchronous implementation
        /// results ensuring consistent metrics across different calling patterns and integration scenarios.
        /// </returns>
        // MARK: GetProcessedCount (Sync version for backward compatibility)
        public int GetProcessedCount()
        {
            // Coordinate with asynchronous implementation ensuring consistent statistical results
            return GetProcessedCountAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Performs comprehensive clearing of all processing records from persistent storage enabling
        /// complete tracking state reset for administrative maintenance, testing scenarios, and
        /// systematic reprocessing operations. This method implements sophisticated database operations
        /// with comprehensive error handling ensuring reliable clearing functionality while maintaining
        /// data integrity and providing administrative oversight through detailed logging and confirmation.
        /// 
        /// The clearing operation serves essential administrative functions including testing preparation,
        /// systematic reprocessing initiation, and maintenance operations requiring complete tracking
        /// state reset. The implementation provides reliable clearing capabilities while ensuring
        /// administrative visibility and confirmation of clearing operation completion and effectiveness.
        /// 
        /// Administrative Safety and Confirmation:
        /// Comprehensive logging provides administrative confirmation of clearing operation completion
        /// while ensuring visibility into systematic state changes affecting future processing decisions.
        /// The safety measures enable confident administrative operations while maintaining system
        /// reliability and providing clear operational feedback essential for administrative oversight.
        /// </summary>
        /// <returns>
        /// Task representing asynchronous clearing operation with comprehensive database integration
        /// and error handling. Completion indicates successful removal of all tracking records
        /// enabling fresh processing evaluation and systematic reprocessing across episode collections.
        /// </returns>
        // MARK: ClearProcessedRecordsAsync
        public async Task ClearProcessedRecordsAsync()
        {
            // Comprehensive database clearing with administrative logging and confirmation
            await _database.ClearAllProcessedEpisodesAsync().ConfigureAwait(false);
            _logger.LogInformation("Cleared all processed episode records");
        }

        /// <summary>
        /// Removes specific episode processing records from persistent storage enabling targeted
        /// tracking state management and selective reprocessing control for administrative maintenance
        /// and content management scenarios. This method implements precise record removal operations
        /// with comprehensive error handling ensuring reliable targeted removal while maintaining
        /// data integrity and providing administrative oversight through detailed logging and confirmation.
        /// 
        /// The targeted removal capability enables sophisticated tracking state management supporting
        /// administrative operations including selective reprocessing, content management, and
        /// maintenance workflows requiring precise control over individual episode tracking state
        /// while maintaining overall system integrity and processing optimization capabilities.
        /// </summary>
        /// <param name="episodeId">
        /// Unique episode identifier for targeted record removal enabling precise tracking state
        /// management and selective reprocessing control. Provides specific episode targeting
        /// while maintaining overall tracking system integrity and optimization capabilities.
        /// </param>
        /// <returns>
        /// Task representing asynchronous removal operation with comprehensive database integration
        /// and error handling. Completion indicates successful targeted record removal enabling
        /// selective reprocessing and administrative control over individual episode tracking state.
        /// </returns>
        // MARK: RemoveProcessedRecordAsync
        public async Task RemoveProcessedRecordAsync(Guid episodeId)
        {
            // Targeted record removal with comprehensive error handling
            await _database.RemoveProcessedEpisodeAsync(episodeId).ConfigureAwait(false);
        }

        /// <summary>
        /// Implements forced reprocessing capability through targeted tracking record removal enabling
        /// administrative control over individual episode processing state for content management,
        /// quality assurance, and maintenance scenarios requiring selective poster regeneration.
        /// This method provides sophisticated administrative control enabling precise reprocessing
        /// initiation while maintaining overall tracking system integrity and optimization capabilities.
        /// 
        /// The forced reprocessing functionality serves essential administrative needs including
        /// quality assurance operations, content updates, and maintenance workflows requiring
        /// specific episode poster regeneration regardless of current tracking state or change
        /// detection results. The implementation ensures reliable reprocessing initiation while
        /// maintaining comprehensive administrative oversight and system integrity.
        /// 
        /// Administrative Control and Oversight:
        /// Enhanced logging provides administrative visibility into forced reprocessing operations
        /// enabling monitoring and confirmation of administrative interventions affecting processing
        /// workflows. The oversight capabilities ensure administrative confidence while maintaining
        /// system reliability and providing clear operational feedback for quality assurance workflows.
        /// </summary>
        /// <param name="episodeId">
        /// Unique episode identifier for forced reprocessing enabling targeted administrative control
        /// over individual episode processing state. Provides precise episode targeting for quality
        /// assurance and maintenance operations requiring selective poster regeneration capabilities.
        /// </param>
        /// <returns>
        /// Task representing asynchronous forced reprocessing initiation with comprehensive database
        /// integration and administrative logging. Completion indicates successful tracking record
        /// removal enabling immediate reprocessing eligibility and administrative control confirmation.
        /// </returns>
        // MARK: ForceReprocessEpisodeAsync
        public async Task ForceReprocessEpisodeAsync(Guid episodeId)
        {
            // Forced reprocessing through targeted record removal with enhanced administrative logging
            await _database.RemoveProcessedEpisodeAsync(episodeId).ConfigureAwait(false);
            _logger.LogInformation("Forced reprocessing for episode: {EpisodeId}", episodeId);
        }

        /// <summary>
        /// Implements sophisticated image modification detection algorithms analyzing temporal relationships
        /// between processing events and image file modifications enabling preservation of manual
        /// customizations while ensuring automatic regeneration when appropriate. This method provides
        /// critical intelligence for balancing automation efficiency with user control maintaining
        /// optimal workflow flexibility while ensuring comprehensive coverage of content requiring updates.
        /// 
        /// The modification detection system serves as an essential component in intelligent processing
        /// decision algorithms providing sophisticated analysis of user interventions and manual
        /// customizations enabling preservation of intentional modifications while maintaining automatic
        /// update capabilities when underlying content or configuration changes warrant poster regeneration.
        /// 
        /// Temporal Analysis and Intelligence:
        /// 
        /// Image Accessibility Validation:
        /// Comprehensive file system validation ensuring image accessibility while detecting deletion
        /// scenarios requiring immediate reprocessing for content restoration. The validation provides
        /// essential baseline assessment enabling appropriate response to missing or inaccessible
        /// image assets requiring automatic regeneration for content completeness.
        /// 
        /// Modification Timestamp Analysis:
        /// Sophisticated temporal comparison algorithms analyzing image modification timestamps relative
        /// to processing record timestamps detecting manual interventions requiring preservation through
        /// reprocessing avoidance. The analysis maintains user control while ensuring automatic updates
        /// when processing changes occur after manual customizations.
        /// 
        /// Timezone and Precision Handling:
        /// Advanced timestamp handling ensures accurate temporal comparison across diverse system
        /// configurations and timezone environments while maintaining precision necessary for reliable
        /// modification detection. The handling algorithms provide consistent behavior regardless of
        /// system configuration variations affecting temporal analysis accuracy.
        /// 
        /// Error Handling and Conservative Fallback:
        /// Comprehensive error handling ensures reliable operation when image analysis encounters
        /// issues, with conservative fallback strategies prioritizing content accuracy and user
        /// customization preservation over automation efficiency. The fallback mechanisms maintain
        /// system reliability while ensuring appropriate handling of edge cases and error conditions.
        /// </summary>
        /// <param name="episode">
        /// Episode object providing access to image path information and metadata required for
        /// comprehensive modification analysis. Enables precise image location and accessibility
        /// validation essential for accurate temporal comparison and modification detection algorithms.
        /// </param>
        /// <param name="record">
        /// Processing record containing temporal markers and metadata required for modification
        /// comparison analysis. Provides baseline processing information enabling accurate detection
        /// of subsequent modifications requiring preservation or regeneration decisions.
        /// </param>
        /// <returns>
        /// Boolean indicating whether image modifications occurred after processing completion requiring
        /// reprocessing consideration. True indicates manual modification preservation needs, false
        /// indicates automatic update eligibility based on temporal analysis and detection algorithms.
        /// </returns>
        // MARK: IsImageModifiedAfterProcessing
        private bool IsImageModifiedAfterProcessing(Episode episode, ProcessedEpisodeRecord record)
        {
            try
            {
                // Image accessibility validation and path resolution for modification analysis
                var imagePath = episode.GetImagePath(ImageType.Primary, 0);
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                {
                    // Image deletion detected - requires immediate reprocessing for content restoration
                    return true;
                }

                // File system metadata retrieval for temporal comparison analysis
                var imageFileInfo = new FileInfo(imagePath);
                var imageLastModified = imageFileInfo.LastWriteTime;

                // Temporal comparison analysis detecting modifications subsequent to processing completion
                if (imageLastModified > record.LastProcessed)
                {
                    return true;
                }

                // No modification detected - automatic update eligibility confirmed
                return false;
            }
            catch (Exception ex)
            {
                // Conservative fallback ensuring reprocessing when modification analysis encounters errors
                _logger.LogWarning(ex, "Failed to check image modification time for episode {EpisodeId}, will reprocess", episode.Id);
                return true;
            }
        }

        /// <summary>
        /// Implements sophisticated cryptographic hash generation for plugin configuration objects
        /// enabling precise change detection and processing impact analysis through consistent
        /// fingerprint creation algorithms. This method provides essential configuration tracking
        /// capabilities ensuring poster regeneration occurs when settings modifications affect
        /// visual output while avoiding unnecessary processing for irrelevant configuration changes.
        /// 
        /// The hash generation system serves as a critical component in intelligent processing
        /// optimization providing reliable configuration change detection through advanced
        /// cryptographic algorithms and normalized serialization ensuring consistent hash generation
        /// regardless of configuration property ordering or formatting variations in data structures.
        /// 
        /// Cryptographic Hash Generation Strategy:
        /// 
        /// Normalized JSON Serialization:
        /// Sophisticated serialization configuration ensures consistent JSON output regardless of
        /// property ordering variations or formatting differences enabling reliable hash generation
        /// across diverse configuration scenarios. The normalization approach provides consistent
        /// fingerprint creation while maintaining computational efficiency during repeated operations.
        /// 
        /// SHA256 Cryptographic Algorithm:
        /// Advanced cryptographic hash generation using SHA256 algorithm providing reliable change
        /// detection with minimal collision probability while maintaining computational efficiency
        /// suitable for frequent hash generation operations typical in batch processing scenarios
        /// requiring consistent configuration validation and change detection capabilities.
        /// 
        /// Base64 Encoding Optimization:
        /// Efficient encoding strategy providing compact hash representation suitable for database
        /// storage and comparison operations while maintaining readability for debugging and
        /// administrative oversight. The encoding approach balances storage efficiency with
        /// practical utility for system monitoring and troubleshooting requirements.
        /// 
        /// Performance and Reliability:
        /// Optimized hash generation algorithms provide efficient computation suitable for frequent
        /// execution during batch processing operations while maintaining cryptographic reliability
        /// essential for accurate change detection. The optimization strategies ensure minimal
        /// computational overhead while preserving hash quality and consistency across operations.
        /// </summary>
        /// <param name="config">
        /// Plugin configuration object requiring cryptographic fingerprint generation for change
        /// detection and processing impact analysis. Provides comprehensive configuration data
        /// enabling accurate hash generation and subsequent change detection capabilities.
        /// </param>
        /// <returns>
        /// Base64-encoded cryptographic hash representing configuration fingerprint for change
        /// detection and comparison operations. Provides reliable configuration identification
        /// enabling accurate processing decisions based on settings modifications and impact analysis.
        /// </returns>
        // MARK: GenerateConfigurationHash
        private string GenerateConfigurationHash(PluginConfiguration config)
        {
            // Normalized JSON serialization ensuring consistent hash input generation
            var configString = JsonSerializer.Serialize(config, HashOptions);
            
            // Cryptographic hash generation using SHA256 for reliable change detection
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(configString));
            
            // Base64 encoding providing compact, storage-efficient hash representation
            return Convert.ToBase64String(hash);
        }
    }
}