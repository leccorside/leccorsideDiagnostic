namespace LeccorWindowsHealthCenter.Application.DevTools;

public sealed record DevToolItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; } // Runtime, Package Manager, VCS, IDE
    public string? Version { get; init; }
    public string? Path { get; init; }
    public bool IsInstalled { get; init; }
    public string? StatusDetail { get; init; }
}

public sealed record WslDistributionInfo
{
    public required string Name { get; init; }
    public required int WslVersion { get; init; } // 1 ou 2
    public required string State { get; init; } // Running, Stopped
    public required bool IsDefault { get; init; }
    public string? BasePath { get; init; }
    public string? VhdxPath { get; init; }
    public long? VhdxSizeBytes { get; init; }
    public string? LinuxVersion { get; init; }
}

public sealed record DockerStatusInfo
{
    public required bool IsInstalled { get; init; }
    public required bool IsRunning { get; init; }
    public string? Version { get; init; }
    public int RunningContainers { get; init; }
    public int TotalContainers { get; init; }
    public int TotalImages { get; init; }
    public int TotalVolumes { get; init; }
    public long? ContainersSizeBytes { get; init; }
    public long? ImagesSizeBytes { get; init; }
    public long? VolumesSizeBytes { get; init; }
    public long? BuildCacheSizeBytes { get; init; }
    public long? TotalDiskUsageBytes { get; init; }
    public string? Backend { get; init; } // WSL2 ou Hyper-V
    public string? ErrorMessage { get; init; }
}

public sealed record PathEnvironmentAnalysis
{
    public required IReadOnlyList<string> UserEntries { get; init; }
    public required IReadOnlyList<string> SystemEntries { get; init; }
    public required IReadOnlyList<string> InvalidEntries { get; init; }
    public required int TotalLength { get; init; }
    public bool IsLengthWarning => TotalLength > 2048;
}

public sealed record DevEnvironmentSummary
{
    public required IReadOnlyList<DevToolItem> Tools { get; init; }
    public required IReadOnlyList<WslDistributionInfo> WslDistributions { get; init; }
    public required DockerStatusInfo Docker { get; init; }
    public required PathEnvironmentAnalysis PathAnalysis { get; init; }
    public required bool HasHyperV { get; init; }
    public DateTimeOffset CapturedAt { get; init; }
}

public static class DevToolsNames
{
    public const string ObservationDevTool = "dev.tool";
    public const string ObservationWslDistro = "dev.wsl.distro";
    public const string ObservationDocker = "dev.docker";
    public const string ObservationPath = "dev.path";

    public const string AttrToolName = "name";
    public const string AttrVersion = "version";
    public const string AttrCategory = "category";
    public const string AttrInstalled = "installed";

    public const string AttrWslName = "distroName";
    public const string AttrWslVersion = "wslVersion";
    public const string AttrWslVhdxSize = "vhdxSize";

    public const string AttrDockerRunning = "dockerRunning";
    public const string AttrDockerTotalUsage = "dockerTotalUsage";

    public const string AttrPathInvalidCount = "pathInvalidCount";
}

public interface IDevToolsProvider
{
    Task<DevEnvironmentSummary> GetDevEnvironmentAsync(CancellationToken cancellationToken);
}

