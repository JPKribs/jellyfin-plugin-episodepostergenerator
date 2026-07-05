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

public sealed class EpisodeTrackingDatabase : IDisposable
{
    private readonly ILogger<EpisodeTrackingDatabase> _logger;
    private readonly string _databasePath;
    private readonly string _connectionString;
    private bool _disposed;
    private bool _initialized;

    public string DatabasePath => _databasePath;

    // EpisodeTrackingDatabase
    // Initializes the database service with the path to the SQLite database file.
    public EpisodeTrackingDatabase(ILogger<EpisodeTrackingDatabase> logger, IApplicationPaths appPaths)
    {
        _logger = logger;

        var dataPath = Path.Combine(appPaths.DataPath, "episodeposter");
        Directory.CreateDirectory(dataPath);
        _databasePath = Path.Combine(dataPath, "episode_tracking.db");

        // Pooled connections (the Microsoft.Data.Sqlite default) let concurrent poster
        // workers each open a cheap connection instead of sharing a single SqliteConnection,
        // which is not safe for concurrent use.
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath
        }.ToString();
    }

    // InitializeAsync
    // Creates required tables and enables WAL journaling for cheap concurrent access.
    public async Task InitializeAsync()
    {
        using var connection = await OpenConnectionAsync().ConfigureAwait(false);

        // WAL is persistent (stored in the database file), so setting it once here covers
        // every pooled connection and avoids a full fsync on each per-episode commit.
        using (var walCommand = new SqliteCommand("PRAGMA journal_mode=WAL;", connection))
        {
            await walCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

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

        using (var command = new SqliteCommand(createTableSql, connection))
        {
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        _initialized = true;
        _logger.LogInformation("Episode tracking database initialized at: {DatabasePath}", _databasePath);
    }

    // OpenConnectionAsync
    // Opens a pooled connection to the tracking database.
    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        return connection;
    }

    // EnsureInitialized
    // Throws if the database has not been initialized.
    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Episode tracking database has not been initialized. Call InitializeAsync() first.");
        }
    }

    // GetProcessedEpisodeAsync
    // Retrieves a processed episode record by its ID from the database.
    public async Task<ProcessedEpisodeRecord?> GetProcessedEpisodeAsync(Guid episodeId)
    {
        EnsureInitialized();

        const string sql = """
            SELECT EpisodeId, LastProcessed, VideoFilePath, VideoFileSize, VideoFileLastModified, ConfigurationHash
            FROM ProcessedEpisodes
            WHERE EpisodeId = @episodeId
            """;

        using var connection = await OpenConnectionAsync().ConfigureAwait(false);
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@episodeId", episodeId.ToString());

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            return ReadRecord(reader);
        }

        return null;
    }

    // SaveProcessedEpisodeAsync
    // Saves or updates a processed episode record in the database.
    public async Task SaveProcessedEpisodeAsync(ProcessedEpisodeRecord record)
    {
        EnsureInitialized();

        const string sql = """
            INSERT OR REPLACE INTO ProcessedEpisodes
            (EpisodeId, LastProcessed, VideoFilePath, VideoFileSize, VideoFileLastModified, ConfigurationHash)
            VALUES (@episodeId, @lastProcessed, @videoFilePath, @videoFileSize, @videoFileLastModified, @configurationHash)
            """;

        using var connection = await OpenConnectionAsync().ConfigureAwait(false);
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@episodeId", record.EpisodeId.ToString());
        command.Parameters.AddWithValue("@lastProcessed", record.LastProcessed.ToString("O"));
        command.Parameters.AddWithValue("@videoFilePath", record.VideoFilePath);
        command.Parameters.AddWithValue("@videoFileSize", record.VideoFileSize);
        command.Parameters.AddWithValue("@videoFileLastModified", record.VideoFileLastModified.ToString("O"));
        command.Parameters.AddWithValue("@configurationHash", record.ConfigurationHash);

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    // RemoveProcessedEpisodeAsync
    // Removes a processed episode record from the database by ID.
    public async Task RemoveProcessedEpisodeAsync(Guid episodeId)
    {
        EnsureInitialized();

        const string sql = "DELETE FROM ProcessedEpisodes WHERE EpisodeId = @episodeId";

        using var connection = await OpenConnectionAsync().ConfigureAwait(false);
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@episodeId", episodeId.ToString());

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    // GetProcessedCountAsync
    // Returns the total count of processed episodes in the database.
    public async Task<int> GetProcessedCountAsync()
    {
        EnsureInitialized();

        const string sql = "SELECT COUNT(*) FROM ProcessedEpisodes";

        using var connection = await OpenConnectionAsync().ConfigureAwait(false);
        using var command = new SqliteCommand(sql, connection);
        var result = await command.ExecuteScalarAsync().ConfigureAwait(false);

        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    // ClearAllProcessedEpisodesAsync
    // Deletes all processed episode records from the database.
    public async Task ClearAllProcessedEpisodesAsync()
    {
        EnsureInitialized();

        const string sql = "DELETE FROM ProcessedEpisodes";

        using var connection = await OpenConnectionAsync().ConfigureAwait(false);
        using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    // GetAllProcessedEpisodesAsync
    // Retrieves all processed episode records from the database.
    public async Task<List<ProcessedEpisodeRecord>> GetAllProcessedEpisodesAsync()
    {
        EnsureInitialized();

        const string sql = """
            SELECT EpisodeId, LastProcessed, VideoFilePath, VideoFileSize, VideoFileLastModified, ConfigurationHash
            FROM ProcessedEpisodes
            """;

        var records = new List<ProcessedEpisodeRecord>();

        using var connection = await OpenConnectionAsync().ConfigureAwait(false);
        using var command = new SqliteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            records.Add(ReadRecord(reader));
        }

        return records;
    }

    // ReadRecord
    // Materializes a ProcessedEpisodeRecord from the current reader row.
    private static ProcessedEpisodeRecord ReadRecord(SqliteDataReader reader)
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

    // Dispose
    // Releases pooled connections held against this plugin's database file only —
    // ClearAllPools would clear every SQLite pool in the Jellyfin process.
    public void Dispose()
    {
        if (!_disposed)
        {
            _initialized = false;
            using (var connection = new SqliteConnection(_connectionString))
            {
                SqliteConnection.ClearPool(connection);
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
