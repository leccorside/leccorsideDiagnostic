using LeccorWindowsHealthCenter.Application.Commands;

namespace LeccorWindowsHealthCenter.Application.WindowsHealth;

public enum ComponentStoreStatus
{
    Healthy = 0,
    Repairable = 1,
    Corrupted = 2,
    CheckFailed = 3,
}

public enum SystemFilesStatus
{
    IntegrityOk = 0,
    CorruptedRepaired = 1,
    CorruptedCannotRepair = 2,
    CheckFailed = 3,
}

public enum FileSystemStatus
{
    VolumeOk = 0,
    ErrorsFound = 1,
    CheckFailed = 2,
}

public static class WindowsIntegrityNames
{
    public const string ObservationComponentStore = "windows.component-store";
    public const string ObservationSystemFiles = "windows.system-files";
    public const string ObservationFileSystem = "windows.file-system";

    public const string AttrStatus = "status";
    public const string AttrMessage = "message";
    public const string AttrExitCode = "exitCode";
    public const string AttrCommandLine = "commandLine";
    public const string AttrDurationMs = "durationMs";
    public const string AttrOutput = "output";
    public const string AttrDrive = "drive";
}

public sealed record ComponentStoreCheck(
    ComponentStoreStatus Status,
    string Message,
    CommandResult? CommandResult);

public sealed record SystemFilesCheck(
    SystemFilesStatus Status,
    string Message,
    CommandResult? CommandResult);

public sealed record FileSystemCheck(
    string Drive,
    FileSystemStatus Status,
    string Message,
    CommandResult? CommandResult);

public sealed record WindowsIntegrityResult
{
    public required ComponentStoreCheck ComponentStore { get; init; }

    public SystemFilesCheck? SystemFiles { get; init; }

    public FileSystemCheck? FileSystem { get; init; }

    public DateTimeOffset VerifiedAt { get; init; } = DateTimeOffset.UtcNow;
}

