using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Data.Sqlite;

namespace LeccorWindowsHealthCenter.Infrastructure.Persistence;

/// <summary>Persistência de varreduras, execuções de scanner, findings, evidências e métricas.</summary>
public sealed class SqliteScanRepository : IScanRepository
{
    private readonly SqliteDatabase _database;

    public SqliteScanRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public async Task SaveAsync(ScanReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var scan = connection.CreateCommand())
        {
            scan.Transaction = transaction;
            scan.CommandText = """
                INSERT INTO Scan (Id, Mode, StartedAt, CompletedAt, WasCancelled, Score, Rating, MachineName, CategoryScoresJson)
                VALUES ($id, $mode, $started, $completed, $cancelled, $score, $rating, $machine, $categories);
                """;
            scan.Add("$id", report.Id.ToString());
            scan.Add("$mode", report.Mode.ToString());
            scan.Add("$started", DbConvert.Date(report.StartedAt));
            scan.Add("$completed", DbConvert.Date(report.CompletedAt));
            scan.Add("$cancelled", report.WasCancelled);
            scan.Add("$score", report.Score.Overall);
            scan.Add("$rating", report.Score.Rating.ToString());
            scan.Add("$machine", report.MachineName);
            scan.Add("$categories", DbConvert.ToJson(report.Score.Categories));
            await scan.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var result in report.Results)
        {
            await InsertScannerRunAsync(connection, transaction, report.Id, result, cancellationToken).ConfigureAwait(false);
            await InsertMetricsAsync(connection, transaction, report.Id, result.Metrics, cancellationToken).ConfigureAwait(false);
            foreach (var finding in result.Findings)
            {
                await InsertFindingAsync(connection, transaction, report.Id, finding, cancellationToken).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScanSummary>> GetRecentAsync(int limit, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.Id, s.Mode, s.StartedAt, s.CompletedAt, s.Score, s.Rating, s.WasCancelled,
                   (SELECT COUNT(*) FROM Finding f WHERE f.ScanId = s.Id AND f.Severity > 0),
                   (SELECT COUNT(*) FROM Finding f WHERE f.ScanId = s.Id AND f.Severity = 0)
            FROM Scan s
            ORDER BY s.StartedAt DESC
            LIMIT $limit;
            """;
        command.Add("$limit", Math.Clamp(limit, 1, 1000));

        var list = new List<ScanSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new ScanSummary(
                Guid.Parse(reader.GetString(0)),
                Enum.Parse<ScanMode>(reader.GetString(1)),
                DbConvert.ParseDate(reader.GetString(2)),
                DbConvert.ParseDate(reader.GetString(3)),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                Enum.Parse<HealthRating>(reader.GetString(5)),
                reader.GetInt32(7),
                reader.GetInt32(8),
                reader.GetBoolean(6)));
        }

        return list;
    }

    public async Task<IReadOnlyList<Finding>> GetFindingsAsync(Guid scanId, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken).ConfigureAwait(false);

        var evidenceByFinding = new Dictionary<string, List<Evidence>>();
        await using (var evidenceCommand = connection.CreateCommand())
        {
            evidenceCommand.CommandText = """
                SELECT e.FindingId, e.Source, e.Description, e.Value, e.Unit, e.Reference, e.SampleCount, e.CollectedAt
                FROM Evidence e JOIN Finding f ON f.Id = e.FindingId
                WHERE f.ScanId = $scan ORDER BY e.Id;
                """;
            evidenceCommand.Add("$scan", scanId.ToString());
            await using var reader = await evidenceCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var findingId = reader.GetString(0);
                if (!evidenceByFinding.TryGetValue(findingId, out var list))
                {
                    evidenceByFinding[findingId] = list = [];
                }

                list.Add(new Evidence
                {
                    Source = Enum.Parse<EvidenceSource>(reader.GetString(1)),
                    Description = reader.GetString(2),
                    Value = reader.IsDBNull(3) ? null : reader.GetDouble(3),
                    Unit = reader.GetNullableString(4),
                    Reference = reader.GetNullableString(5),
                    SampleCount = reader.GetInt32(6),
                    CollectedAt = DbConvert.ParseDate(reader.GetString(7)),
                });
            }
        }

        var findings = new List<Finding>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, RuleId, ScannerId, Category, Title, Description, Severity, Confidence, ProbableCause, Impact,
                   Recommendation, RepairActionId, RepairRisk, RequiresElevation, RequiresRestart, SupportsRollback, Status, DetectedAt
            FROM Finding WHERE ScanId = $scan ORDER BY Severity DESC, Confidence DESC;
            """;
        command.Add("$scan", scanId.ToString());
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.GetString(0);
                findings.Add(new Finding
                {
                    Id = Guid.Parse(id),
                    RuleId = reader.GetString(1),
                    ScannerId = reader.GetString(2),
                    Category = Enum.Parse<FindingCategory>(reader.GetString(3)),
                    Title = reader.GetString(4),
                    Description = reader.GetString(5),
                    Severity = (Severity)reader.GetInt32(6),
                    Confidence = new Confidence(reader.GetInt32(7)),
                    ProbableCause = reader.GetNullableString(8),
                    Impact = reader.GetString(9),
                    Recommendation = reader.GetString(10),
                    RepairActionId = reader.GetNullableString(11),
                    RepairRisk = reader.IsDBNull(12) ? null : (RiskLevel)reader.GetInt32(12),
                    RequiresElevation = reader.GetBoolean(13),
                    RequiresRestart = reader.GetBoolean(14),
                    SupportsRollback = reader.GetBoolean(15),
                    Status = Enum.Parse<FindingStatus>(reader.GetString(16)),
                    DetectedAt = DbConvert.ParseDate(reader.GetString(17)),
                    Evidence = evidenceByFinding.TryGetValue(id, out var evidence) ? evidence : [],
                });
            }
        }

        return findings;
    }

    private static async Task InsertScannerRunAsync(SqliteConnection connection, SqliteTransaction transaction, Guid scanId, DiagnosticResult result, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ScannerRun (ScanId, ScannerId, ScannerName, Module, Status, DurationMs, Error)
            VALUES ($scan, $scanner, $name, $module, $status, $duration, $error);
            """;
        command.Add("$scan", scanId.ToString());
        command.Add("$scanner", result.ScannerId);
        command.Add("$name", result.ScannerName);
        command.Add("$module", result.Module.ToString());
        command.Add("$status", result.Status.ToString());
        command.Add("$duration", (long)result.Duration.TotalMilliseconds);
        command.Add("$error", result.Error);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertMetricsAsync(SqliteConnection connection, SqliteTransaction transaction, Guid scanId, IReadOnlyList<Metric> metrics, CancellationToken cancellationToken)
    {
        if (metrics.Count == 0)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Metric (ScanId, ScannerId, Name, Value, Unit, Timestamp, TagsJson)
            VALUES ($scan, $scanner, $name, $value, $unit, $ts, $tags);
            """;
        var scan = command.Add("$scan", scanId.ToString());
        var scanner = command.Add("$scanner", string.Empty);
        var name = command.Add("$name", string.Empty);
        var value = command.Add("$value", 0d);
        var unit = command.Add("$unit", string.Empty);
        var ts = command.Add("$ts", string.Empty);
        var tags = command.Add("$tags", null);

        foreach (var metric in metrics)
        {
            scanner.Value = metric.ScannerId;
            name.Value = metric.Name;
            value.Value = metric.Value;
            unit.Value = metric.Unit;
            ts.Value = DbConvert.Date(metric.Timestamp);
            tags.Value = metric.Tags.Count == 0 ? DBNull.Value : DbConvert.ToJson(metric.Tags);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task InsertFindingAsync(SqliteConnection connection, SqliteTransaction transaction, Guid scanId, Finding finding, CancellationToken cancellationToken)
    {
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Finding (Id, ScanId, RuleId, ScannerId, Category, Title, Description, Severity, Confidence, ProbableCause, Impact,
                                     Recommendation, RepairActionId, RepairRisk, RequiresElevation, RequiresRestart, SupportsRollback, Status, DetectedAt)
                VALUES ($id, $scan, $rule, $scanner, $category, $title, $description, $severity, $confidence, $cause, $impact,
                        $recommendation, $repair, $risk, $elevation, $restart, $rollback, $status, $detected);
                """;
            command.Add("$id", finding.Id.ToString());
            command.Add("$scan", scanId.ToString());
            command.Add("$rule", finding.RuleId);
            command.Add("$scanner", finding.ScannerId);
            command.Add("$category", finding.Category.ToString());
            command.Add("$title", finding.Title);
            command.Add("$description", finding.Description);
            command.Add("$severity", (int)finding.Severity);
            command.Add("$confidence", finding.Confidence.Percent);
            command.Add("$cause", finding.ProbableCause);
            command.Add("$impact", finding.Impact);
            command.Add("$recommendation", finding.Recommendation);
            command.Add("$repair", finding.RepairActionId);
            command.Add("$risk", finding.RepairRisk is null ? null : (int)finding.RepairRisk.Value);
            command.Add("$elevation", finding.RequiresElevation);
            command.Add("$restart", finding.RequiresRestart);
            command.Add("$rollback", finding.SupportsRollback);
            command.Add("$status", finding.Status.ToString());
            command.Add("$detected", DbConvert.Date(finding.DetectedAt));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var evidence in finding.Evidence)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Evidence (FindingId, Source, Description, Value, Unit, Reference, SampleCount, CollectedAt)
                VALUES ($finding, $source, $description, $value, $unit, $reference, $samples, $collected);
                """;
            command.Add("$finding", finding.Id.ToString());
            command.Add("$source", evidence.Source.ToString());
            command.Add("$description", evidence.Description);
            command.Add("$value", evidence.Value);
            command.Add("$unit", evidence.Unit);
            command.Add("$reference", evidence.Reference);
            command.Add("$samples", evidence.SampleCount);
            command.Add("$collected", DbConvert.Date(evidence.CollectedAt));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
