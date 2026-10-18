namespace LeccorWindowsHealthCenter.Application.SystemInfo;

/// <summary>
/// Leitura instantânea dos contadores do sistema. Valores de taxa (por segundo) e percentuais
/// calculados entre duas coletas são null na primeira leitura de uma sessão — nunca 0 inventado.
/// </summary>
public sealed record PerformanceReading
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    // CPU
    public double? CpuPercent { get; init; }

    /// <summary>% Processor Performance: frequência efetiva em relação à nominal (pode passar de 100 com turbo).</summary>
    public double? ProcessorPerformancePercent { get; init; }

    /// <summary>Frequência nominal (MHz) informada pelo contador Processor Frequency.</summary>
    public double? ProcessorBaseFrequencyMHz { get; init; }

    public double? ProcessorQueueLength { get; init; }

    // Memória
    public long? TotalPhysicalBytes { get; init; }

    public long? AvailablePhysicalBytes { get; init; }

    public long? CommitTotalBytes { get; init; }

    public long? CommitLimitBytes { get; init; }

    public long? SystemCacheBytes { get; init; }

    public long? PagedPoolBytes { get; init; }

    public long? NonPagedPoolBytes { get; init; }

    /// <summary>Memory\Pages Input/sec: páginas lidas do disco para resolver falhas de página (hard faults).</summary>
    public double? PagesInputPerSecond { get; init; }

    public double? PageFileUsagePercent { get; init; }

    // GPU
    public IReadOnlyList<GpuAdapterUsage> Gpus { get; init; } = [];

    public double? MemoryUsedPercent => TotalPhysicalBytes is > 0 && AvailablePhysicalBytes is { } available
        ? (TotalPhysicalBytes.Value - available) * 100.0 / TotalPhysicalBytes.Value
        : null;

    public double? CommitPercent => CommitLimitBytes is > 0 && CommitTotalBytes is { } commit
        ? commit * 100.0 / CommitLimitBytes.Value
        : null;

    public double? EffectiveFrequencyMHz => ProcessorBaseFrequencyMHz is { } baseMhz && ProcessorPerformancePercent is { } perf
        ? baseMhz * perf / 100.0
        : null;

    /// <summary>Utilização da GPU como o Gerenciador de Tarefas: o motor mais ocupado do adaptador mais ocupado.</summary>
    public double? GpuPercent => Gpus.Count == 0 ? null : Gpus.Max(g => g.UtilizationPercent);
}

/// <summary>Uso de um adaptador gráfico (identificado pelo LUID dos contadores "GPU Engine").</summary>
public sealed record GpuAdapterUsage
{
    public required string AdapterLuid { get; init; }

    /// <summary>Maior utilização entre os tipos de motor (3D, Copy, VideoDecode...).</summary>
    public double UtilizationPercent { get; init; }

    public IReadOnlyDictionary<string, double> EngineUtilization { get; init; } = new Dictionary<string, double>();

    public long? DedicatedUsageBytes { get; init; }

    public long? SharedUsageBytes { get; init; }
}

/// <summary>Sessão de leitura de contadores (mantém o estado necessário para calcular taxas).</summary>
public interface IPerformanceCounterSession : IDisposable
{
    PerformanceReading Read();
}

public interface IPerformanceCounterSessionFactory
{
    IPerformanceCounterSession Create();
}

/// <summary>Amostra de um processo em um instante.</summary>
public sealed record ProcessSample
{
    public required int Pid { get; init; }

    public required string Name { get; init; }

    /// <summary>Tempo total de CPU consumido. Null quando o processo é protegido (acesso negado).</summary>
    public TimeSpan? TotalProcessorTime { get; init; }

    public long? WorkingSetBytes { get; init; }

    public long? PrivateBytes { get; init; }

    public int? ThreadCount { get; init; }

    public int? HandleCount { get; init; }

    public string? ExecutablePath { get; init; }

    public DateTimeOffset? StartTime { get; init; }
}

public sealed record ProcessSnapshot(DateTimeOffset CapturedAt, IReadOnlyList<ProcessSample> Processes);

public interface IProcessSnapshotProvider
{
    ProcessSnapshot Capture(bool includePaths = false);

    /// <summary>Caminho do executável; null quando o processo é protegido ou já terminou.</summary>
    string? TryGetExecutablePath(int pid);
}

/// <summary>Amostra do histórico de fundo (sistema inteiro).</summary>
public sealed record SystemHistorySample(DateTimeOffset Timestamp, double? CpuPercent, double? MemoryUsedPercent, double? CommitPercent);

/// <summary>Amostra do histórico de memória privada de um processo.</summary>
public sealed record ProcessMemorySample(DateTimeOffset Timestamp, int Pid, string Name, DateTimeOffset? StartTime, long PrivateBytes);

/// <summary>
/// Histórico leve mantido em segundo plano enquanto o app está aberto (seção 49: duração e recorrência).
/// Sem ele, uma varredura só enxergaria os segundos em que está rodando.
/// </summary>
public interface IMetricsHistory
{
    TimeSpan Retention { get; }

    IReadOnlyList<SystemHistorySample> SystemSamples();

    IReadOnlyList<ProcessMemorySample> ProcessMemorySamples();
}
