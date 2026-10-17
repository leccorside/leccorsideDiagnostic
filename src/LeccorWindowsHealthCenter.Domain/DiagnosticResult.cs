namespace LeccorWindowsHealthCenter.Domain;

/// <summary>Resultado da execução de um scanner (seção 10 do PROMPT).</summary>
public sealed record DiagnosticResult
{
    public required string ScannerId { get; init; }

    public required string ScannerName { get; init; }

    public required ScanModule Module { get; init; }

    public required ScannerRunStatus Status { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public TimeSpan Duration { get; init; }

    public IReadOnlyList<Metric> Metrics { get; init; } = [];

    public IReadOnlyList<Observation> Observations { get; init; } = [];

    /// <summary>Preenchido pelo orquestrador após o RuleEngine avaliar as regras do scanner.</summary>
    public IReadOnlyList<Finding> Findings { get; init; } = [];

    /// <summary>Mensagem de erro amigável quando <see cref="Status"/> = Failed.</summary>
    public string? Error { get; init; }

    public static DiagnosticResult Failed(string scannerId, string scannerName, ScanModule module, TimeSpan duration, string error) => new()
    {
        ScannerId = scannerId,
        ScannerName = scannerName,
        Module = module,
        Status = ScannerRunStatus.Failed,
        Duration = duration,
        Error = error,
    };
}
