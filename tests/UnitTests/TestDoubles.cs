using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;

namespace LeccorWindowsHealthCenter.UnitTests;

/// <summary>Scanner controlável: devolve métricas fixas, pode falhar, demorar ou observar cancelamento.</summary>
internal sealed class FakeScanner : IDiagnosticScanner
{
    private readonly Func<CancellationToken, Task<DiagnosticResult>>? _behavior;

    public FakeScanner(string id, ScanModule module = ScanModule.Performance, bool quick = true, int order = 0, Func<CancellationToken, Task<DiagnosticResult>>? behavior = null)
    {
        Id = id;
        Module = module;
        IncludedInQuickScan = quick;
        Order = order;
        _behavior = behavior;
    }

    public string Id { get; }

    public string Name => Id.ToUpperInvariant();

    public ScanModule Module { get; }

    public bool IncludedInQuickScan { get; }

    public int Order { get; }

    public int Calls { get; private set; }

    public IReadOnlyList<Metric> Metrics { get; init; } = [];

    public Task<DiagnosticResult> ScanAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return _behavior?.Invoke(cancellationToken) ?? Task.FromResult(Completed());
    }

    public DiagnosticResult Completed() => new()
    {
        ScannerId = Id,
        ScannerName = Name,
        Module = Module,
        Status = ScannerRunStatus.Completed,
        Metrics = Metrics,
    };
}

/// <summary>Regra genérica configurável para os testes do RuleEngine.</summary>
internal sealed class DelegateRule : IRule
{
    private readonly Func<RuleContext, IEnumerable<Finding>> _evaluate;

    public DelegateRule(string id, string[] requiredScanners, Func<RuleContext, IEnumerable<Finding>> evaluate, params RuleParameter[] parameters)
    {
        Id = id;
        RequiredScanners = requiredScanners;
        Parameters = parameters;
        _evaluate = evaluate;
    }

    public string Id { get; }

    public string Name => Id;

    public IReadOnlyList<string> RequiredScanners { get; }

    public IReadOnlyList<RuleParameter> Parameters { get; }

    public IEnumerable<Finding> Evaluate(RuleContext context) => _evaluate(context);
}

internal static class Findings
{
    public static Finding Make(
        string scannerId = "cpu",
        Severity severity = Severity.High,
        int confidence = 100,
        FindingCategory category = FindingCategory.Cpu,
        string ruleId = "test.rule",
        bool withEvidence = true) => new()
    {
        RuleId = ruleId,
        ScannerId = scannerId,
        Category = category,
        Title = "Problema de teste",
        Description = "Descrição",
        Severity = severity,
        Confidence = new Confidence(confidence),
        Evidence = withEvidence ? [new Evidence { Source = EvidenceSource.Measurement, Description = "medição" }] : [],
        Impact = "Impacto",
        Recommendation = "Recomendação",
    };

    public static Metric Sample(string name, double value, DateTimeOffset at, string scannerId = "cpu") => new()
    {
        Name = name,
        Value = value,
        Unit = "%",
        ScannerId = scannerId,
        Timestamp = at,
    };
}

internal sealed class FakeRuleSettings : IRuleSettings
{
    public HashSet<string> Disabled { get; } = [];

    public Dictionary<(string Rule, string Key), double> Values { get; } = [];

    public bool IsEnabled(string ruleId) => !Disabled.Contains(ruleId);

    public double? GetParameter(string ruleId, string key) => Values.TryGetValue((ruleId, key), out var v) ? v : null;
}

internal sealed class InMemoryScanRepository : IScanRepository
{
    public List<ScanReport> Saved { get; } = [];

    public Task SaveAsync(ScanReport report, CancellationToken cancellationToken)
    {
        Saved.Add(report);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ScanSummary>> GetRecentAsync(int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ScanSummary>>([]);

    public Task<IReadOnlyList<Finding>> GetFindingsAsync(Guid scanId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Finding>>([]);
}

internal sealed class InMemoryBackupStore : IBackupStore
{
    private readonly List<string> _log;

    public InMemoryBackupStore(List<string> log)
    {
        _log = log;
    }

    public Dictionary<Guid, BackupRecord> Records { get; } = [];

    public bool Fail { get; set; }

    public Task SaveAsync(BackupRecord record, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new IOException("disco cheio");
        }

        _log.Add("backup");
        Records[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task<BackupRecord?> GetAsync(Guid backupId, CancellationToken cancellationToken) =>
        Task.FromResult(Records.TryGetValue(backupId, out var r) ? r : null);
}

internal sealed class InMemoryRepairHistory : IRepairHistoryRepository
{
    public List<RepairRecord> Records { get; } = [];

    public Task AddAsync(RepairRecord record, CancellationToken cancellationToken)
    {
        Records.Add(record);
        return Task.CompletedTask;
    }

    public Task MarkRolledBackAsync(Guid recordId, DateTimeOffset rolledBackAt, string message, CancellationToken cancellationToken)
    {
        var index = Records.FindIndex(r => r.Id == recordId);
        Records[index] = Records[index] with { RolledBackAt = rolledBackAt, Outcome = RepairOutcome.RolledBack };
        return Task.CompletedTask;
    }

    public Task<RepairRecord?> GetAsync(Guid recordId, CancellationToken cancellationToken) =>
        Task.FromResult(Records.FirstOrDefault(r => r.Id == recordId));

    public Task<IReadOnlyList<RepairRecord>> ListAsync(int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RepairRecord>>(Records);
}

internal sealed class FakePrivileges : IPrivilegeService
{
    public bool IsElevated { get; set; }

    public string CurrentUser => @"PC\teste";
}

/// <summary>
/// Ação de reparo simulada: altera apenas um dicionário em memória. Nunca toca no sistema real
/// (seção 50: nunca executar correções destrutivas em testes automatizados).
/// </summary>
internal sealed class FakeRepairAction : IMeasurableRepairAction
{
    private readonly List<string> _log;

    public FakeRepairAction(List<string> log, RiskLevel risk = RiskLevel.Low, bool supportsRollback = true)
    {
        _log = log;
        Risk = risk;
        SupportsRollback = supportsRollback;
    }

    public Dictionary<string, string?> State { get; } = new() { ["HKCU\\Run\\App"] = "C:\\app.exe" };

    public string Id => "fake.disable-startup";

    public string Title => "Desativar App na inicialização";

    public string Description => "Remove a entrada de startup.";

    public RiskLevel Risk { get; }

    public bool RequiresElevation { get; init; }

    public bool RequiresRestart { get; init; }

    public bool SupportsRollback { get; }

    public bool ThrowOnExecute { get; set; }

    public bool FailValidation { get; set; }

    public double MeasureValue { get; set; } = 10;

    public string MeasurementName => "Tempo de logon";

    public string MeasurementUnit => "s";

    public bool LowerIsBetter => true;

    public Task<IReadOnlyList<PlannedChange>> PlanAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PlannedChange>>([new PlannedChange { Target = "HKCU\\Run\\App", Description = "Remover entrada", PreviousValue = State["HKCU\\Run\\App"] }]);

    public Task<IReadOnlyDictionary<string, string?>> CaptureStateAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, string?>>(new Dictionary<string, string?>(State));

    public Task<RepairResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        _log.Add("execute");
        State["HKCU\\Run\\App"] = null;
        if (ThrowOnExecute)
        {
            throw new InvalidOperationException("falha simulada");
        }

        MeasureValue = 2;
        return Task.FromResult(new RepairResult(true, "Entrada removida."));
    }

    public Task<RepairValidation> ValidateAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new RepairValidation(!FailValidation, FailValidation ? "ainda presente" : "ok"));

    public Task<RollbackResult> RollbackAsync(BackupRecord backup, CancellationToken cancellationToken)
    {
        _log.Add("rollback");
        foreach (var (key, value) in backup.PreviousValues)
        {
            State[key] = value;
        }

        return Task.FromResult(new RollbackResult(true, "Restaurado."));
    }

    public Task<double> MeasureAsync(CancellationToken cancellationToken) => Task.FromResult(MeasureValue);
}
