using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

/// <summary>
/// Cartão de informação: um único título (a informação real, ex.: o nome do processador), legenda opcional abaixo
/// e linhas rótulo/valor. A categoria ("Processador", "GPU") é o título da seção, fora do cartão — nunca um rótulo acima do título.
/// </summary>
public sealed record InfoCard(string Title, string? Caption, IReadOnlyList<InfoRow> Rows);

/// <summary>Hardware: inventário lido via WMI e registro, sem administrador. Campos ausentes aparecem como "não disponível".</summary>
public sealed partial class HardwareViewModel : ObservableObject, IPageLifecycle
{
    private readonly IHardwareInventoryProvider _inventory;
    private readonly UiPreferences _preferences;
    private readonly ILogger<HardwareViewModel> _logger;
    private bool _loaded;

    public HardwareViewModel(IHardwareInventoryProvider inventory, UiPreferences preferences, ILogger<HardwareViewModel> logger)
    {
        _inventory = inventory;
        _preferences = preferences;
        _logger = logger;
        preferences.PropertyChanged += (_, _) => OnPropertyChanged(nameof(AdvancedModeWithWarnings));
    }

    /// <summary>Fontes WMI que falharam: detalhe técnico, exibido só no modo avançado (seção 56).</summary>
    public bool AdvancedModeWithWarnings => _preferences.AdvancedMode && Warnings.Count > 0;

    partial void OnWarningsChanged(IReadOnlyList<string> value) => OnPropertyChanged(nameof(AdvancedModeWithWarnings));

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _capturedText = string.Empty;
    [ObservableProperty] private InfoCard? _system;
    [ObservableProperty] private IReadOnlyList<InfoCard> _processors = [];
    [ObservableProperty] private InfoCard? _memory;
    [ObservableProperty] private IReadOnlyList<InfoCard> _memoryModules = [];
    [ObservableProperty] private IReadOnlyList<InfoCard> _gpus = [];
    [ObservableProperty] private IReadOnlyList<string> _warnings = [];

    public void Activate()
    {
        if (!_loaded && !IsLoading)
        {
            _ = LoadAsync(forceRefresh: false);
        }
    }

    public void Deactivate()
    {
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync(forceRefresh: true);

    private async Task LoadAsync(bool forceRefresh)
    {
        IsLoading = true;
        Error = null;
        try
        {
            var inventory = await _inventory.GetAsync(CancellationToken.None, forceRefresh);
            Apply(inventory);
            _loaded = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao ler o inventário de hardware");
            Error = $"Não foi possível ler o inventário de hardware: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Apply(HardwareInventory inventory)
    {
        var c = inventory.Computer;
        CapturedText = $"Lido em {Fmt.DateTime(inventory.CapturedAt)} via WMI e registro do Windows.";
        System = new InfoCard(Join(c.Manufacturer, c.Model) ?? "Este computador", Join(c.OperatingSystem, c.OsArchitecture),
        [
            new("Windows", Fmt.Text(Join(c.OperatingSystem, c.OsArchitecture))),
            new("Versão / build", Fmt.Text(Join(c.OsVersion, c.OsBuild is null ? null : $"(build {c.OsBuild})"))),
            new("Último boot", Fmt.DateTime(c.LastBoot)),
            new("Placa-mãe", Fmt.Text(c.Motherboard)),
            new("BIOS", Fmt.Text(Join(c.BiosVendor, c.BiosVersion))),
            new("Data da BIOS", Fmt.Date(c.BiosDate)),
        ]);

        Processors = inventory.Processors.Select(p => new InfoCard(p.Name, null,
        [
            new("Fabricante", Fmt.Text(p.Manufacturer)),
            new("Núcleos / threads", p.Cores is null && p.LogicalProcessors is null ? Fmt.NotAvailable : $"{Fmt.Number(p.Cores)} núcleos · {Fmt.Number(p.LogicalProcessors)} threads"),
            new("Clock máximo informado", Fmt.GHz(p.MaxClockMHz)),
            new("Cache L2 / L3", p.L2CacheKb is null && p.L3CacheKb is null ? Fmt.NotAvailable : $"{Fmt.Bytes(p.L2CacheKb * 1024L)} / {Fmt.Bytes(p.L3CacheKb * 1024L)}"),
            new("Virtualização no firmware", p.VirtualizationFirmwareEnabled == true
                ? "Ativada"
                : "Não confirmada (o Windows oculta esse dado quando o Hyper-V está ativo)"),
        ])).ToList();

        var installed = inventory.MemoryModules.Sum(m => m.CapacityBytes);
        Memory = new InfoCard(installed > 0 ? $"{Fmt.Bytes(installed)} instalados" : "Memória instalada não informada", null,
        [
            new("Utilizável pelo Windows", Fmt.Bytes(inventory.TotalPhysicalMemoryBytes)),
            new("Módulos", inventory.MemoryModules.Count == 0 ? Fmt.NotAvailable : inventory.MemoryModules.Count.ToString(CultureInfo.CurrentCulture)),
            new("Velocidade configurada", Speeds(inventory.MemoryModules)),
        ]);

        MemoryModules = inventory.MemoryModules.Select(m => new InfoCard($"{Fmt.Bytes(m.CapacityBytes)} · {Fmt.Text(m.Slot)}", null,
        [
            new("Velocidade", m.SpeedMTs is { } speed ? $"{speed} MT/s" : Fmt.NotAvailable),
            new("Fabricante", Fmt.Text(m.Manufacturer)),
            new("Part number", Fmt.Text(m.PartNumber)),
        ])).ToList();

        Gpus = inventory.Gpus.Select(g => new InfoCard(g.Name, null,
        [
            new("Fabricante", Fmt.Text(g.Vendor)),
            new("Memória dedicada (VRAM)", Fmt.Bytes(g.DedicatedMemoryBytes)),
            new("Versão do driver", Fmt.Text(g.DriverVersion)),
            new("Data do driver", Fmt.Date(g.DriverDate)),
            new("Status do dispositivo", g.Status switch { "OK" => "OK", null => Fmt.NotAvailable, var other => other }),
        ])).ToList();

        Warnings = inventory.Warnings;
    }

    private static string Speeds(IReadOnlyList<MemoryModuleInfo> modules)
    {
        var speeds = modules.Where(m => m.SpeedMTs is not null).Select(m => m.SpeedMTs!.Value).Distinct().Order().ToList();
        return speeds.Count == 0 ? Fmt.NotAvailable : string.Join(" / ", speeds.Select(s => $"{s} MT/s"));
    }

    private static string? Join(string? first, string? second) =>
        string.IsNullOrWhiteSpace(first) ? second : string.IsNullOrWhiteSpace(second) ? first : $"{first} {second}";
}
