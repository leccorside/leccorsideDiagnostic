namespace LeccorWindowsHealthCenter.Application.Crash;

public sealed record DumpFileInfo
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string DumpType { get; init; } // KernelMinidump, MemoryDump, AppCrashDump
    public required long SizeBytes { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public string? ProbableModule { get; init; }
}

public sealed record AppCrashCount
{
    public required string AppName { get; init; }
    public required int CrashCount { get; init; }
    public required int HangCount { get; init; }
    public required DateTimeOffset LastOccurredAt { get; init; }
}

public sealed record CrashSummary
{
    public required IReadOnlyList<DumpFileInfo> DumpFiles { get; init; }
    public required IReadOnlyList<AppCrashCount> TopCrashingApps { get; init; }
    public required int TotalKernelDumpsCount { get; init; }
    public required int TotalAppDumpsCount { get; init; }
    public required int TotalBsodEventsPast30Days { get; init; }
    public DateTimeOffset? LatestBsodTimestamp { get; init; }
    public string? LatestBsodBugCheck { get; init; }
    public DateTimeOffset CapturedAt { get; init; }
}

public static class CrashNames
{
    public const string ObservationDumpFile = "crash.dump";
    public const string ObservationAppCrash = "crash.app";
    public const string ObservationBsod = "crash.bsod";

    public const string AttrFileName = "fileName";
    public const string AttrDumpType = "dumpType";
    public const string AttrSizeBytes = "size";
    public const string AttrAppName = "appName";
    public const string AttrCrashCount = "crashCount";
    public const string AttrBugCheck = "bugCheck";
}

public interface ICrashAnalyzerProvider
{
    Task<CrashSummary> GetCrashSummaryAsync(CancellationToken cancellationToken);
}

