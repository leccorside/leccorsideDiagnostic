using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Application.Scanning;

/// <summary>O que deve ser executado em uma varredura.</summary>
public sealed record ScanPlan
{
    public required ScanMode Mode { get; init; }

    /// <summary>Módulos selecionados (somente para <see cref="ScanMode.Custom"/>).</summary>
    public IReadOnlySet<ScanModule> Modules { get; init; } = new HashSet<ScanModule>();

    public static ScanPlan Quick { get; } = new() { Mode = ScanMode.Quick };

    public static ScanPlan Full { get; } = new() { Mode = ScanMode.Full };

    public static ScanPlan Custom(IEnumerable<ScanModule> modules) => new()
    {
        Mode = ScanMode.Custom,
        Modules = modules.ToHashSet(),
    };

    public bool Includes(IDiagnosticScanner scanner) => Mode switch
    {
        ScanMode.Full => true,
        ScanMode.Quick => scanner.IncludedInQuickScan,
        ScanMode.Custom => Modules.Contains(scanner.Module),
        _ => false,
    };
}

/// <summary>Estado de um scanner exibido na tela de progresso (seção 53).</summary>
public sealed record ScannerProgress(string ScannerId, string ScannerName, ScannerRunStatus Status);

public sealed record ScanProgress(IReadOnlyList<ScannerProgress> Scanners)
{
    public int Total => Scanners.Count;

    public int Finished => Scanners.Count(s => s.Status is ScannerRunStatus.Completed or ScannerRunStatus.Failed or ScannerRunStatus.Cancelled or ScannerRunStatus.Skipped);

    public double Percent => Total == 0 ? 100 : Math.Round(Finished * 100.0 / Total, 0);
}

public sealed class ScanOptions
{
    public const string SectionName = "Scan";

    /// <summary>Máximo de scanners em paralelo. Baixo por padrão: a ferramenta não pode causar a lentidão que mede (seção 58).</summary>
    public int MaxConcurrency { get; set; } = 2;

    /// <summary>Tempo máximo por scanner antes de ser considerado falho.</summary>
    public TimeSpan ScannerTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
