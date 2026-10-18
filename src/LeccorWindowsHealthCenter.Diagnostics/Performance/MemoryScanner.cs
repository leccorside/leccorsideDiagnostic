using System.Globalization;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.Diagnostics.Performance;

/// <summary>
/// Memória (seção 12): uso físico, commit, cache, pools, arquivo de paginação, falhas de página com leitura em disco
/// e maiores consumidores; inclui o histórico de memória privada por processo (base da regra de possível vazamento).
/// </summary>
public sealed class MemoryScanner : DiagnosticScannerBase
{
    public const string ScannerId = "memory";

    private static readonly IReadOnlyDictionary<string, string> ScanTag = new Dictionary<string, string> { [PerfNames.SourceTag] = PerfNames.SourceScan };
    private static readonly IReadOnlyDictionary<string, string> HistoryTag = new Dictionary<string, string> { [PerfNames.SourceTag] = PerfNames.SourceHistory };

    private readonly IPerformanceCounterSessionFactory _counters;
    private readonly IProcessSnapshotProvider _processes;
    private readonly IMetricsHistory _history;
    private readonly SamplingOptions _options;
    private readonly TimeProvider _timeProvider;

    public MemoryScanner(
        IPerformanceCounterSessionFactory counters,
        IProcessSnapshotProvider processes,
        IMetricsHistory history,
        IOptions<SamplingOptions> options,
        TimeProvider timeProvider,
        ILogger<MemoryScanner> logger)
        : base(logger)
    {
        _counters = counters;
        _processes = processes;
        _history = history;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public override string Id => ScannerId;

    public override string Name => "Memória";

    public override ScanModule Module => ScanModule.Performance;

    public override int Order => 30;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var scanStart = _timeProvider.GetUtcNow();

        var system = _history.SystemSamples().Where(s => s.Timestamp < scanStart).ToList();
        foreach (var sample in system)
        {
            if (sample.MemoryUsedPercent is { } used)
            {
                collector.AddMetric(PerfNames.MemoryUsed, used, "%", HistoryTag, sample.Timestamp);
            }

            if (sample.CommitPercent is { } commit)
            {
                collector.AddMetric(PerfNames.MemoryCommit, commit, "%", HistoryTag, sample.Timestamp);
            }
        }

        foreach (var sample in _history.ProcessMemorySamples())
        {
            collector.AddMetric(PerfNames.ProcessPrivate, sample.PrivateBytes, "bytes", new Dictionary<string, string>
            {
                [PerfNames.SourceTag] = PerfNames.SourceHistory,
                ["pid"] = sample.Pid.ToString(CultureInfo.InvariantCulture),
                ["name"] = sample.Name,
                ["start"] = sample.StartTime?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            }, sample.Timestamp);
        }

        using var session = _counters.Create();
        for (var i = 0; i < _options.SampleCount; i++)
        {
            await Task.Delay(_options.Interval, _timeProvider, cancellationToken).ConfigureAwait(false);
            var r = session.Read();
            var t = r.Timestamp;
            Add(collector, PerfNames.MemoryUsed, r.MemoryUsedPercent, "%", t);
            Add(collector, PerfNames.MemoryCommit, r.CommitPercent, "%", t);
            Add(collector, PerfNames.MemoryPagesInput, r.PagesInputPerSecond, "páginas/s", t);
            Add(collector, PerfNames.MemoryPageFile, r.PageFileUsagePercent, "%", t);

            if (i == _options.SampleCount - 1)
            {
                // Valores absolutos: basta a última leitura.
                Add(collector, PerfNames.MemoryTotal, r.TotalPhysicalBytes, "bytes", t);
                Add(collector, PerfNames.MemoryAvailable, r.AvailablePhysicalBytes, "bytes", t);
                Add(collector, PerfNames.MemoryCommitTotal, r.CommitTotalBytes, "bytes", t);
                Add(collector, PerfNames.MemoryCommitLimit, r.CommitLimitBytes, "bytes", t);
                Add(collector, PerfNames.MemoryCache, r.SystemCacheBytes, "bytes", t);
                Add(collector, PerfNames.MemoryPagedPool, r.PagedPoolBytes, "bytes", t);
                Add(collector, PerfNames.MemoryNonPagedPool, r.NonPagedPoolBytes, "bytes", t);
            }
        }

        var snapshot = _processes.Capture();
        foreach (var process in ProcessUsageCalculator.TopByPrivateMemory(snapshot, _options.TopProcesses))
        {
            collector.AddObservation(PerfNames.ObservationProcessMemory, $"{process.Name} ({process.Pid})", new Dictionary<string, string>
            {
                ["pid"] = process.Pid.ToString(CultureInfo.InvariantCulture),
                ["name"] = process.Name,
                ["privateBytes"] = process.PrivateBytes!.Value.ToString(CultureInfo.InvariantCulture),
                ["workingSetBytes"] = (process.WorkingSetBytes ?? 0).ToString(CultureInfo.InvariantCulture),
                ["path"] = _processes.TryGetExecutablePath(process.Pid) ?? string.Empty,
            });
        }

        collector.AddObservation(PerfNames.ObservationSampling, ScannerId, new Dictionary<string, string>
        {
            ["windowSeconds"] = _options.Window.TotalSeconds.ToString("0", CultureInfo.InvariantCulture),
            ["historySeconds"] = (system.Count < 2 ? 0 : (system[^1].Timestamp - system[0].Timestamp).TotalSeconds).ToString("0", CultureInfo.InvariantCulture),
        });
    }

    private static void Add(ScanCollector collector, string name, double? value, string unit, DateTimeOffset timestamp)
    {
        if (value is { } v && double.IsFinite(v))
        {
            collector.AddMetric(name, v, unit, ScanTag, timestamp);
        }
    }

    private static void Add(ScanCollector collector, string name, long? value, string unit, DateTimeOffset timestamp) =>
        Add(collector, name, value is { } v ? (double)v : null, unit, timestamp);
}
