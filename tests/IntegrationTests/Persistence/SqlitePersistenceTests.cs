using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;
using LeccorWindowsHealthCenter.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.IntegrationTests.Persistence;

public sealed class SqlitePersistenceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly SqliteDatabase _database;

    public SqlitePersistenceTests()
    {
        _database = new SqliteDatabase(_temp.File(Path.Combine("Data", "health.db")));
    }

    public void Dispose() => _temp.Dispose();

    private Task<int> MigrateAsync() => new MigrationRunner(_database, NullLogger<MigrationRunner>.Instance).MigrateAsync(CancellationToken.None);

    [Fact]
    public async Task Migrations_create_schema_and_are_idempotent()
    {
        var first = await MigrateAsync();
        var second = await MigrateAsync();

        Assert.Equal(Migrations.All[^1].Version, first);
        Assert.Equal(first, second);

        await using var connection = await _database.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";
        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        foreach (var expected in new[] { "Scan", "Finding", "Evidence", "Metric", "Repair", "Rollback", "HardwareSnapshot", "EventSummary", "Application", "Configuration", "SchemaMigrations" })
        {
            Assert.Contains(expected, tables);
        }
    }

    [Fact]
    public async Task Failed_migration_is_rolled_back()
    {
        var broken = new List<Migration>(Migrations.All) { new(999, "Quebrada", "CREATE TABLE Ok (Id INTEGER); SELECT * FROM TabelaInexistente;") };
        var runner = new MigrationRunner(_database, NullLogger<MigrationRunner>.Instance, broken);

        await Assert.ThrowsAnyAsync<Exception>(() => runner.MigrateAsync(CancellationToken.None));

        Assert.Equal(Migrations.All[^1].Version, await MigrateAsync());
        await using var connection = await _database.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = 'Ok';";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Scan_report_roundtrip_with_findings_evidence_and_metrics()
    {
        await MigrateAsync();
        var repository = new SqliteScanRepository(_database);
        var finding = new Finding
        {
            RuleId = "events.disk-io",
            ScannerId = "eventlog",
            Category = FindingCategory.Disk,
            Title = "Disco D: com erros de I/O",
            Description = "Eventos recorrentes de I/O.",
            Severity = Severity.Critical,
            Confidence = new Confidence(95),
            Evidence =
            [
                new Evidence { Source = EvidenceSource.EventLog, Description = "Disk / Event 153", Value = 47, Unit = "eventos", Reference = "System/Disk/153", SampleCount = 47 },
                new Evidence { Source = EvidenceSource.EventLog, Description = "Últimas 24h", Value = 19, Unit = "eventos" },
            ],
            Impact = "Possível falha de armazenamento.",
            Recommendation = "Backup dos dados e diagnóstico aprofundado.",
            ProbableCause = "Comunicação com o dispositivo",
        };
        var report = new ScanReport
        {
            Mode = ScanMode.Full,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            CompletedAt = DateTimeOffset.UtcNow,
            Score = new HealthScore { Overall = 72, Rating = HealthRating.Attention, Categories = new Dictionary<ScoreCategory, int> { [ScoreCategory.Hardware] = 55 } },
            Results =
            [
                new DiagnosticResult
                {
                    ScannerId = "eventlog",
                    ScannerName = "Event Viewer",
                    Module = ScanModule.Events,
                    Status = ScannerRunStatus.Completed,
                    Duration = TimeSpan.FromSeconds(3),
                    Findings = [finding, finding with { Id = Guid.NewGuid(), Severity = Severity.Info, Title = "Recomendação" }],
                    Metrics = [new Metric { Name = "events.errors", Value = 47, Unit = "count", ScannerId = "eventlog", Tags = new Dictionary<string, string> { ["log"] = "System" } }],
                },
                DiagnosticResult.Failed("wmi", "WMI", ScanModule.Hardware, TimeSpan.FromSeconds(1), "indisponível"),
            ],
        };

        await repository.SaveAsync(report, CancellationToken.None);
        var recent = await repository.GetRecentAsync(10, CancellationToken.None);
        var findings = await repository.GetFindingsAsync(report.Id, CancellationToken.None);

        var summary = Assert.Single(recent);
        Assert.Equal(72, summary.Score);
        Assert.Equal(HealthRating.Attention, summary.Rating);
        Assert.Equal(1, summary.ProblemCount);
        Assert.Equal(1, summary.RecommendationCount);

        var loaded = findings.First(f => f.Id == finding.Id);
        Assert.Equal(finding.Title, loaded.Title);
        Assert.Equal(95, loaded.Confidence.Percent);
        Assert.Equal(Severity.Critical, loaded.Severity);
        Assert.Equal(2, loaded.Evidence.Count);
        Assert.Equal("System/Disk/153", loaded.Evidence[0].Reference);
        Assert.Equal(47, loaded.Evidence[0].SampleCount);
    }

    [Fact]
    public async Task Repair_history_roundtrip_and_rollback_marking()
    {
        await MigrateAsync();
        var repository = new SqliteRepairHistoryRepository(_database);
        var record = new RepairRecord
        {
            ActionId = "startup.disable",
            ActionTitle = "Desativar Discord na inicialização",
            Risk = RiskLevel.Low,
            Outcome = RepairOutcome.Succeeded,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            User = @"PC\usuario",
            Message = "Entrada desativada.",
            Reason = "Impacto alto no logon",
            BackupId = Guid.NewGuid(),
            SupportsRollback = true,
            Changes = [new PlannedChange { Target = @"HKCU\...\Run\Discord", Description = "Remover", PreviousValue = "C:\\discord.exe" }],
            Comparison = new MeasurementComparison { Name = "Logon", Unit = "s", Before = 40, After = 25 },
        };

        await repository.AddAsync(record, CancellationToken.None);
        var loaded = await repository.GetAsync(record.Id, CancellationToken.None);
        await repository.MarkRolledBackAsync(record.Id, DateTimeOffset.UtcNow, "Restaurado", CancellationToken.None);
        var afterRollback = await repository.GetAsync(record.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.True(loaded.CanRollback);
        Assert.Equal("C:\\discord.exe", Assert.Single(loaded.Changes).PreviousValue);
        Assert.Equal(37.5, loaded.Comparison!.ImprovementPercent);
        Assert.NotNull(afterRollback!.RolledBackAt);
        Assert.Equal(RepairOutcome.RolledBack, afterRollback.Outcome);
        Assert.False(afterRollback.CanRollback);
        Assert.Single(await repository.ListAsync(10, CancellationToken.None));
    }
}
