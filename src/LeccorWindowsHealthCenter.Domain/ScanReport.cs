namespace LeccorWindowsHealthCenter.Domain;

/// <summary>Resultado consolidado de uma varredura completa/rápida/personalizada.</summary>
public sealed record ScanReport
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required ScanMode Mode { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }

    public bool WasCancelled { get; init; }

    public required IReadOnlyList<DiagnosticResult> Results { get; init; }

    public required HealthScore Score { get; init; }

    public string MachineName { get; init; } = Environment.MachineName;

    public TimeSpan Duration => CompletedAt - StartedAt;

    public IEnumerable<Finding> Findings => Results.SelectMany(r => r.Findings);

    /// <summary>Problemas (severidade ≥ Low), ordenados do mais grave para o menos grave.</summary>
    public IReadOnlyList<Finding> Problems => Findings
        .Where(f => !f.IsRecommendation)
        .OrderByDescending(f => f.Severity)
        .ThenByDescending(f => f.Confidence.Percent)
        .ToList();

    public IReadOnlyList<Finding> Recommendations => Findings.Where(f => f.IsRecommendation).ToList();

    public int CountBySeverity(Severity severity) => Findings.Count(f => f.Severity == severity);

    public int ConfirmedProblemCount => Findings.Count(f => !f.IsRecommendation && f.Confidence.IsConfirmed);
}
