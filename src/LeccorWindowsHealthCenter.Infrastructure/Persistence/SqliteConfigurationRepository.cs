using System.Globalization;
using LeccorWindowsHealthCenter.Application.Settings;
using Microsoft.Data.Sqlite;

namespace LeccorWindowsHealthCenter.Infrastructure.Persistence;

/// <summary>
/// Implementação do repositório de configurações usando a tabela Configuration do SQLite (seção 66).
/// </summary>
public sealed class SqliteConfigurationRepository : IConfigurationRepository
{
    private readonly SqliteDatabase _database;

    public SqliteConfigurationRepository(SqliteDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Configuration WHERE Key = @key LIMIT 1;";
        command.Parameters.AddWithValue("@key", key);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null or DBNull ? null : Convert.ToString(result, CultureInfo.InvariantCulture);
    }

    public async Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Configuration (Key, Value, UpdatedAt)
            VALUES (@key, @value, @now)
            ON CONFLICT(Key) DO UPDATE SET Value = @value, UpdatedAt = @now;
            """;
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Value FROM Configuration;";

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var k = reader.GetString(0);
            var v = reader.GetString(1);
            dict[k] = v;
        }

        return dict;
    }
}

