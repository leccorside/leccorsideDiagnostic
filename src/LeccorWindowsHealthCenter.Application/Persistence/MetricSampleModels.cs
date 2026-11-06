namespace LeccorWindowsHealthCenter.Application.Persistence;

/// <summary>
/// Amostra temporal de métricas essenciais do sistema (CPU, memória e commit) persistida no banco local SQLite (Pacote A).
/// Permite histórico contínuo entre sessões e amostragens em segundo plano quando o app roda com --background-scan.
/// </summary>
public sealed record MetricSampleRecord
{
    public long Id { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required double CpuPercent { get; init; }
    public required double MemoryUsedPercent { get; init; }
    public required long MemoryUsedBytes { get; init; }
    public required double CommitUsedPercent { get; init; }
    public required long CommitUsedBytes { get; init; }
}

/// <summary>
/// Repositório para gravação e consulta do histórico persistido de amostras de telemetria de hardware.
/// </summary>
public interface IMetricSampleRepository
{
    Task SaveSampleAsync(MetricSampleRecord sample, CancellationToken cancellationToken);
    Task<IReadOnlyList<MetricSampleRecord>> GetRecentSamplesAsync(TimeSpan window, CancellationToken cancellationToken);
    Task PruneOlderThanAsync(DateTimeOffset threshold, CancellationToken cancellationToken);
}

