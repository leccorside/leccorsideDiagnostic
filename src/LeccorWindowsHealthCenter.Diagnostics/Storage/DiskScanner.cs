using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;
using static LeccorWindowsHealthCenter.Diagnostics.Storage.StorageNames;

namespace LeccorWindowsHealthCenter.Diagnostics.Storage;

/// <summary>
/// Discos (seção 14): dispositivos físicos, tipo, saúde informada pelo Windows, TRIM, temperatura (quando o
/// dispositivo informa) e volumes com espaço livre. Só coleta; pouco espaço, saúde e temperatura são decididos pelas regras.
/// </summary>
public sealed class DiskScanner : DiagnosticScannerBase
{
    public const string ScannerId = "disks";

    private readonly IStorageInventoryProvider _inventory;

    public DiskScanner(IStorageInventoryProvider inventory, ILogger<DiskScanner> logger)
        : base(logger)
    {
        _inventory = inventory;
    }

    public override string Id => ScannerId;

    public override string Name => "Discos";

    public override ScanModule Module => ScanModule.Disks;

    public override int Order => 50;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        // Leitura nova: espaço livre de 30 s atrás não serve de evidência numa varredura.
        var inventory = await _inventory.GetAsync(cancellationToken, forceRefresh: true).ConfigureAwait(false);

        foreach (var disk in inventory.Disks)
        {
            collector.AddObservation(ObservationDisk, disk.Name, new Dictionary<string, string>
            {
                ["number"] = Invariant(disk.Number),
                ["name"] = disk.Name,
                ["kind"] = StorageText.Kind(disk.Media, disk.Bus),
                ["bus"] = disk.Bus.ToString(),
                ["media"] = disk.Media.ToString(),
                ["solidState"] = Invariant(disk.IsSolidState),
                ["sizeBytes"] = Invariant(disk.SizeBytes),
                ["health"] = disk.Health.ToString(),
                ["predictedFailure"] = Invariant(disk.PredictedFailure),
                ["operationalStatus"] = string.Join("; ", disk.OperationalStatus),
                ["firmware"] = disk.Firmware ?? string.Empty,
                ["system"] = Invariant(disk.IsSystemDisk),
                ["trim"] = Invariant(disk.TrimEnabled),
                ["ntfsTrim"] = Invariant(inventory.TrimEnabledForNtfs),
                ["temperature"] = Invariant(disk.TemperatureCelsius),
                ["warningTemperature"] = Invariant(disk.WarningTemperatureCelsius),
                ["criticalTemperature"] = Invariant(disk.CriticalTemperatureCelsius),
                ["wear"] = Invariant(disk.WearPercent),
                ["powerOnHours"] = Invariant(disk.PowerOnHours),
                ["elevated"] = Invariant(inventory.ReadWithElevation),
            });

            if (disk.TemperatureCelsius is { } celsius)
            {
                collector.AddMetric(DiskTemperature, celsius, "°C", new Dictionary<string, string> { [DiskTag] = Invariant(disk.Number) });
            }
        }

        foreach (var volume in inventory.Volumes)
        {
            collector.AddObservation(ObservationVolume, volume.Root, new Dictionary<string, string>
            {
                ["root"] = volume.Root,
                ["label"] = volume.Label ?? string.Empty,
                ["fileSystem"] = volume.FileSystem ?? string.Empty,
                ["sizeBytes"] = Invariant(volume.SizeBytes),
                ["freeBytes"] = Invariant(volume.FreeBytes),
                ["system"] = Invariant(volume.IsSystem),
                ["removable"] = Invariant(volume.IsRemovable),
                ["disk"] = Invariant(volume.DiskNumber),
                ["health"] = volume.Health ?? string.Empty,
            });

            var tags = new Dictionary<string, string> { [DriveTag] = volume.Root };
            collector.AddMetric(VolumeFreePercent, volume.FreePercent, "%", tags);
            collector.AddMetric(VolumeFreeBytes, volume.FreeBytes, "bytes", tags);
        }
    }
}
