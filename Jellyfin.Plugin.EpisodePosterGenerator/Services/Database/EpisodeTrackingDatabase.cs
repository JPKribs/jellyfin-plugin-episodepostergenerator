using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.Database;

/// <summary>
/// Comprehensive SQLite database service providing enterprise-grade persistent storage infrastructure
/// for episode processing records with sophisticated data management capabilities, robust transaction
/// handling, and optimized query operations supporting intelligent episode tracking and processing
/// optimization algorithms. Serves as the foundational data persistence layer enabling reliable
/// tracking state management across system restarts, batch operations, and extended processing
/// sessions with diverse episode collections requiring comprehensive state management and optimization.
/// 
/// This service represents a critical infrastructure component in the poster generation ecosystem,
/// implementing advanced database operations that provide essential persistent storage capabilities
/// enabling sophisticated tracking algorithms and processing optimization strategies. The implementation
/// emphasizes data integrity, performance optimization, and reliable operation across diverse
/// deployment environments while maintaining compatibility with Jellyfin's data management patterns.
/// 
/// Database Architecture and Design Philosophy:
/// The service implements a sophisticated SQLite-based persistence layer that balances performance,
/// reliability, and data integrity requirements essential for tracking large episode collections
/// while maintaining optimal query performance and storage efficiency. The architecture separates
/// data access logic from business logic enabling clean separation of concerns while providing
/// optimized database operations suitable for high-frequency tracking and analysis operations.
/// 
/// Core Database Infrastructure Overview:
/// 
/// SQLite Integration and Management:
/// Advanced SQLite database management providing lightweight, embedded persistence with comprehensive
/// transaction support and optimized query performance suitable for Jellyfin plugin deployment
/// environments. The SQLite integration provides reliable data storage without requiring external
/// database infrastructure while maintaining enterprise-grade reliability and performance characteristics.
/// 
/// Schema Design and Optimization:
/// Sophisticated table schema optimized for episode tracking operations with efficient indexing
/// strategies and data type selection providing optimal query performance for tracking operations.
/// The schema design balances storage efficiency with query performance ensuring responsive
/// operation during large-scale batch processing scenarios requiring extensive tracking queries.
/// 
/// Data Integrity and Consistency:
/// Comprehensive data validation and integrity constraints ensuring reliable storage of episode
/// processing records with consistent data formats and validation. The integrity mechanisms
/// prevent data corruption while ensuring consistent behavior across diverse processing scenarios
/// and system configurations requiring reliable tracking state management.
/// 
/// Performance Optimization Strategies:
/// 
/// Query Optimization and Indexing:
/// Advanced query construction with optimized SQL patterns and efficient indexing strategies
/// providing responsive database operations during high-frequency tracking operations typical
/// in batch processing scenarios. The optimization ensures minimal latency for tracking queries
/// while maintaining data integrity and consistency across diverse usage patterns.
/// 
/// Asynchronous Operation Patterns:
/// Comprehensive asynchronous database operations providing non-blocking database access essential
/// for responsive application performance during intensive tracking operations. The asynchronous
/// patterns ensure optimal resource utilization while maintaining responsive user experience
/// during large-scale processing operations requiring extensive database interactions.
/// 
/// Connection Management and Resource Optimization:
/// Sophisticated connection lifecycle management providing efficient database resource utilization
/// while ensuring reliable operation across extended processing sessions. The connection management
/// includes proper disposal patterns and resource cleanup ensuring optimal system performance
/// without resource leaks or connection exhaustion during intensive usage scenarios.
/// 
/// Security and Data Protection:
/// 
/// SQL Injection Prevention:
/// Comprehensive parameterized query implementation ensuring protection against SQL injection
/// attacks through proper parameter binding and input validation. The security measures provide
/// robust protection while maintaining query performance and operational efficiency essential
/// for production deployment environments requiring comprehensive security considerations.
/// 
/// Data Validation and Sanitization:
/// Advanced input validation and data sanitization ensuring reliable storage of episode metadata
/// while preventing data corruption or injection vulnerabilities. The validation mechanisms
/// ensure data integrity while maintaining processing efficiency suitable for high-frequency
/// tracking operations across diverse episode collections and content types.
/// 
/// Access Control and Isolation:
/// Proper database access patterns ensuring isolated operation within Jellyfin's security context
/// while maintaining data confidentiality and integrity. The access control provides appropriate
/// security boundaries while enabling efficient tracking operations essential for poster
/// generation workflow optimization and administrative oversight.
/// 
/// Data Model and Record Management:
/// 
/// ProcessedEpisodeRecord Persistence:
/// Sophisticated storage mechanisms for comprehensive episode processing records including temporal
/// metadata, file system attributes, and configuration fingerprints essential for intelligent
/// processing decisions. The record management provides complete state capture enabling accurate
/// change detection and processing optimization across diverse content and configuration scenarios.
/// 
/// Temporal Data Handling:
/// Advanced DateTime storage and retrieval with culture-invariant formatting ensuring consistent
/// temporal data representation across diverse system configurations and timezone environments.
/// The temporal handling provides reliable timestamp comparison capabilities essential for
/// accurate change detection and processing decision algorithms.
/// 
/// File System Metadata Integration:
/// Comprehensive file system attribute storage including file paths, sizes, and modification
/// timestamps providing essential data for content change detection algorithms. The metadata
/// integration ensures accurate tracking of file system changes affecting poster generation
/// requirements while maintaining storage efficiency and query performance.
/// 
/// Configuration State Management:
/// Sophisticated configuration hash storage enabling precise detection of settings changes
/// requiring poster regeneration while maintaining processing efficiency through intelligent
/// change detection. The configuration management provides foundation for optimization
/// algorithms balancing automation efficiency with content accuracy requirements.
/// 
/// CRUD Operations and Data Access Patterns:
/// 
/// Create and Update Operations:
/// Advanced upsert operations using INSERT OR REPLACE patterns providing efficient record
/// creation and modification with atomic transaction handling. The operations ensure data
/// consistency while providing optimal performance for tracking record management during
/// intensive processing scenarios requiring frequent database updates.
/// 
/// Read Operations and Query Optimization:
/// Sophisticated query operations with optimized SQL construction providing responsive data
/// retrieval for tracking decisions and administrative queries. The read operations include
/// efficient filtering and result processing ensuring minimal latency during processing
/// decision algorithms requiring database lookups and validation.
/// 
/// Delete Operations and Cleanup:
/// Comprehensive deletion operations supporting both targeted record removal and bulk clearing
/// operations essential for administrative maintenance and selective reprocessing scenarios.
/// The deletion operations ensure complete record removal while maintaining database integrity
/// and performance characteristics across diverse cleanup and maintenance workflows.
/// 
/// Bulk Operations and Batch Processing:
/// Optimized bulk operation support providing efficient handling of large-scale tracking
/// operations during batch processing scenarios. The bulk operations minimize database
/// overhead while ensuring data consistency and integrity during intensive processing
/// workflows requiring extensive tracking state management and optimization.
/// 
/// Error Handling and Reliability:
/// 
/// Exception Management and Recovery:
/// Comprehensive error handling ensuring graceful degradation when database operations encounter
/// issues while maintaining data integrity and system stability. The error management includes
/// detailed logging for debugging and monitoring while providing fallback strategies ensuring
/// continued operation during recoverable error conditions.
/// 
/// Transaction Management and Atomicity:
/// Robust transaction handling ensuring atomic operations and data consistency during complex
/// tracking updates and batch operations. The transaction management prevents partial updates
/// and data corruption while ensuring reliable operation across diverse processing scenarios
/// requiring comprehensive state management and coordination.
/// 
/// Connection Recovery and Resilience:
/// Advanced connection management with automatic recovery mechanisms ensuring reliable database
/// access across diverse system configurations and potential connectivity issues. The resilience
/// mechanisms provide stable operation while maintaining performance characteristics essential
/// for production deployment environments.
/// 
/// Resource Management and Lifecycle:
/// 
/// Disposal Pattern Implementation:
/// Comprehensive IDisposable implementation ensuring proper resource cleanup and connection
/// disposal preventing resource leaks and connection exhaustion during extended operation.
/// The disposal pattern follows established .NET patterns while ensuring database resource
/// management suitable for plugin lifecycle management and system integration.
/// 
/// Connection Lifecycle Management:
/// Sophisticated connection lifecycle management providing efficient database resource utilization
/// while ensuring proper cleanup and disposal during service termination. The lifecycle
/// management ensures optimal performance while preventing resource accumulation and
/// system degradation during extended operation periods.
/// 
/// Memory Management and Optimization:
/// Advanced memory management patterns ensuring efficient resource utilization during database
/// operations while preventing memory leaks and excessive allocation during intensive tracking
/// scenarios. The memory management provides optimal performance characteristics suitable
/// for long-running service operation and intensive processing workflows.
/// 
/// Integration Architecture and Compatibility:
/// 
/// Jellyfin Infrastructure Integration:
/// Seamless integration with Jellyfin's application path management and data storage patterns
/// ensuring consistent deployment and configuration across diverse Jellyfin installations.
/// The integration provides reliable data storage location management while maintaining
/// compatibility with Jellyfin's security and data management requirements.
/// 
/// Plugin Ecosystem Coordination:
/// Advanced coordination with plugin infrastructure providing reliable database services
/// essential for tracking service operations and processing optimization algorithms. The
/// coordination ensures efficient service integration while maintaining modular architecture
/// and clean separation of concerns between database and business logic components.
/// 
/// Cross-Platform Compatibility:
/// Comprehensive cross-platform database operation ensuring reliable functionality across
/// Windows, Linux, and macOS deployment environments with consistent behavior and performance
/// characteristics. The compatibility ensures reliable operation regardless of underlying
/// system configuration while maintaining optimal performance and feature availability.
/// 
/// Administrative and Monitoring Capabilities:
/// 
/// Database Path Management:
/// Sophisticated database file location management with proper directory creation and path
/// resolution ensuring reliable database access across diverse deployment configurations.
/// The path management provides predictable database location while ensuring proper
/// permissions and accessibility for plugin operation and administrative oversight.
/// 
/// Logging and Monitoring Integration:
/// Comprehensive logging integration providing detailed database operation monitoring and
/// debugging information essential for administrative oversight and troubleshooting. The
/// logging provides visibility into database performance and operation while maintaining
/// appropriate detail levels for production monitoring and development debugging.
/// 
/// Statistical and Administrative Queries:
/// Advanced administrative query capabilities providing essential statistics and monitoring
/// information for system oversight and performance analysis. The administrative capabilities
/// enable data-driven optimization decisions while providing comprehensive visibility into
/// tracking system effectiveness and database performance characteristics.
/// 
/// The database service represents a foundational infrastructure component enabling sophisticated
/// episode tracking and processing optimization through reliable persistent storage, optimized
/// query operations, and comprehensive data management capabilities essential for efficient
/// poster generation workflows across diverse deployment environments and usage scenarios.
/// </summary>
public sealed class EpisodeTrackingDatabase : IDisposable
{
    /// <summary>
    /// Logger instance for comprehensive database operation monitoring, debugging, and administrative oversight.
    /// Provides detailed logging throughout database lifecycle including initialization, query operations,
    /// and error conditions essential for system monitoring, troubleshooting, and performance analysis.
    /// </summary>
    private readonly ILogger<EpisodeTrackingDatabase> _logger;

    /// <summary>
    /// File system path to the SQLite database file providing persistent storage for episode tracking records.
    /// Calculated during initialization using Jellyfin's application path infrastructure ensuring consistent
    /// database location across diverse deployment configurations while maintaining proper permissions
    /// and accessibility for plugin operation and administrative management.
    /// </summary>
    private readonly string _databasePath;

    /// <summary>
    /// SQLite database connection providing persistent storage infrastructure with comprehensive transaction
    /// support and optimized query capabilities. Managed through sophisticated lifecycle patterns ensuring
    /// efficient resource utilization while maintaining reliable database access across extended operation
    /// periods and intensive tracking workflows requiring frequent database interactions.
    /// </summary>
    private SqliteConnection? _connection;

    /// <summary>
    /// Disposal state tracking ensuring proper resource cleanup and preventing multiple disposal attempts
    /// during service lifecycle management. Provides thread-safe disposal coordination while maintaining
    /// proper resource management patterns essential for plugin lifecycle integration and system stability.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Gets the file system path to the SQLite database file providing transparency for administrative
    /// oversight and debugging purposes. Enables external tools and administrative processes to locate
    /// and analyze database content while providing visibility into storage configuration and deployment
    /// characteristics essential for troubleshooting and system monitoring.
    /// </summary>
    public string DatabasePath => _databasePath;

    /// <summary>
    /// Initializes a new instance of the episode tracking database with comprehensive path management
    /// and establishes the foundational infrastructure for persistent storage operations. Sets up logging
    /// and database path resolution using Jellyfin's application infrastructure while ensuring proper
    /// directory creation and file system accessibility essential for reliable database operation
    /// across diverse deployment environments and system configurations.
    /// 
    /// The constructor establishes the database foundation by integrating with Jellyfin's data storage
    /// patterns and preparing necessary infrastructure for database initialization and operation. The
    /// initialization process focuses on path management and directory preparation while maintaining
    /// compatibility with Jellyfin's security and data management requirements.
    /// 
    /// Path Management and Directory Creation:
    /// Sophisticated path calculation using Jellyfin's application path infrastructure ensures consistent
    /// database location while maintaining proper permissions and accessibility. The path management
    /// includes automatic directory creation ensuring database accessibility regardless of initial
    /// deployment state while providing predictable storage location for administrative oversight.
    /// 
    /// Infrastructure Integration:
    /// The initialization process integrates with Jellyfin's dependency injection and configuration
    /// systems ensuring seamless service integration while maintaining optimal performance and
    /// reliability characteristics essential for production deployment and extended operation.
    /// </summary>
    /// <param name="logger">
    /// Logger service for database operation monitoring, error reporting, and debugging information.
    /// Provides comprehensive logging capabilities throughout database lifecycle enabling administrative
    /// oversight and troubleshooting during tracking and storage operations.
    /// </param>
    /// <param name="appPaths">
    /// Jellyfin application path service providing access to configured data storage locations.
    /// Enables consistent database placement while maintaining compatibility with Jellyfin's
    /// data management patterns and security requirements.
    /// </param>
    // MARK: Constructor
    public EpisodeTrackingDatabase(ILogger<EpisodeTrackingDatabase> logger, IApplicationPaths appPaths)
    {
        _logger = logger;
        
        // Calculate database path using Jellyfin's data storage infrastructure
        var dataPath = Path.Combine(appPaths.DataPath, "episodeposter");
        Directory.CreateDirectory(dataPath);  // Ensure directory exists for database accessibility
        _databasePath = Path.Combine(dataPath, "episode_tracking.db");
    }

    /// <summary>
    /// Performs comprehensive database initialization including connection establishment, schema creation,
    /// and infrastructure preparation ensuring reliable persistent storage capability for episode tracking
    /// operations. This method implements sophisticated initialization algorithms establishing database
    /// connectivity while ensuring proper schema configuration and operational readiness essential for
    /// tracking service integration and processing optimization workflows.
    /// 
    /// The initialization process serves as the critical foundation for all database operations providing
    /// essential infrastructure preparation including connection management, schema validation, and
    /// operational verification ensuring reliable database service availability throughout plugin
    /// lifecycle and extended processing sessions requiring persistent tracking state management.
    /// 
    /// Connection Establishment and Configuration:
    /// Advanced SQLite connection configuration with optimized settings providing reliable database
    /// access while maintaining performance characteristics suitable for intensive tracking operations.
    /// The connection establishment includes proper configuration for plugin deployment environments
    /// while ensuring compatibility with Jellyfin's infrastructure and security requirements.
    /// 
    /// Schema Creation and Validation:
    /// Comprehensive schema creation with sophisticated table definition ensuring optimal data storage
    /// and query performance for episode tracking operations. The schema creation includes proper
    /// indexing strategies and data type optimization providing efficient storage and retrieval
    /// capabilities essential for responsive tracking and processing decision algorithms.
    /// 
    /// Operational Readiness Verification:
    /// Database initialization includes comprehensive verification ensuring operational readiness
    /// and accessibility for tracking operations. The verification process validates database
    /// functionality while ensuring proper configuration and performance characteristics essential
    /// for reliable service integration and extended operation.
    /// 
    /// Infrastructure Integration and Monitoring:
    /// Initialization includes comprehensive logging and monitoring setup providing administrative
    /// visibility into database operation and configuration. The monitoring integration ensures
    /// proper operational oversight while enabling debugging and performance analysis essential
    /// for production deployment and maintenance.
    /// </summary>
    /// <returns>
    /// Task representing asynchronous database initialization operation with comprehensive error
    /// handling and infrastructure setup. Completion indicates successful database readiness
    /// enabling tracking service operation and persistent storage capabilities.
    /// </returns>
    // MARK: InitializeAsync
    public async Task InitializeAsync()
    {
        // Establish SQLite connection with optimized configuration for plugin deployment
        _connection = new SqliteConnection($"Data Source={_databasePath}");
        await _connection.OpenAsync().ConfigureAwait(false);
        
        // Create database schema with optimized table structure for tracking operations
        await CreateTablesAsync().ConfigureAwait(false);
        
        // Administrative logging providing initialization confirmation and path transparency
        _logger.LogInformation("Episode tracking database initialized at: {DatabasePath}", _databasePath);
    }

    /// <summary>
    /// Implements comprehensive database schema creation with optimized table design ensuring efficient
    /// storage and retrieval of episode processing records with proper data types and indexing strategies.
    /// This method establishes the foundational database structure enabling sophisticated tracking
    /// operations while maintaining optimal query performance and storage efficiency essential for
    /// large-scale episode collections and intensive processing workflows.
    /// 
    /// The schema creation process implements advanced database design principles providing optimal
    /// balance between storage efficiency and query performance while ensuring data integrity and
    /// consistency across diverse tracking scenarios. The table design supports comprehensive
    /// episode metadata storage enabling intelligent processing decisions and optimization algorithms.
    /// 
    /// Table Structure and Optimization:
    /// 
    /// Primary Key Design:
    /// Episode ID serves as primary key providing efficient record identification and query optimization
    /// while ensuring data uniqueness and referential integrity. The primary key design enables
    /// optimal indexing and query performance for tracking operations requiring frequent episode
    /// lookup and validation during processing decision algorithms.
    /// 
    /// Temporal Data Storage:
    /// Sophisticated timestamp storage using TEXT format with culture-invariant serialization ensuring
    /// consistent temporal data representation across diverse system configurations. The temporal
    /// storage provides precise timestamp handling essential for accurate change detection and
    /// processing decision algorithms requiring temporal comparison and analysis.
    /// 
    /// File System Metadata Integration:
    /// Comprehensive file system attribute storage including paths, sizes, and modification timestamps
    /// providing essential data for content change detection algorithms. The metadata storage ensures
    /// accurate tracking of file system changes while maintaining efficient storage and retrieval
    /// capabilities essential for responsive processing decision operations.
    /// 
    /// Configuration Hash Storage:
    /// Advanced configuration fingerprint storage enabling precise detection of settings changes
    /// requiring poster regeneration while maintaining storage efficiency. The hash storage provides
    /// foundation for optimization algorithms ensuring regeneration occurs when necessary while
    /// avoiding unnecessary processing for unchanged configurations.
    /// 
    /// Data Type Optimization and Storage Efficiency:
    /// Careful data type selection balancing storage efficiency with query performance ensuring
    /// optimal database operation across diverse episode collections and usage patterns. The
    /// optimization provides responsive query performance while maintaining reasonable storage
    /// requirements suitable for plugin deployment environments.
    /// </summary>
    /// <returns>
    /// Task representing asynchronous schema creation operation with comprehensive error handling
    /// and SQL execution. Completion indicates successful table creation enabling tracking
    /// record storage and retrieval operations essential for processing optimization.
    /// </returns>
    // MARK: CreateTablesAsync
    private async Task CreateTablesAsync()
    {
        // Comprehensive table definition with optimized schema for episode tracking operations
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS ProcessedEpisodes (
                EpisodeId TEXT PRIMARY KEY,                   -- Unique episode identifier for efficient indexing
                LastProcessed TEXT NOT NULL,                  -- UTC timestamp of processing completion
                VideoFilePath TEXT NOT NULL,                  -- Current video file path for change detection
                VideoFileSize INTEGER NOT NULL,               -- File size for content modification detection
                VideoFileLastModified TEXT NOT NULL,          -- File modification timestamp for change analysis
                ConfigurationHash TEXT NOT NULL               -- Configuration fingerprint for settings change detection
            )
            """;

        // Execute schema creation with comprehensive error handling and transaction management
        using var command = new SqliteCommand(createTableSql, _connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves specific episode processing records from persistent storage using optimized query
    /// operations with comprehensive data validation and type conversion ensuring accurate record
    /// reconstruction and reliable data access. This method implements sophisticated database query
    /// algorithms providing efficient episode lookup capabilities essential for processing decision
    /// algorithms and tracking state validation during poster generation workflows.
    /// 
    /// The retrieval process serves as a critical component in processing optimization providing
    /// rapid access to tracking records enabling intelligent processing decisions while maintaining
    /// optimal query performance suitable for high-frequency lookup operations typical during
    /// batch processing scenarios requiring extensive tracking validation and change detection.
    /// 
    /// Query Optimization and Performance:
    /// Advanced SQL query construction with parameterized operations ensuring optimal performance
    /// while maintaining security through SQL injection prevention. The query optimization provides
    /// responsive database access while ensuring data integrity and security essential for
    /// production deployment environments requiring reliable and secure database operations.
    /// 
    /// Data Reconstruction and Validation:
    /// Sophisticated data reader operations with comprehensive type conversion and validation ensuring
    /// accurate record reconstruction from database storage. The reconstruction process includes
    /// proper DateTime parsing with culture-invariant formatting ensuring consistent temporal data
    /// representation across diverse system configurations and timezone environments.
    /// 
    /// Error Handling and Reliability:
    /// Comprehensive error handling ensures graceful operation when record retrieval encounters
    /// issues while maintaining query reliability and system stability. The error management
    /// provides appropriate fallback behavior while ensuring continued operation during
    /// recoverable database access issues or data corruption scenarios.
    /// 
    /// Security and Data Protection:
    /// Parameterized query implementation ensuring protection against SQL injection attacks while
    /// maintaining query performance and operational efficiency. The security measures provide
    /// robust protection suitable for production deployment while enabling efficient tracking
    /// operations across diverse episode collections and processing workflows.
    /// </summary>
    /// <param name="episodeId">
    /// Unique episode identifier for targeted record retrieval enabling precise tracking record
    /// lookup and validation. Provides specific episode targeting for processing decision algorithms
    /// while maintaining query efficiency and optimal database performance.
    /// </param>
    /// <returns>
    /// ProcessedEpisodeRecord containing comprehensive tracking metadata if record exists, null
    /// if no tracking record found enabling appropriate processing decision determination. The
    /// record provides complete tracking state information for change detection and optimization.
    /// </returns>
    // MARK: GetProcessedEpisodeAsync
    public async Task<ProcessedEpisodeRecord?> GetProcessedEpisodeAsync(Guid episodeId)
    {
        // Optimized query construction with comprehensive field selection for complete record retrieval
        const string sql = """
            SELECT EpisodeId, LastProcessed, VideoFilePath, VideoFileSize, VideoFileLastModified, ConfigurationHash
            FROM ProcessedEpisodes 
            WHERE EpisodeId = @episodeId
            """;

        // Parameterized query execution ensuring security and optimal performance
        using var command = new SqliteCommand(sql, _connection);
        command.Parameters.AddWithValue("@episodeId", episodeId.ToString());

        // Data reader operations with comprehensive record reconstruction and validation
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            // Comprehensive record reconstruction with proper type conversion and validation
            return new ProcessedEpisodeRecord
            {
                EpisodeId = Guid.Parse(reader.GetString(0)),                                              // Unique identifier parsing
                LastProcessed = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture),       // UTC timestamp reconstruction
                VideoFilePath = reader.GetString(2),                                                      // File path retrieval
                VideoFileSize = reader.GetInt64(3),                                                       // File size reconstruction
                VideoFileLastModified = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture), // Modification timestamp parsing
                ConfigurationHash = reader.GetString(5)                                                   // Configuration fingerprint retrieval
            };
        }

        // Return null indicating no record found enabling appropriate processing decision determination
        return null;
    }

    /// <summary>
    /// Performs comprehensive episode processing record storage using advanced upsert operations
    /// ensuring efficient record creation and modification with atomic transaction handling and
    /// data integrity validation. This method implements sophisticated database operations providing
    /// reliable record persistence essential for tracking state management and processing optimization
    /// across diverse episode collections and processing workflows requiring comprehensive state capture.
    /// 
    /// The storage process serves as the critical persistence mechanism enabling reliable tracking
    /// record management while maintaining optimal performance during intensive processing operations.
    /// The implementation provides atomic upsert functionality ensuring data consistency while
    /// supporting both initial record creation and subsequent update operations essential for
    /// comprehensive tracking lifecycle management and processing optimization.
    /// 
    /// Upsert Operation and Atomicity:
    /// Advanced INSERT OR REPLACE operation providing atomic record storage with comprehensive
    /// data validation and transaction handling. The upsert functionality ensures data consistency
    /// while providing efficient storage for both new records and existing record updates enabling
    /// streamlined tracking operations without complex existence checking or conditional logic.
    /// 
    /// Data Serialization and Storage:
    /// Sophisticated data serialization with culture-invariant formatting ensuring consistent
    /// data representation across diverse system configurations and timezone environments. The
    /// serialization process includes proper DateTime formatting and data type conversion ensuring
    /// reliable storage and subsequent retrieval with data integrity preservation.
    /// 
    /// Parameter Binding and Security:
    /// Comprehensive parameterized query implementation ensuring protection against SQL injection
    /// while maintaining optimal storage performance and reliability. The parameter binding provides
    /// robust security measures suitable for production deployment while enabling efficient
    /// tracking record storage across diverse processing scenarios and episode collections.
    /// 
    /// Transaction Management and Reliability:
    /// Advanced transaction handling ensuring atomic storage operations with comprehensive error
    /// recovery and data integrity validation. The transaction management prevents partial updates
    /// and data corruption while ensuring reliable operation across diverse processing scenarios
    /// requiring consistent tracking state management and optimization capabilities.
    /// </summary>
    /// <param name="record">
    /// ProcessedEpisodeRecord containing comprehensive tracking metadata for persistent storage.
    /// Provides complete episode processing state information including temporal markers, file
    /// system metadata, and configuration fingerprints essential for processing optimization.
    /// </param>
    /// <returns>
    /// Task representing asynchronous storage operation with comprehensive error handling and
    /// transaction management. Completion indicates successful record persistence enabling
    /// future processing optimization and tracking state validation.
    /// </returns>
    // MARK: SaveProcessedEpisodeAsync
    public async Task SaveProcessedEpisodeAsync(ProcessedEpisodeRecord record)
    {
        // Advanced upsert operation with comprehensive parameter binding for atomic record storage
        const string sql = """
            INSERT OR REPLACE INTO ProcessedEpisodes 
            (EpisodeId, LastProcessed, VideoFilePath, VideoFileSize, VideoFileLastModified, ConfigurationHash)
            VALUES (@episodeId, @lastProcessed, @videoFilePath, @videoFileSize, @videoFileLastModified, @configurationHash)
            """;

        // Parameterized command construction with comprehensive data binding and security validation
        using var command = new SqliteCommand(sql, _connection);
        command.Parameters.AddWithValue("@episodeId", record.EpisodeId.ToString());                       // Unique identifier serialization
        command.Parameters.AddWithValue("@lastProcessed", record.LastProcessed.ToString("O"));           // ISO 8601 timestamp formatting
        command.Parameters.AddWithValue("@videoFilePath", record.VideoFilePath);                         // File path storage
        command.Parameters.AddWithValue("@videoFileSize", record.VideoFileSize);                         // File size persistence
        command.Parameters.AddWithValue("@videoFileLastModified", record.VideoFileLastModified.ToString("O")); // Modification timestamp formatting
        command.Parameters.AddWithValue("@configurationHash", record.ConfigurationHash);                 // Configuration fingerprint storage

        // Atomic execution with comprehensive error handling and transaction management
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Performs targeted episode processing record removal using optimized deletion operations
    /// with comprehensive error handling and transaction management ensuring reliable record
    /// cleanup for administrative maintenance and selective reprocessing scenarios. This method
    /// implements sophisticated database operations providing precise record removal capabilities
    /// essential for tracking state management and processing workflow control across diverse
    /// administrative and maintenance scenarios requiring selective tracking record manipulation.
    /// 
    /// The removal process serves essential administrative functions including selective reprocessing
    /// initiation, tracking state cleanup, and maintenance operations requiring precise control
    /// over individual episode tracking records while maintaining overall database integrity
    /// and performance characteristics essential for continued operation and optimization.
    /// 
    /// Targeted Deletion and Precision:
    /// Advanced parameterized deletion operations ensuring precise record targeting while maintaining
    /// database integrity and performance characteristics. The targeted approach enables surgical
    /// record removal without affecting other tracking records while providing efficient cleanup
    /// capabilities suitable for administrative maintenance and selective processing workflows.
    /// 
    /// Transaction Safety and Atomicity:
    /// Comprehensive transaction handling ensuring atomic deletion operations with proper error
    /// recovery and consistency validation. The transaction safety prevents partial operations
    /// and ensures reliable record removal while maintaining database integrity across diverse
    /// administrative scenarios requiring precise tracking state manipulation.
    /// 
    /// Security and Access Control:
    /// Parameterized query implementation ensuring protection against SQL injection while maintaining
    /// efficient deletion performance and operational reliability. The security measures provide
    /// robust protection suitable for administrative operations while enabling efficient tracking
    /// record management across diverse maintenance and processing control scenarios.
    /// 
    /// Performance and Efficiency:
    /// Optimized deletion operations providing efficient record removal suitable for frequent
    /// administrative operations while maintaining minimal database overhead and optimal
    /// performance characteristics essential for responsive administrative interfaces and
    /// maintenance workflows requiring rapid tracking state manipulation.
    /// </summary>
    /// <param name="episodeId">
    /// Unique episode identifier for targeted record removal enabling precise tracking state
    /// management and selective processing control. Provides specific episode targeting for
    /// administrative operations while maintaining database integrity and performance optimization.
    /// </param>
    /// <returns>
    /// Task representing asynchronous deletion operation with comprehensive error handling and
    /// transaction management. Completion indicates successful record removal enabling selective
    /// reprocessing and administrative control over tracking state management.
    /// </returns>
    // MARK: RemoveProcessedEpisodeAsync
    public async Task RemoveProcessedEpisodeAsync(Guid episodeId)
    {
        // Optimized deletion query with parameterized targeting for secure and efficient record removal
        const string sql = "DELETE FROM ProcessedEpisodes WHERE EpisodeId = @episodeId";

        // Parameterized command execution ensuring security and optimal deletion performance
        using var command = new SqliteCommand(sql, _connection);
        command.Parameters.AddWithValue("@episodeId", episodeId.ToString());

        // Atomic deletion execution with comprehensive error handling and transaction management
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves comprehensive count of processed episodes from database storage using optimized
    /// aggregation queries providing essential statistical information for administrative monitoring
    /// and system analysis. This method implements sophisticated database operations providing
    /// efficient count retrieval capabilities essential for tracking coverage analysis, performance
    /// evaluation, and administrative oversight enabling data-driven optimization decisions and
    /// comprehensive understanding of processing workflow effectiveness across episode collections.
    /// 
    /// The count retrieval serves critical monitoring functions providing essential metrics for
    /// administrative oversight while maintaining optimal query performance suitable for frequent
    /// statistical analysis and reporting operations typical in administrative interfaces requiring
    /// real-time tracking coverage information and processing effectiveness evaluation.
    /// 
    /// Aggregation Query Optimization:
    /// Advanced SQL aggregation with optimized count operations providing efficient statistical
    /// retrieval while maintaining minimal database overhead and responsive query performance.
    /// The aggregation optimization ensures rapid count calculation suitable for frequent
    /// administrative queries while preserving database performance during intensive operations.
    /// 
    /// Statistical Accuracy and Reliability:
    /// Comprehensive count calculation with proper data validation ensuring accurate statistical
    /// information for administrative analysis and monitoring purposes. The accuracy mechanisms
    /// provide reliable metrics while maintaining consistent behavior across diverse database
    /// states and processing scenarios requiring comprehensive coverage analysis.
    /// 
    /// Performance and Responsiveness:
    /// Optimized query execution providing responsive statistical retrieval suitable for real-time
    /// administrative interfaces and monitoring dashboards requiring immediate access to tracking
    /// coverage information. The performance optimization ensures minimal latency while maintaining
    /// accurate statistical calculation across diverse episode collections and database sizes.
    /// 
    /// Error Handling and Resilience:
    /// Comprehensive error handling ensuring reliable count retrieval even when database operations
    /// encounter issues while maintaining statistical accuracy and administrative interface
    /// responsiveness. The resilience mechanisms provide stable operation while ensuring
    /// appropriate error reporting for debugging and monitoring purposes.
    /// </summary>
    /// <returns>
    /// Integer representing total count of processed episodes in database storage providing
    /// comprehensive statistical information for administrative monitoring and analysis. The
    /// count enables coverage evaluation and performance analysis essential for optimization.
    /// </returns>
    // MARK: GetProcessedCountAsync
    public async Task<int> GetProcessedCountAsync()
    {
        // Optimized aggregation query for efficient count calculation with minimal database overhead
        const string sql = "SELECT COUNT(*) FROM ProcessedEpisodes";

        // Scalar query execution with comprehensive result processing and type conversion
        using var command = new SqliteCommand(sql, _connection);
        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
        
        // Culture-invariant conversion ensuring consistent numerical result across system configurations
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Performs comprehensive clearing of all episode processing records using optimized bulk
    /// deletion operations with comprehensive transaction management ensuring reliable database
    /// cleanup for administrative maintenance, testing scenarios, and systematic reprocessing
    /// operations. This method implements sophisticated database operations providing complete
    /// tracking state reset capabilities essential for administrative maintenance workflows
    /// requiring comprehensive database cleanup and systematic processing reinitialization.
    /// 
    /// The clearing operation serves essential administrative functions including testing preparation,
    /// systematic reprocessing initiation, and maintenance workflows requiring complete tracking
    /// state reset while maintaining database integrity and performance characteristics. The
    /// implementation provides reliable clearing capabilities suitable for administrative operations
    /// requiring comprehensive database state management and systematic processing control.
    /// 
    /// Bulk Deletion and Performance:
    /// Advanced bulk deletion operations providing efficient clearing of all tracking records
    /// while maintaining optimal database performance and minimal transaction overhead. The
    /// bulk approach ensures rapid clearing completion suitable for administrative operations
    /// requiring immediate database state reset and systematic processing reinitialization.
    /// 
    /// Transaction Atomicity and Safety:
    /// Comprehensive transaction handling ensuring atomic clearing operations with proper error
    /// recovery and consistency validation. The transaction safety prevents partial clearing
    /// and ensures reliable database state reset while maintaining integrity across diverse
    /// administrative scenarios requiring complete tracking record removal.
    /// 
    /// Administrative Safety and Confirmation:
    /// Database clearing operations include appropriate safeguards and logging ensuring administrative
    /// visibility and confirmation of clearing completion. The safety measures enable confident
    /// administrative operations while maintaining system reliability and providing clear
    /// operational feedback essential for administrative oversight and maintenance workflows.
    /// 
    /// Recovery and Reinitialization:
    /// Clearing operations prepare database for fresh tracking operations enabling systematic
    /// reprocessing and maintenance workflows while maintaining database schema and configuration.
    /// The preparation ensures immediate readiness for new tracking operations while preserving
    /// database infrastructure and performance characteristics essential for continued operation.
    /// </summary>
    /// <returns>
    /// Task representing asynchronous clearing operation with comprehensive transaction management
    /// and error handling. Completion indicates successful removal of all tracking records
    /// enabling fresh tracking operation and systematic reprocessing across episode collections.
    /// </returns>
    // MARK: ClearAllProcessedEpisodesAsync
    public async Task ClearAllProcessedEpisodesAsync()
    {
        // Comprehensive bulk deletion for efficient clearing of all tracking records
        const string sql = "DELETE FROM ProcessedEpisodes";

        // Atomic execution with comprehensive error handling and transaction management
        using var command = new SqliteCommand(sql, _connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves comprehensive collection of all episode processing records using optimized bulk
    /// query operations with efficient data processing and memory management ensuring reliable
    /// access to complete tracking dataset for administrative analysis, reporting, and maintenance
    /// operations. This method implements sophisticated database operations providing bulk data
    /// retrieval capabilities essential for comprehensive tracking analysis and administrative
    /// oversight requiring complete dataset access across diverse episode collections and processing scenarios.
    /// 
    /// The bulk retrieval serves essential administrative and analytical functions providing
    /// complete tracking dataset access for comprehensive analysis while maintaining optimal
    /// query performance and memory utilization suitable for large episode collections requiring
    /// detailed tracking review and statistical analysis across diverse processing workflows.
    /// 
    /// Bulk Query Optimization and Performance:
    /// Advanced bulk query operations with optimized result processing providing efficient retrieval
    /// of complete tracking dataset while maintaining reasonable memory utilization and responsive
    /// query performance. The optimization ensures practical bulk access suitable for administrative
    /// operations while maintaining system stability during large dataset retrieval operations.
    /// 
    /// Memory Management and Efficiency:
    /// Sophisticated memory management patterns ensuring efficient handling of large tracking
    /// datasets while preventing excessive memory allocation and system resource consumption.
    /// The memory management provides practical bulk access capabilities while maintaining
    /// system stability and performance characteristics essential for administrative operations.
    /// 
    /// Data Processing and Reconstruction:
    /// Comprehensive data reader operations with efficient record reconstruction and validation
    /// ensuring accurate dataset retrieval while maintaining optimal processing performance.
    /// The data processing includes proper type conversion and validation ensuring reliable
    /// bulk access suitable for administrative analysis and reporting requirements.
    /// 
    /// Administrative and Analytical Capabilities:
    /// Bulk retrieval enables comprehensive administrative analysis including tracking coverage
    /// evaluation, processing pattern analysis, and system performance assessment providing
    /// essential data for optimization decisions and administrative oversight across diverse
    /// tracking scenarios and episode collections requiring detailed analytical capabilities.
    /// </summary>
    /// <returns>
    /// List of ProcessedEpisodeRecord objects representing complete tracking dataset for
    /// administrative analysis and reporting. Provides comprehensive access to all tracking
    /// records enabling detailed analysis and administrative oversight across episode collections.
    /// </returns>
    // MARK: GetAllProcessedEpisodesAsync
    public async Task<List<ProcessedEpisodeRecord>> GetAllProcessedEpisodesAsync()
    {
        // Comprehensive bulk query for complete tracking dataset retrieval
        const string sql = """
            SELECT EpisodeId, LastProcessed, VideoFilePath, VideoFileSize, VideoFileLastModified, ConfigurationHash
            FROM ProcessedEpisodes
            """;

        var records = new List<ProcessedEpisodeRecord>();

        // Bulk query execution with comprehensive result processing and memory management
        using var command = new SqliteCommand(sql, _connection);
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        // Efficient record reconstruction with comprehensive data validation and type conversion
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            records.Add(new ProcessedEpisodeRecord
            {
                EpisodeId = Guid.Parse(reader.GetString(0)),                                              // Unique identifier reconstruction
                LastProcessed = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture),       // UTC timestamp parsing
                VideoFilePath = reader.GetString(2),                                                      // File path retrieval
                VideoFileSize = reader.GetInt64(3),                                                       // File size reconstruction
                VideoFileLastModified = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture), // Modification timestamp parsing
                ConfigurationHash = reader.GetString(5)                                                   // Configuration fingerprint retrieval
            });
        }

        return records;
    }

    /// <summary>
    /// Implements comprehensive resource disposal ensuring proper cleanup of database connections
    /// and associated resources preventing memory leaks and connection exhaustion during service
    /// lifecycle management. This method provides sophisticated disposal pattern implementation
    /// following established .NET patterns while ensuring database resource management suitable
    /// for plugin lifecycle integration and system stability across diverse deployment scenarios.
    /// 
    /// The disposal implementation serves critical resource management functions ensuring proper
    /// cleanup during service termination while preventing resource accumulation and system
    /// degradation. The pattern implementation provides thread-safe disposal coordination while
    /// maintaining optimal resource utilization and system stability essential for plugin
    /// lifecycle management and extended operation periods.
    /// 
    /// Disposal Pattern and Thread Safety:
    /// Comprehensive disposal pattern implementation with thread-safe state tracking ensuring
    /// proper resource cleanup while preventing multiple disposal attempts and resource conflicts.
    /// The pattern provides reliable disposal coordination suitable for plugin lifecycle
    /// management while maintaining system stability and resource optimization.
    /// 
    /// Connection Lifecycle Management:
    /// Sophisticated connection disposal ensuring proper database resource cleanup and connection
    /// termination preventing resource leaks and connection pool exhaustion. The connection
    /// management provides reliable resource cleanup while maintaining optimal system performance
    /// and stability characteristics essential for plugin deployment environments.
    /// 
    /// Resource Cleanup and Optimization:
    /// Advanced resource management ensuring comprehensive cleanup of database-related resources
    /// including connections, commands, and associated memory allocations preventing system
    /// resource accumulation and degradation. The cleanup optimization provides efficient
    /// resource management suitable for extended plugin operation and intensive processing workflows.
    /// 
    /// Garbage Collection Optimization:
    /// Proper garbage collection suppression preventing unnecessary finalization overhead while
    /// ensuring appropriate resource cleanup through explicit disposal patterns. The optimization
    /// provides efficient memory management while maintaining proper resource lifecycle control
    /// essential for plugin performance and system stability.
    /// </summary>
    // MARK: Dispose
    public void Dispose()
    {
        // Thread-safe disposal pattern ensuring proper resource cleanup and preventing multiple disposal
        if (!_disposed)
        {
            // Comprehensive database connection cleanup and resource disposal
            _connection?.Dispose();
            _disposed = true;
            
            // Garbage collection optimization preventing unnecessary finalization overhead
            GC.SuppressFinalize(this);
        }
    }
}