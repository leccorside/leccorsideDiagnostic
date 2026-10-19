using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.Startup;
using LeccorWindowsHealthCenter.Diagnostics.Storage;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Diagnostics.Startup;

/// <summary>
/// Inicialização (seção 19) na varredura: uma observação por entrada (Run/RunOnce, pastas Inicializar, tarefas de
/// logon/boot e serviços automáticos). Só coleta — executável inexistente e pasta temporária são decididos pelas regras.
/// </summary>
public sealed class StartupScanner : DiagnosticScannerBase
{
    public const string ScannerId = "startup";
    public const string ObservationEntry = "startup.entry";
    public const string EntryCount = "startup.entries";
    public const string EnabledCount = "startup.entries.enabled";

    private readonly IStartupInventoryProvider _inventory;

    public StartupScanner(IStartupInventoryProvider inventory, ILogger<StartupScanner> logger)
        : base(logger)
    {
        _inventory = inventory;
    }

    public override string Id => ScannerId;

    public override string Name => "Inicialização";

    public override ScanModule Module => ScanModule.Startup;

    public override int Order => 70;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var inventory = await _inventory.GetAsync(cancellationToken, forceRefresh: true).ConfigureAwait(false);
        foreach (var entry in inventory.Entries)
        {
            collector.AddObservation(ObservationEntry, entry.Name, new Dictionary<string, string>
            {
                ["id"] = entry.Id,
                ["name"] = entry.Name,
                ["source"] = entry.Source.ToString(),
                ["location"] = entry.Location,
                ["command"] = entry.Command ?? string.Empty,
                ["executable"] = entry.ExecutablePath ?? string.Empty,
                ["exists"] = StorageNames.Invariant(entry.ExecutableExists),
                ["enabled"] = StorageNames.Invariant(entry.Enabled),
                ["windows"] = StorageNames.Invariant(entry.IsWindowsComponent),
                ["user"] = StorageNames.Invariant(entry.CurrentUserOnly),
            });
        }

        collector.AddMetric(EntryCount, inventory.Entries.Count, "count");
        collector.AddMetric(EnabledCount, inventory.Entries.Count(e => e.Enabled == true), "count");
    }
}
