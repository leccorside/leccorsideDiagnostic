using System.Globalization;
using System.Text.Json;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Diagnostics.Performance;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Diagnostics.Hardware;

/// <summary>
/// Inventário de hardware (sistema, BIOS, placa-mãe, processadores, módulos de memória, GPUs).
/// Puramente informativo: gera o HardwareSnapshot salvo a cada varredura, sem findings próprios.
/// </summary>
public sealed class HardwareScanner : DiagnosticScannerBase
{
    public const string ScannerId = "hardware";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly IHardwareInventoryProvider _inventory;

    public HardwareScanner(IHardwareInventoryProvider inventory, ILogger<HardwareScanner> logger)
        : base(logger)
    {
        _inventory = inventory;
    }

    public override string Id => ScannerId;

    public override string Name => "Hardware";

    public override ScanModule Module => ScanModule.Hardware;

    public override int Order => 10;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var inventory = await _inventory.GetAsync(cancellationToken).ConfigureAwait(false);

        if (inventory.TotalPhysicalMemoryBytes is { } total)
        {
            collector.AddMetric(PerfNames.MemoryInstalled, total, "bytes");
        }

        collector.AddMetric(PerfNames.MemoryModules, inventory.MemoryModules.Count, "count");
        var cores = inventory.Processors.Sum(p => p.Cores ?? 0);
        if (cores > 0)
        {
            collector.AddMetric(PerfNames.CpuCores, cores, "count");
        }

        collector.AddObservation(PerfNames.ObservationHardwareInventory, inventory.Computer.Model ?? Environment.MachineName, new Dictionary<string, string>
        {
            ["json"] = JsonSerializer.Serialize(inventory, Json),
            ["warnings"] = inventory.Warnings.Count.ToString(CultureInfo.InvariantCulture),
        });
    }
}
