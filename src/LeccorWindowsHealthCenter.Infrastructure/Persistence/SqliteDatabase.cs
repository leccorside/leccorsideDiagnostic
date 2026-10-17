using Microsoft.Data.Sqlite;

namespace LeccorWindowsHealthCenter.Infrastructure.Persistence;

/// <summary>Fábrica de conexões do banco SQLite local (seção 40). Nada sai da máquina.</summary>
public sealed class SqliteDatabase
{
    public SqliteDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = databasePath;
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = true,
        }.ToString();
    }

    public string DatabasePath { get; }

    public string ConnectionString { get; }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
