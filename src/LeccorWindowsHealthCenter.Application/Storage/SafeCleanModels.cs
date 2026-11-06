namespace LeccorWindowsHealthCenter.Application.Storage;

public enum CleanCategory
{
    Safe = 0,
    Review = 1,
    Advanced = 2,
}

public sealed record CleanItem
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required CleanCategory Category { get; init; }

    public required IReadOnlyList<string> TargetPaths { get; init; }

    public long TotalSizeBytes { get; init; }

    public int FileCount { get; init; }

    public int InaccessibleCount { get; init; }

    public required string TechnicalJustification { get; init; }

    public bool Exists => TotalSizeBytes > 0 || FileCount > 0;
}

public sealed record CleanCategorySummary
{
    public required CleanCategory Category { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public long TotalSizeBytes { get; init; }

    public int TotalFilesCount { get; init; }

    public required IReadOnlyList<CleanItem> Items { get; init; }
}

public sealed record CleanAnalysisResult
{
    public required IReadOnlyList<CleanCategorySummary> Categories { get; init; }

    public long TotalCleanableBytes { get; init; }

    public long SafeCleanableBytes { get; init; }

    public long ReviewCleanableBytes { get; init; }

    public long AdvancedCleanableBytes { get; init; }

    public int TotalFilesCount { get; init; }

    public DateTimeOffset AnalyzedAt { get; init; }
}

public static class SafeCleanNames
{
    public const string ObservationCleanCategory = "clean.category";
    public const string ObservationCleanItem = "clean.item";

    public const string AttrCategory = "category";
    public const string AttrItemId = "itemId";
    public const string AttrTitle = "title";
    public const string AttrSize = "size";
    public const string AttrFiles = "files";
}

public interface ISafeCleanProvider
{
    Task<CleanAnalysisResult> AnalyzeCleanableLocationsAsync(CancellationToken cancellationToken);
}

