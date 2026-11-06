using System.Globalization;
using System.Text.Json;
using LeccorWindowsHealthCenter.Application.Storage;
using Microsoft.Data.Sqlite;

namespace LeccorWindowsHealthCenter.Infrastructure.Persistence;

/// <summary>
/// Repositório SQLite para persistência e carregamento rápido de snapshots da análise de espaço em disco (Pacote B).
/// </summary>
public sealed class SqliteSpaceAnalysisSnapshotRepository : ISpaceAnalysisSnapshotRepository
{
    private readonly SqliteDatabase _database;

    public SqliteSpaceAnalysisSnapshotRepository(SqliteDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task SaveSnapshotAsync(string volumeRoot, SpaceAnalysis analysis, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(volumeRoot);
        ArgumentNullException.ThrowIfNull(analysis);

        var dataJson = JsonSerializer.Serialize(analysis);
        var normRoot = volumeRoot.TrimEnd('\\').ToUpperInvariant();

        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO SpaceAnalysisSnapshot (VolumeRoot, CapturedAt, TotalSizeBytes, TotalFilesCount, DurationMs, DataJson)
            VALUES (@volumeRoot, @capturedAt, @totalBytes, @totalFiles, @durationMs, @dataJson);
            """;
        command.Parameters.AddWithValue("@volumeRoot", normRoot);
        command.Parameters.AddWithValue("@capturedAt", analysis.CapturedAt.ToString("o", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@totalBytes", analysis.Root.SizeBytes);
        command.Parameters.AddWithValue("@totalFiles", analysis.Root.FileCount);
        command.Parameters.AddWithValue("@durationMs", (long)analysis.Duration.TotalMilliseconds);
        command.Parameters.AddWithValue("@dataJson", dataJson);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        // Manter no máximo os últimos 10 snapshots por volume
        await using var pruneCmd = connection.CreateCommand();
        pruneCmd.CommandText = """
            DELETE FROM SpaceAnalysisSnapshot
            WHERE VolumeRoot = @volumeRoot AND Id NOT IN (
                SELECT Id FROM SpaceAnalysisSnapshot
                WHERE VolumeRoot = @volumeRoot
                ORDER BY CapturedAt DESC
                LIMIT 10
            );
            """;
        pruneCmd.Parameters.AddWithValue("@volumeRoot", normRoot);
        await pruneCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SpaceAnalysis?> GetLatestSnapshotAsync(string volumeRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(volumeRoot);
        var normRoot = volumeRoot.TrimEnd('\\').ToUpperInvariant();

        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DataJson
            FROM SpaceAnalysisSnapshot
            WHERE VolumeRoot = @volumeRoot
            ORDER BY CapturedAt DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@volumeRoot", normRoot);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null or DBNull)
        {
            return null;
        }

        var json = Convert.ToString(result, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<SpaceAnalysis>(json);
    }

    public async Task<IReadOnlyList<SpaceAnalysisSnapshotSummary>> GetSnapshotsHistoryAsync(string volumeRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(volumeRoot);
        var normRoot = volumeRoot.TrimEnd('\\').ToUpperInvariant();

        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, VolumeRoot, CapturedAt, TotalSizeBytes, TotalFilesCount, DurationMs
            FROM SpaceAnalysisSnapshot
            WHERE VolumeRoot = @volumeRoot
            ORDER BY CapturedAt DESC;
            """;
        command.Parameters.AddWithValue("@volumeRoot", normRoot);

        var list = new List<SpaceAnalysisSnapshotSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new SpaceAnalysisSnapshotSummary(
                reader.GetInt64(0),
                reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
                reader.GetInt64(3),
                reader.GetInt64(4),
                TimeSpan.FromMilliseconds(reader.GetInt64(5))));
        }

        return list;
    }

    public async Task DeleteSnapshotAsync(long snapshotId, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SpaceAnalysisSnapshot WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", snapshotId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

