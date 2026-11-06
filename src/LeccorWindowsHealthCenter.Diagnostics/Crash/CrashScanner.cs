using System.Globalization;
using LeccorWindowsHealthCenter.Application.Crash;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Diagnostics.Crash;

public sealed class CrashScanner : DiagnosticScannerBase
{
    public const string ScannerId = "crash";

    private readonly ICrashAnalyzerProvider _crashProvider;

    public CrashScanner(ICrashAnalyzerProvider crashProvider, ILogger<CrashScanner> logger)
        : base(logger)
    {
        _crashProvider = crashProvider;
    }

    public override string Id => ScannerId;

    public override string Name => "Análise de Travamentos e Despejos";

    public override ScanModule Module => ScanModule.Events;

    public override bool IncludedInQuickScan => true;

    public override int Order => 125;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var summary = await _crashProvider.GetCrashSummaryAsync(cancellationToken);

        collector.AddMetric("crash.kernel_dumps_count", summary.TotalKernelDumpsCount, "count");
        collector.AddMetric("crash.app_dumps_count", summary.TotalAppDumpsCount, "count");
        collector.AddMetric("crash.bsod_count_30d", summary.TotalBsodEventsPast30Days, "count");

        // Dumps recentes
        foreach (var dump in summary.DumpFiles.Take(10))
        {
            var attrs = new Dictionary<string, string>
            {
                [CrashNames.AttrFileName] = dump.FileName,
                [CrashNames.AttrDumpType] = dump.DumpType,
                [CrashNames.AttrSizeBytes] = dump.SizeBytes.ToString(CultureInfo.InvariantCulture)
            };
            collector.AddObservation(CrashNames.ObservationDumpFile, dump.FileName, attrs);
        }

        // Top Crashing Apps
        foreach (var app in summary.TopCrashingApps.Take(5))
        {
            var attrs = new Dictionary<string, string>
            {
                [CrashNames.AttrAppName] = app.AppName,
                [CrashNames.AttrCrashCount] = app.CrashCount.ToString(CultureInfo.InvariantCulture)
            };
            collector.AddObservation(CrashNames.ObservationAppCrash, app.AppName, attrs);
        }

        // BSOD
        if (summary.TotalBsodEventsPast30Days > 0)
        {
            var bsodAttrs = new Dictionary<string, string>
            {
                [CrashNames.AttrBugCheck] = summary.LatestBsodBugCheck ?? "Desconhecido"
            };
            collector.AddObservation(CrashNames.ObservationBsod, "RecentBSOD", bsodAttrs);
        }
    }
}

