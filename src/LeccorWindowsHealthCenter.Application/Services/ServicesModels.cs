namespace LeccorWindowsHealthCenter.Application.Services;

public enum WindowsServiceState
{
    Running,
    Stopped,
    Paused,
    StartPending,
    StopPending,
    PausePending,
    ContinuePending,
    Unknown,
}

public enum WindowsServiceStartupType
{
    Automatic,
    AutomaticDelayed,
    Manual,
    Disabled,
    Boot,
    System,
    Unknown,
}

public sealed record ServiceItem
{
    public required string ServiceName { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public required WindowsServiceState State { get; init; }

    public required WindowsServiceStartupType StartupType { get; init; }

    public string? ServiceAccount { get; init; }

    public string? RawImagePath { get; init; }

    public string? ExecutablePath { get; init; }

    public required bool FileExists { get; init; }

    public string? Publisher { get; init; }

    public bool? IsSigned { get; init; }

    public bool IsWindowsService { get; init; }

    public int? ProcessId { get; init; }

    public IReadOnlyList<string> Dependencies { get; init; } = [];

    public bool HasRecentFailure { get; init; }

    public string? FailureReason { get; init; }
}

public static class WindowsServiceNames
{
    public const string ObservationService = "services.entry";

    public const string AttrName = "name";
    public const string AttrDisplayName = "displayName";
    public const string AttrState = "state";
    public const string AttrStartupType = "startupType";
    public const string AttrExecutablePath = "executablePath";
    public const string AttrExists = "exists";
    public const string AttrPublisher = "publisher";
    public const string AttrSigned = "signed";
    public const string AttrWindows = "windows";
    public const string AttrPid = "pid";
    public const string AttrHasFailure = "hasFailure";
    public const string AttrFailureReason = "failureReason";
}

public interface IServicesProvider
{
    Task<IReadOnlyList<ServiceItem>> GetServicesAsync(CancellationToken cancellationToken);
}

