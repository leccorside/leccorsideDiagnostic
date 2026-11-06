namespace LeccorWindowsHealthCenter.Application.Explorer;

public enum ShellExtensionType
{
    ContextMenu,
    PropertySheet,
    IconOverlay,
    ThumbnailHandler,
    PreviewHandler,
    DragAndDrop,
    Other,
}

public sealed record ShellExtensionItem
{
    public required string Clsid { get; init; }

    public required string Name { get; init; }

    public required ShellExtensionType Type { get; init; }

    public required string DllPath { get; init; }

    public required bool FileExists { get; init; }

    public string? Publisher { get; init; }

    public bool? IsSigned { get; init; }

    public bool IsRevoked { get; init; }

    public bool IsBlocked { get; init; }

    public bool IsWindowsOwned { get; init; }

    public string? RegistryKey { get; init; }

    public string? ThreadingModel { get; init; }
}

public sealed record ExplorerCacheStatus
{
    public long IconCacheSizeBytes { get; init; }

    public long ThumbnailCacheSizeBytes { get; init; }

    public int CacheFilesCount { get; init; }

    public IReadOnlyList<string> InaccessibleFiles { get; init; } = [];
}

public sealed record FolderBenchmarkResult
{
    public required string FolderPath { get; init; }

    public required TimeSpan EnumerationDuration { get; init; }

    public required TimeSpan MetadataReadDuration { get; init; }

    public required TimeSpan TotalDuration { get; init; }

    public required int FileCount { get; init; }

    public required int DirectoryCount { get; init; }

    public required long TotalSizeBytes { get; init; }

    public required IReadOnlyList<string> IdentifiedBottlenecks { get; init; }

    public bool HasPerformanceIssue => IdentifiedBottlenecks.Count > 0 || TotalDuration.TotalSeconds > 3;
}

public static class ExplorerNames
{
    public const string ObservationShellExtension = "explorer.shellex";
    public const string ObservationCache = "explorer.cache";

    public const string AttrClsid = "clsid";
    public const string AttrName = "name";
    public const string AttrType = "type";
    public const string AttrDllPath = "dllPath";
    public const string AttrExists = "exists";
    public const string AttrPublisher = "publisher";
    public const string AttrSigned = "signed";
    public const string AttrRevoked = "revoked";
    public const string AttrWindows = "windows";
    public const string AttrBlocked = "blocked";
}

public interface IExplorerProvider
{
    Task<IReadOnlyList<ShellExtensionItem>> GetShellExtensionsAsync(CancellationToken cancellationToken);

    Task<ExplorerCacheStatus> GetCacheStatusAsync(CancellationToken cancellationToken);
}

public interface IFolderBenchmarkService
{
    Task<FolderBenchmarkResult> BenchmarkFolderAsync(string folderPath, CancellationToken cancellationToken);
}

