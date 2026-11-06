namespace LeccorWindowsHealthCenter.Application.Scheduling;

public enum ScheduledScanFrequency
{
    Disabled,
    Daily,
    Weekly,
    Monthly,
}

public sealed record ScheduleStatus
{
    public required bool IsConfigured { get; init; }

    public required ScheduledScanFrequency Frequency { get; init; }

    public string? TaskName { get; init; }

    public string? NextRun { get; init; }

    public string? Details { get; init; }
}

/// <summary>
/// Gerenciador de agendamento de varreduras via Windows Task Scheduler (seção 60).
/// </summary>
public interface IScanSchedulerService
{
    Task<ScheduleStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task ConfigureScheduleAsync(ScheduledScanFrequency frequency, CancellationToken cancellationToken = default);

    Task DisableScheduleAsync(CancellationToken cancellationToken = default);
}

