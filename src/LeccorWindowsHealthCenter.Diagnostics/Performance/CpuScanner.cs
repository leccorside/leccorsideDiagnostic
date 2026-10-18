using System.Globalization;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.Diagnostics.Performance;

/// <summary>
/// CPU (seção 11): utilização, desempenho/frequência efetiva, fila do processador e maiores consumidores,
/// amostrados durante a varredura e complementados pelo histórico de fundo (duração e recorrência).
/// </summary>
public sealed class CpuScanner : DiagnosticScannerBase
{
    public const string ScannerId = "cpu";

    private static readonly IReadOnlyDictionary<string, string> ScanTag = new Dictionary<string, string> { [PerfNames.SourceTag] = PerfNames.SourceScan };
    private static readonly IReadOnlyDictionary<string, string> HistoryTag = new Dictionary<string, string> { [PerfNames.SourceTag] = PerfNames.SourceHistory };

    private readonly IPerformanceCounterSessionFactory _counters;
    private readonly IProcessSnapshotProvider _processes;
    private readonly IMetricsHistory _history;
    private readonly SamplingOptions _options;
    private readonly TimeProvider _timeProvider;

    public CpuScanner(
        IPerformanceCounterSessionFactory counters,
        IProcessSnapshotProvider processes,
        IMetricsHistory history,
        IOptions<SamplingOptions> options,
        TimeProvider timeProvider,
        ILogger<CpuScanner> logger)
        : base(logger)
    {
        _counters = counters;
        _processes = processes;
        _history = history;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public override string Id => ScannerId;

    public override string Name => "CPU";

    public override ScanModule Module => ScanModule.Performance;

    public override int Order => 20;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var scanStart = _timeProvider.GetUtcNow();
        var logical = Environment.ProcessorCount;
        collector.AddMetric(PerfNames.CpuLogical, logical, "count");

        // Histórico anterior à varredura: dá às regras minutos de observação em vez de segundos.
        var history = _history.SystemSamples().Where(s => s.Timestamp < scanStart && s.CpuPercent is not null).ToList();
        foreach (var sample in history)
        {
            collector.AddMetric(PerfNames.CpuTotal, sample.CpuPercent!.Value, "%", HistoryTag, sample.Timestamp);
        }

        using var session = _counters.Create();
        var before = _processes.Capture();
        for (var i = 0; i < _options.SampleCount; i++)
        {
            await Task.Delay(_options.Interval, _timeProvider, cancellationToken).ConfigureAwait(false);
            var reading = session.Read();
            Add(collector, PerfNames.CpuTotal, reading.CpuPercent, "%", reading.Timestamp);
            Add(collector, PerfNames.CpuPerformance, reading.ProcessorPerformancePercent, "%", reading.Timestamp);
            Add(collector, PerfNames.CpuBaseFrequency, reading.ProcessorBaseFrequencyMHz, "MHz", reading.Timestamp);
            Add(collector, PerfNames.CpuEffectiveFrequency, reading.EffectiveFrequencyMHz, "MHz", reading.Timestamp);
            Add(collector, PerfNames.CpuQueue, reading.ProcessorQueueLength, "threads", reading.Timestamp);
        }

        var after = _processes.Capture();
        foreach (var usage in ProcessUsageCalculator.TopByCpu(before, after, logical, _options.TopProcesses))
        {
            collector.AddObservation(PerfNames.ObservationProcessCpu, $"{usage.Name} ({usage.Pid})", new Dictionary<string, string>
            {
                ["pid"] = usage.Pid.ToString(CultureInfo.InvariantCulture),
                ["name"] = usage.Name,
                ["cpuPercent"] = usage.CpuPercent.ToString("0.##", CultureInfo.InvariantCulture),
                ["path"] = _processes.TryGetExecutablePath(usage.Pid) ?? string.Empty,
            });
        }

        collector.AddObservation(PerfNames.ObservationSampling, ScannerId, new Dictionary<string, string>
        {
            ["windowSeconds"] = _options.Window.TotalSeconds.ToString("0", CultureInfo.InvariantCulture),
            ["historySeconds"] = (history.Count < 2 ? 0 : (history[^1].Timestamp - history[0].Timestamp).TotalSeconds).ToString("0", CultureInfo.InvariantCulture),
        });
    }

    private static void Add(ScanCollector collector, string name, double? value, string unit, DateTimeOffset timestamp)
    {
        if (value is { } v && double.IsFinite(v))
        {
            collector.AddMetric(name, v, unit, ScanTag, timestamp);
        }
    }
}
