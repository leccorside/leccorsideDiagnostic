namespace LeccorWindowsHealthCenter.Application.WindowsUpdate;

public sealed record WindowsUpdateServiceStatus
{
    public required string WuauservState { get; init; }

    public required string BitsState { get; init; }

    public required string DosvcState { get; init; }

    public bool IsWuauservDisabled => string.Equals(WuauservState, "Disabled", StringComparison.OrdinalIgnoreCase);
}

public sealed record UpdateHistoryItem
{
    public required string HotFixId { get; init; }

    public string? Description { get; init; }

    public DateTimeOffset? InstalledOn { get; init; }

    public string? InstalledBy { get; init; }
}

public sealed record WindowsUpdateStatus
{
    public bool IsRebootPending { get; init; }

    public string? RebootReason { get; init; }

    public DateTimeOffset? LastSuccessfulScanTime { get; init; }

    public DateTimeOffset? LastSuccessfulInstallTime { get; init; }

    public int PendingUpdatesCount { get; init; }

    public IReadOnlyList<string> PendingUpdateTitles { get; init; } = [];

    public int RecentErrorCount24h { get; init; }

    public IReadOnlyList<string> RecentErrorMessages { get; init; } = [];

    public required WindowsUpdateServiceStatus ServiceStatus { get; init; }
}

public static class WindowsUpdateNames
{
    public const string ObservationStatus = "windowsupdate.status";
    public const string ObservationPendingUpdate = "windowsupdate.pending";
    public const string ObservationRecentError = "windowsupdate.error";

    public const string AttrRebootPending = "rebootPending";
    public const string AttrRebootReason = "rebootReason";
    public const string AttrLastScan = "lastScan";
    public const string AttrLastInstall = "lastInstall";
    public const string AttrPendingCount = "pendingCount";
    public const string AttrWuauservState = "wuauservState";
    public const string AttrBitsState = "bitsState";
    public const string AttrDosvcState = "dosvcState";
    public const string AttrRecentErrorCount = "recentErrorCount";
}

public interface IWindowsUpdateProvider
{
    Task<WindowsUpdateStatus> GetStatusAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<UpdateHistoryItem>> GetHistoryAsync(CancellationToken cancellationToken);
}

