using System.Globalization;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Diagnostics.Performance;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.Diagnostics.Hardware;

/// <summary>
/// GPU (seção 13): adaptadores, fabricante, VRAM, driver e versão (WMI + registro) e utilização/VRAM em uso
/// (contadores "GPU Engine"/"GPU Adapter Memory"). Eventos de driver (TDR, LiveKernelEvent) chegam com o
/// Event Viewer Analyzer (Passo 5).
/// </summary>
public sealed class GpuScanner : DiagnosticScannerBase
{
    public const string ScannerId = "gpu";

    private readonly IHardwareInventoryProvider _inventory;
    private readonly IPerformanceCounterSessionFactory _counters;
    private readonly SamplingOptions _options;
    private readonly TimeProvider _timeProvider;

    public GpuScanner(
        IHardwareInventoryProvider inventory,
        IPerformanceCounterSessionFactory counters,
        IOptions<SamplingOptions> options,
        TimeProvider timeProvider,
        ILogger<GpuScanner> logger)
        : base(logger)
    {
        _inventory = inventory;
        _counters = counters;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public override string Id => ScannerId;

    public override string Name => "GPU";

    public override ScanModule Module => ScanModule.Hardware;

    public override int Order => 40;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var inventory = await _inventory.GetAsync(cancellationToken).ConfigureAwait(false);
        foreach (var gpu in inventory.Gpus)
        {
            collector.AddObservation(PerfNames.ObservationGpuAdapter, gpu.Name, new Dictionary<string, string>
            {
                ["vendor"] = gpu.Vendor ?? string.Empty,
                ["dedicatedMemoryBytes"] = gpu.DedicatedMemoryBytes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                ["driverVersion"] = gpu.DriverVersion ?? string.Empty,
                ["driverDate"] = gpu.DriverDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                ["status"] = gpu.Status ?? string.Empty,
                ["pnpDeviceId"] = gpu.PnpDeviceId ?? string.Empty,
            });
        }

        collector.AddMetric(PerfNames.GpuCount, inventory.Gpus.Count, "count");

        using var session = _counters.Create();
        for (var i = 0; i < _options.GpuSampleCount; i++)
        {
            await Task.Delay(_options.Interval, _timeProvider, cancellationToken).ConfigureAwait(false);
            var reading = session.Read();
            foreach (var adapter in reading.Gpus)
            {
                var tags = new Dictionary<string, string> { ["luid"] = adapter.AdapterLuid, [PerfNames.SourceTag] = PerfNames.SourceScan };
                collector.AddMetric(PerfNames.GpuUtilization, adapter.UtilizationPercent, "%", tags, reading.Timestamp);
                if (adapter.DedicatedUsageBytes is { } used)
                {
                    collector.AddMetric(PerfNames.GpuDedicatedUsed, used, "bytes", tags, reading.Timestamp);
                }
            }
        }
    }
}
