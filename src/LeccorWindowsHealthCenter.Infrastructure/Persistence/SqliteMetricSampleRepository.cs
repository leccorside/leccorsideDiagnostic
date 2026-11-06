using System.Globalization;
using LeccorWindowsHealthCenter.Application.Persistence;
using Microsoft.Data.Sqlite;

namespace LeccorWindowsHealthCenter.Infrastructure.Persistence;

/// <summary>
/// Repositório de amostras de telemetria persistidas no SQLite (Pacote A).
/// </summary>
public sealed class SqliteMetricSampleRepository : IMetricSampleRepository
{
    private readonly SqliteDatabase _database;

    public SqliteMetricSampleRepository(SqliteDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task SaveSampleAsync(MetricSampleRecord sample, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sample);

        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO MetricSamples (Timestamp, CpuPercent, MemoryUsedPercent, MemoryUsedBytes, CommitUsedPercent, CommitUsedBytes)
            VALUES (@timestamp, @cpu, @memPct, @memBytes, @commitPct, @commitBytes);
            """;
        command.Parameters.AddWithValue("@timestamp", sample.Timestamp.ToString("o", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@cpu", sample.CpuPercent);
        command.Parameters.AddWithValue("@memPct", sample.MemoryUsedPercent);
        command.Parameters.AddWithValue("@memBytes", sample.MemoryUsedBytes);
        command.Parameters.AddWithValue("@commitPct", sample.CommitUsedPercent);
        command.Parameters.AddWithValue("@commitBytes", sample.CommitUsedBytes);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MetricSampleRecord>> GetRecentSamplesAsync(TimeSpan window, CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow - window;
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Timestamp, CpuPercent, MemoryUsedPercent, MemoryUsedBytes, CommitUsedPercent, CommitUsedBytes
            FROM MetricSamples
            WHERE Timestamp >= @since
            ORDER BY Timestamp ASC;
            """;
        command.Parameters.AddWithValue("@since", since.ToString("o", CultureInfo.InvariantCulture));

        var list = new List<MetricSampleRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new MetricSampleRecord
            {
                Id = reader.GetInt64(0),
                Timestamp = DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                CpuPercent = reader.GetDouble(2),
                MemoryUsedPercent = reader.GetDouble(3),
                MemoryUsedBytes = reader.GetInt64(4),
                CommitUsedPercent = reader.GetDouble(5),
                CommitUsedBytes = reader.GetInt64(6),
            });
        }

        return list;
    }

    public async Task PruneOlderThanAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM MetricSamples WHERE Timestamp < @threshold;";
        command.Parameters.AddWithValue("@threshold", threshold.ToString("o", CultureInfo.InvariantCulture));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

