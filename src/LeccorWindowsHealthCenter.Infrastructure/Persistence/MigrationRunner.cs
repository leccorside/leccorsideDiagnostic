using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.Persistence;

/// <summary>Aplica migrations pendentes, cada uma em sua própria transação.</summary>
public sealed class MigrationRunner
{
    private readonly SqliteDatabase _database;
    private readonly ILogger<MigrationRunner> _logger;
    private readonly IReadOnlyList<Migration> _migrations;

    public MigrationRunner(SqliteDatabase database, ILogger<MigrationRunner> logger, IReadOnlyList<Migration>? migrations = null)
    {
        _database = database;
        _logger = logger;
        _migrations = migrations ?? Migrations.All;
    }

    /// <summary>Retorna a versão final do esquema.</summary>
    public async Task<int> MigrateAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_database.DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(connection, null, "PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS SchemaMigrations (
                Version   INTEGER PRIMARY KEY,
                Name      TEXT NOT NULL,
                AppliedAt TEXT NOT NULL
            );
            """, cancellationToken).ConfigureAwait(false);

        var current = await GetCurrentVersionAsync(connection, cancellationToken).ConfigureAwait(false);
        foreach (var migration in _migrations.Where(m => m.Version > current).OrderBy(m => m.Version))
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await ExecuteAsync(connection, transaction, migration.Sql, cancellationToken).ConfigureAwait(false);

                await using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO SchemaMigrations (Version, Name, AppliedAt) VALUES ($v, $n, $a);";
                insert.Parameters.AddWithValue("$v", migration.Version);
                insert.Parameters.AddWithValue("$n", migration.Name);
                insert.Parameters.AddWithValue("$a", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                current = migration.Version;
                _logger.LogInformation("Migration {Version} ({Name}) aplicada", migration.Version, migration.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao aplicar a migration {Version} ({Name})", migration.Version, migration.Name);
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }

        return current;
    }

    private static async Task<int> GetCurrentVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(Version), 0) FROM SchemaMigrations;";
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
