namespace LeccorWindowsHealthCenter.Application.Apps;

public enum AppSourceType
{
    Win32,
    WindowsStore,
    SystemComponent,
}

public sealed record InstalledAppItem
{
    public required string DisplayName { get; init; }

    public string? DisplayVersion { get; init; }

    public string? Publisher { get; init; }

    public DateTimeOffset? InstallDate { get; init; }

    public long? EstimatedSizeBytes { get; init; }

    public required AppSourceType SourceType { get; init; }

    public string? InstallLocation { get; init; }

    public string? UninstallString { get; init; }

    public string? QuietUninstallString { get; init; }

    public string? RegistryKeyPath { get; init; }

    public bool Is64Bit { get; init; }

    public bool IsOrphaned { get; init; }

    public bool HasDuplicateVersion { get; init; }
}

public static class AppsNames
{
    public const string ObservationApp = "apps.installed";

    public const string AttrName = "name";
    public const string AttrVersion = "version";
    public const string AttrPublisher = "publisher";
    public const string AttrSource = "source";
    public const string AttrSize = "size";
    public const string AttrInstallDate = "installDate";
    public const string AttrUninstallString = "uninstallString";
    public const string AttrIsOrphaned = "isOrphaned";
    public const string AttrHasDuplicate = "hasDuplicate";
}

public interface IAppsProvider
{
    Task<IReadOnlyList<InstalledAppItem>> GetInstalledAppsAsync(CancellationToken cancellationToken);
}

