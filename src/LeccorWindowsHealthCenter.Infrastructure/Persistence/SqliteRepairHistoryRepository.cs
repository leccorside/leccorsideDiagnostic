using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;
using Microsoft.Data.Sqlite;

namespace LeccorWindowsHealthCenter.Infrastructure.Persistence;

/// <summary>Histórico de Correções (seção 37) e registro de rollbacks.</summary>
public sealed class SqliteRepairHistoryRepository : IRepairHistoryRepository
{
    private const string SelectColumns = """
        SELECT Id, ActionId, ActionTitle, FindingId, Risk, Outcome, StartedAt, CompletedAt, UserName, Reason, Message,
               BackupId, SupportsRollback, ChangesJson, ComparisonJson, RolledBackAt
        FROM Repair
        """;

    private readonly SqliteDatabase _database;

    public SqliteRepairHistoryRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public async Task AddAsync(RepairRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Repair (Id, ActionId, ActionTitle, FindingId, Risk, Outcome, StartedAt, CompletedAt, UserName, Reason, Message,
                                BackupId, SupportsRollback, ChangesJson, ComparisonJson, RolledBackAt)
            VALUES ($id, $action, $title, $finding, $risk, $outcome, $started, $completed, $user, $reason, $message,
                    $backup, $rollback, $changes, $comparison, $rolledBack);
            """;
        command.Add("$id", record.Id.ToString());
        command.Add("$action", record.ActionId);
        command.Add("$title", record.ActionTitle);
        command.Add("$finding", record.FindingId?.ToString());
        command.Add("$risk", (int)record.Risk);
        command.Add("$outcome", record.Outcome.ToString());
        command.Add("$started", DbConvert.Date(record.StartedAt));
        command.Add("$completed", DbConvert.DateOrNull(record.CompletedAt));
        command.Add("$user", record.User);
        command.Add("$reason", record.Reason);
        command.Add("$message", record.Message);
        command.Add("$backup", record.BackupId?.ToString());
        command.Add("$rollback", record.SupportsRollback);
        command.Add("$changes", DbConvert.ToJson(record.Changes));
        command.Add("$comparison", record.Comparison is null ? null : DbConvert.ToJson(record.Comparison));
        command.Add("$rolledBack", DbConvert.DateOrNull(record.RolledBackAt));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkRolledBackAsync(Guid recordId, DateTimeOffset rolledBackAt, string message, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE Repair SET RolledBackAt = $at, Outcome = $outcome WHERE Id = $id;";
            update.Add("$at", DbConvert.Date(rolledBackAt));
            update.Add("$outcome", RepairOutcome.RolledBack.ToString());
            update.Add("$id", recordId.ToString());
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO \"Rollback\" (RepairId, ExecutedAt, Success, Message) VALUES ($id, $at, 1, $message);";
            insert.Add("$id", recordId.ToString());
            insert.Add("$at", DbConvert.Date(rolledBackAt));
            insert.Add("$message", message);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<RepairRecord?> GetAsync(Guid recordId, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectColumns + " WHERE Id = $id;";
        command.Add("$id", recordId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<RepairRecord>> ListAsync(int limit, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectColumns + " ORDER BY StartedAt DESC LIMIT $limit;";
        command.Add("$limit", Math.Clamp(limit, 1, 1000));

        var list = new List<RepairRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(Map(reader));
        }

        return list;
    }

    private static RepairRecord Map(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        ActionId = reader.GetString(1),
        ActionTitle = reader.GetString(2),
        FindingId = reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
        Risk = (RiskLevel)reader.GetInt32(4),
        Outcome = Enum.Parse<RepairOutcome>(reader.GetString(5)),
        StartedAt = DbConvert.ParseDate(reader.GetString(6)),
        CompletedAt = reader.IsDBNull(7) ? null : DbConvert.ParseDate(reader.GetString(7)),
        User = reader.GetString(8),
        Reason = reader.GetNullableString(9),
        Message = reader.GetString(10),
        BackupId = reader.IsDBNull(11) ? null : Guid.Parse(reader.GetString(11)),
        SupportsRollback = reader.GetBoolean(12),
        Changes = DbConvert.FromJson<List<PlannedChange>>(reader.GetString(13)) ?? [],
        Comparison = DbConvert.FromJson<MeasurementComparison>(reader.GetNullableString(14)),
        RolledBackAt = reader.IsDBNull(15) ? null : DbConvert.ParseDate(reader.GetString(15)),
    };
}
