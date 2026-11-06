namespace LeccorWindowsHealthCenter.Application.Startup;

/// <summary>
/// Registro de degradação de tempo de inicialização medido no boot do Windows (Event IDs 101 a 110 do canal Diagnostics-Performance).
/// </summary>
public sealed record BootDegradationRecord
{
    public required string Target { get; init; }
    public required long DegradationTimeMs { get; init; }
    public required long TotalTimeMs { get; init; }
    public required int EventId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Provedor de telemetria de degradação de boot do canal Microsoft-Windows-Diagnostics-Performance/Operational.
/// </summary>
public interface IBootPerformanceProvider
{
    Task<IReadOnlyDictionary<string, BootDegradationRecord>> GetRecentBootDegradationsAsync(CancellationToken cancellationToken);
}

