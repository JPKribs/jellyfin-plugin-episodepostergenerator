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
/// SQLite database service for persistent episode tracking storage
/// </summary>
public sealed class EpisodeTrackingDatabase : IDisposable
{
    /// <summary>
    /// Logger for database operations and error reporting
    /// </summary>
    private readonly ILogger<EpisodeTrackingDatabase> _logger;

    /// <summary>
    /// File path to the SQLite database
    /// </summary>
    private readonly string _databasePath;

    /// <summary>
    /// SQLite connection for database operations
    /// </summary>
    private SqliteConnection? _connection;

    /// <summary>
    /// Tracks disposal state to prevent multiple disposal
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Gets the database file path for administrative purposes
    /// </summary>
    public string DatabasePath => _databasePath;

    // MARK: Constructor
    public EpisodeTrackingDatabase(ILogger<EpisodeTrackingDatabase> logger, IApplicationPaths appPaths)
    {
        _logger = logger;
        
        var dataPath = Path.Combine(appPaths.DataPath, "episodeposter");
        Directory.CreateDirectory(dataPath);
        _databasePath = Path.Combine(dataPath, "episode_tracking.db");
    }

    // MARK: InitializeAsync
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection($"Data Source={_databasePath}");
        await _connection.OpenAsync().ConfigureAwait(false);
        
        await CreateTablesAsync().ConfigureAwait(false);
        
        _logger.LogInformation("Episode tracking database initialized at: {DatabasePath}", _databasePath);
    }

    // MARK: CreateTablesAsync
    private async Task CreateTablesAsync()
    {
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS ProcessedEpisodes (
                EpisodeId TEXT PRIMARY KEY,
                LastProcessed TEXT NOT NULL,
                VideoFilePath TEXT NOT NULL,
                VideoFileSize INTEGER NOT NULL,
                VideoFileLastModified TEXT NOT NULL,
                ConfigurationHash TEXT NOT NULL
            )
            """;

        using var command = new SqliteCommand(createTableSql, _connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    // MARK: GetProcessedEpisodeAsync
    public async Task<ProcessedEpisodeRecord?> GetProcessedEpisodeAsync(Guid episodeId)
    {
        const string sql = """
            SELECT EpisodeId, LastProcessed, VideoFilePath, VideoFileSize, VideoFileLastModified, ConfigurationHash
            FROM ProcessedEpisodes 
            WHERE EpisodeId = @episodeId
            """;

        using var command = new SqliteCommand(sql, _connection);
        command.Parameters.AddWithValue("@episodeId", episodeId.ToString());

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            return new ProcessedEpisodeRecord
            {
                EpisodeId = Guid.Parse(reader.GetString(0)),
                LastProcessed = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                VideoFilePath = reader.GetString(2),
                VideoFileSize = reader.GetInt64(3),
                VideoFileLastModified = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                ConfigurationHash = reader.GetString(5)
            };
        }

        return null;
    }

    // MARK: SaveProcessedEpisodeAsync
    public async Task SaveProcessedEpisodeAsync(ProcessedEpisodeRecord record)
    {
        const string sql = """
            INSERT OR REPLACE INTO ProcessedEpisodes 
            (EpisodeId, LastProcessed, VideoFilePath, VideoFileSize, VideoFileLastModified, ConfigurationHash)
            VALUES (@episodeId, @lastProcessed, @videoFilePath, @videoFileSize, @videoFileLastModified, @configurationHash)
            """;

        using var command = new SqliteCommand(sql, _connection);
        command.Parameters.AddWithValue("@episodeId", record.EpisodeId.ToString());
        command.Parameters.AddWithValue("@lastProcessed", record.LastProcessed.ToString("O"));
        command.Parameters.AddWithValue("@videoFilePath", record.VideoFilePath);
        command.Parameters.AddWithValue("@videoFileSize", record.VideoFileSize);
        command.Parameters.AddWithValue("@videoFileLastModified", record.VideoFileLastModified.ToString("O"));
        command.Parameters.AddWithValue("@configurationHash", record.ConfigurationHash);

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    // MARK: RemoveProcessedEpisodeAsync
    public async Task RemoveProcessedEpisodeAsync(Guid episodeId)
    {
        const string sql = "DELETE FROM ProcessedEpisodes WHERE EpisodeId = @episodeId";

        using var command = new SqliteCommand(sql, _connection);
        command.Parameters.AddWithValue("@episodeId", episodeId.ToString());

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    // MARK: GetProcessedCountAsync
    public async Task<int> GetProcessedCountAsync()
    {
        const string sql = "SELECT COUNT(*) FROM ProcessedEpisodes";

        using var command = new SqliteCommand(sql, _connection);
        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
        
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    // MARK: ClearAllProcessedEpisodesAsync
    public async Task ClearAllProcessedEpisodesAsync()
    {
        const string sql = "DELETE FROM ProcessedEpisodes";

        using var command = new SqliteCommand(sql, _connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    // MARK: GetAllProcessedEpisodesAsync
    public async Task<List<ProcessedEpisodeRecord>> GetAllProcessedEpisodesAsync()
    {
        const string sql = """
            SELECT EpisodeId, LastProcessed, VideoFilePath, VideoFileSize, VideoFileLastModified, ConfigurationHash
            FROM ProcessedEpisodes
            """;

        var records = new List<ProcessedEpisodeRecord>();

        using var command = new SqliteCommand(sql, _connection);
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            records.Add(new ProcessedEpisodeRecord
            {
                EpisodeId = Guid.Parse(reader.GetString(0)),
                LastProcessed = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                VideoFilePath = reader.GetString(2),
                VideoFileSize = reader.GetInt64(3),
                VideoFileLastModified = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                ConfigurationHash = reader.GetString(5)
            });
        }

        return records;
    }

    // MARK: Dispose
    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}