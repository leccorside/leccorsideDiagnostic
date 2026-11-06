using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Runtime.Versioning;
using LeccorWindowsHealthCenter.Application.Crash;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.Crash;

[SupportedOSPlatform("windows")]
public sealed class CrashAnalyzerProvider : ICrashAnalyzerProvider
{
    private readonly ILogger<CrashAnalyzerProvider> _logger;

    public CrashAnalyzerProvider(ILogger<CrashAnalyzerProvider> logger)
    {
        _logger = logger;
    }

    public Task<CrashSummary> GetCrashSummaryAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var dumps = CollectDumpFiles(cancellationToken);
            var (topApps, bsodCount, latestBsod, latestBugCheck) = AnalyzeEventViewerCrashes(cancellationToken);

            var kernelDumpsCount = dumps.Count(d => d.DumpType == "KernelMinidump" || d.DumpType == "MemoryDump");
            var appDumpsCount = dumps.Count(d => d.DumpType == "AppCrashDump");

            return new CrashSummary
            {
                DumpFiles = dumps,
                TopCrashingApps = topApps,
                TotalKernelDumpsCount = kernelDumpsCount,
                TotalAppDumpsCount = appDumpsCount,
                TotalBsodEventsPast30Days = bsodCount,
                LatestBsodTimestamp = latestBsod,
                LatestBsodBugCheck = latestBugCheck,
                CapturedAt = DateTimeOffset.UtcNow
            };
        }, cancellationToken);
    }

    private static List<DumpFileInfo> CollectDumpFiles(CancellationToken cancellationToken)
    {
        var list = new List<DumpFileInfo>();
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // 1. Minidumps do kernel (%SystemRoot%\Minidump)
        var minidumpDir = Path.Combine(winDir, "Minidump");
        if (Directory.Exists(minidumpDir))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(minidumpDir, "*.dmp"))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fi = new FileInfo(file);
                    list.Add(new DumpFileInfo
                    {
                        FilePath = file,
                        FileName = fi.Name,
                        DumpType = "KernelMinidump",
                        SizeBytes = fi.Length,
                        CreatedAt = fi.LastWriteTimeUtc
                    });
                }
            }
            catch
            {
            }
        }

        // 2. Despejo completo (%SystemRoot%\MEMORY.DMP)
        var memoryDmp = Path.Combine(winDir, "MEMORY.DMP");
        if (File.Exists(memoryDmp))
        {
            try
            {
                var fi = new FileInfo(memoryDmp);
                list.Add(new DumpFileInfo
                {
                    FilePath = memoryDmp,
                    FileName = fi.Name,
                    DumpType = "MemoryDump",
                    SizeBytes = fi.Length,
                    CreatedAt = fi.LastWriteTimeUtc
                });
            }
            catch
            {
            }
        }

        // 3. Application Crash Dumps (%LocalAppData%\CrashDumps)
        var appCrashDir = Path.Combine(localApp, "CrashDumps");
        if (Directory.Exists(appCrashDir))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(appCrashDir, "*.dmp"))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fi = new FileInfo(file);
                    var appName = fi.Name.Split('.')[0];
                    list.Add(new DumpFileInfo
                    {
                        FilePath = file,
                        FileName = fi.Name,
                        DumpType = "AppCrashDump",
                        SizeBytes = fi.Length,
                        CreatedAt = fi.LastWriteTimeUtc,
                        ProbableModule = appName
                    });
                }
            }
            catch
            {
            }
        }

        return list.OrderByDescending(d => d.CreatedAt).ToList();
    }

    private (IReadOnlyList<AppCrashCount> TopApps, int BsodCount, DateTimeOffset? LatestBsod, string? LatestBugCheck)
        AnalyzeEventViewerCrashes(CancellationToken cancellationToken)
    {
        var appStats = new Dictionary<string, (int Crashes, int Hangs, DateTimeOffset Last)>(StringComparer.OrdinalIgnoreCase);
        var bsodCount = 0;
        DateTimeOffset? latestBsod = null;
        string? latestBugCheck = null;

        var cutoff = DateTime.UtcNow.AddDays(-30);

        // Application log: Event ID 1000 (Error) e 1002 (Hang)
        try
        {
            var query = new EventLogQuery("Application", PathType.LogName, "*[System[(EventID=1000 or EventID=1002) and TimeCreated[timediff(@SystemTime) <= 2592000000]]]");
            using var reader = new EventLogReader(query);
            for (var record = reader.ReadEvent(); record != null; record = reader.ReadEvent())
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (record)
                {
                    var time = record.TimeCreated ?? DateTime.UtcNow;
                    var id = record.Id;
                    var appName = "Aplicativo";

                    if (record.Properties.Count > 0 && record.Properties[0].Value is string s && !string.IsNullOrWhiteSpace(s))
                    {
                        appName = Path.GetFileName(s);
                    }

                    if (!appStats.TryGetValue(appName, out var current))
                    {
                        current = (0, 0, time);
                    }

                    if (id == 1000)
                    {
                        current = (current.Crashes + 1, current.Hangs, time > current.Last ? time : current.Last);
                    }
                    else
                    {
                        current = (current.Crashes, current.Hangs + 1, time > current.Last ? time : current.Last);
                    }

                    appStats[appName] = current;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível ler eventos de Application Error/Hang");
        }

        // System log: Event ID 1001 (BugCheck / BSOD)
        try
        {
            var query = new EventLogQuery("System", PathType.LogName, "*[System[Provider[@Name='Microsoft-Windows-WER-SystemErrorReporting' or @Name='BugCheck'] and (EventID=1001) and TimeCreated[timediff(@SystemTime) <= 2592000000]]]");
            using var reader = new EventLogReader(query);
            for (var record = reader.ReadEvent(); record != null; record = reader.ReadEvent())
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (record)
                {
                    bsodCount++;
                    var time = record.TimeCreated ?? DateTime.UtcNow;
                    if (latestBsod == null || time > latestBsod)
                    {
                        latestBsod = time;
                        if (record.Properties.Count > 0 && record.Properties[0].Value is string bugCheck)
                        {
                            latestBugCheck = bugCheck;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível ler eventos de BugCheck no System log");
        }

        var topApps = appStats
            .Select(kv => new AppCrashCount
            {
                AppName = kv.Key,
                CrashCount = kv.Value.Crashes,
                HangCount = kv.Value.Hangs,
                LastOccurredAt = kv.Value.Last
            })
            .OrderByDescending(a => a.CrashCount + a.HangCount)
            .Take(15)
            .ToList();

        return (topApps, bsodCount, latestBsod, latestBugCheck);
    }
}
