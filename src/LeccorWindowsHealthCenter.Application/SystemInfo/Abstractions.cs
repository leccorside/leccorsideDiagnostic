namespace LeccorWindowsHealthCenter.Application.SystemInfo;

/// <summary>Contexto de segurança do processo atual.</summary>
public interface IPrivilegeService
{
    bool IsElevated { get; }

    /// <summary>Usuário atual no formato DOMÍNIO\usuário (registrado em backups e histórico).</summary>
    string CurrentUser { get; }
}

/// <summary>
/// Amostra leve de uso do sistema para o dashboard. Valores null significam
/// "não disponível" — nunca preencher com estimativas.
/// </summary>
public sealed record LiveSystemMetrics
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public double? CpuPercent { get; init; }

    public double? MemoryPercent { get; init; }

    public long? MemoryTotalBytes { get; init; }

    public long? MemoryAvailableBytes { get; init; }

    public double? SystemDrivePercent { get; init; }

    public string SystemDrive { get; init; } = "C:";

    public double? GpuPercent { get; init; }
}

public interface ILiveMetricsProvider
{
    /// <summary>Coleta uma amostra. Chamadas consecutivas calculam CPU pelo delta entre elas.</summary>
    LiveSystemMetrics Sample();
}
