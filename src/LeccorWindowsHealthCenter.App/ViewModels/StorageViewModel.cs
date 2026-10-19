using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Storage;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.App.ViewModels;

/// <summary>Volume com barra de uso. <see cref="UsedPercent"/> é a fração ocupada (0–100).</summary>
public sealed record VolumeRow(string Root, string Title, string Caption, string Value, double UsedPercent, bool SolidState, long UsedBytes);

/// <summary>Local de temporários/cache medido. <see cref="ToolTip"/> lista os caminhos reais.</summary>
public sealed record LocationRow(string Title, string Value, string Detail, string? ToolTip);

/// <summary>Linha da árvore de pastas. <see cref="Node"/> null = linha de resumo, sem navegação. <see cref="Glyph"/>: ícone Segoe Fluent.</summary>
public sealed record FolderRow(string Name, string Value, string Detail, double Percent, FolderNode? Node, string ToolTip, string Glyph = "")
{
    public bool CanOpen => Node is { Children.Count: > 0 };
}

public sealed record FileRow(string Name, string Folder, string Value, string Detail, string ToolTip);

public sealed record BreadcrumbItem(string Name, FolderNode Node, bool IsLast);

/// <summary>
/// Armazenamento (seções 14–15): discos físicos, volumes, temporários/caches medidos e análise de espaço navegável
/// sob demanda. Somente leitura — nada é apagado nesta tela.
/// </summary>
public sealed partial class StorageViewModel : ObservableObject, IPageLifecycle, IDisposable
{
    private const int VisibleFiles = 20;

    /// <summary>Arquivos do sistema que aparecem entre os maiores e assustam quem não os conhece.</summary>
    private static readonly Dictionary<string, string> KnownSystemFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pagefile.sys"] = "Arquivo de paginação do Windows (memória virtual)",
        ["swapfile.sys"] = "Arquivo de troca dos aplicativos da Store",
        ["hiberfil.sys"] = "Arquivo de hibernação/inicialização rápida",
        ["MEMORY.DMP"] = "Despejo de memória de um travamento",
    };

    private readonly IStorageInventoryProvider _inventory;
    private readonly ISpaceAnalyzer _analyzer;
    private readonly UiPreferences _preferences;
    private readonly SpaceAnalysisOptions _options;
    private readonly ILogger<StorageViewModel> _logger;
    private readonly List<FolderNode> _path = [];
    private CancellationTokenSource? _analysisCts;
    private SpaceAnalysis? _analysis;
    private long? _analyzedVolumeUsed;
    private bool _loaded;

    public StorageViewModel(
        IStorageInventoryProvider inventory,
        ISpaceAnalyzer analyzer,
        UiPreferences preferences,
        IOptions<SpaceAnalysisOptions> options,
        ILogger<StorageViewModel> logger)
    {
        _options = options.Value;
        _inventory = inventory;
        _analyzer = analyzer;
        _preferences = preferences;
        _logger = logger;
        preferences.PropertyChanged += (_, _) => OnPropertyChanged(nameof(AdvancedModeWithWarnings));
    }

    public bool AdvancedModeWithWarnings => _preferences.AdvancedMode && Warnings.Count > 0;

    partial void OnWarningsChanged(IReadOnlyList<string> value) => OnPropertyChanged(nameof(AdvancedModeWithWarnings));

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _capturedText = string.Empty;
    [ObservableProperty] private IReadOnlyList<VolumeRow> _volumes = [];
    [ObservableProperty] private IReadOnlyList<InfoCard> _disks = [];
    [ObservableProperty] private string _disksNote = string.Empty;
    [ObservableProperty] private string _disksEmptyText = string.Empty;
    [ObservableProperty] private IReadOnlyList<string> _warnings = [];

    [ObservableProperty] private bool _isMeasuring;
    [ObservableProperty] private string _locationsTitle = "Medindo temporários e caches…";
    [ObservableProperty] private string _locationsCaption = string.Empty;
    [ObservableProperty] private IReadOnlyList<LocationRow> _locations = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelAnalysisCommand))]
    private bool _isAnalyzing;

    [ObservableProperty] private string _analysisStatus = "Escolha um volume e clique em Analisar para ver as maiores pastas e arquivos. A leitura pode levar alguns minutos em discos grandes.";
    [ObservableProperty] private bool _hasAnalysis;
    [ObservableProperty] private IReadOnlyList<BreadcrumbItem> _breadcrumb = [];
    [ObservableProperty] private string _currentTitle = string.Empty;
    [ObservableProperty] private string _currentCaption = string.Empty;
    [ObservableProperty] private IReadOnlyList<FolderRow> _folders = [];
    [ObservableProperty] private IReadOnlyList<FileRow> _largestFiles = [];
    [ObservableProperty] private IReadOnlyList<FileRow> _oldFiles = [];
    [ObservableProperty] private string _largestFilesEmptyText = string.Empty;
    [ObservableProperty] private string _oldFilesEmptyText = string.Empty;

    public void Activate()
    {
        if (!_loaded && !IsLoading)
        {
            _loaded = true;
            _ = LoadAsync(forceRefresh: false);
        }
    }

    public void Deactivate()
    {
        // A análise continua com a tela fechada: o usuário pode navegar e voltar. Cancelar é explícito.
    }

    /// <summary>Fechar o aplicativo interrompe uma análise em andamento (o próprio Analyze descarta o token ao terminar).</summary>
    public void Dispose() => _analysisCts?.Cancel();

    [RelayCommand]
    private Task Refresh() => LoadAsync(forceRefresh: true);

    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task Analyze(VolumeRow? volume)
    {
        if (volume is null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _analysisCts = cts;
        IsAnalyzing = true;
        HasAnalysis = false;
        AnalysisStatus = $"Lendo {volume.Root}…";
        var progress = new Progress<SpaceProgress>(p => AnalysisStatus = string.Create(CultureInfo.CurrentCulture,
            $"Lendo {volume.Root} — {p.FilesScanned:#,##0} arquivos · {Fmt.Bytes(p.BytesScanned)} · {p.CurrentFolder}"));
        try
        {
            _analysis = await _analyzer.AnalyzeAsync(volume.Root, volume.SolidState, progress, cts.Token);
            _analyzedVolumeUsed = volume.UsedBytes;
            _path.Clear();
            _path.Add(_analysis.Root);
            ShowCurrent();
            ApplyFiles(_analysis);
            HasAnalysis = true;
            AnalysisStatus = string.Create(CultureInfo.CurrentCulture,
                $"Analisado em {Fmt.Duration(_analysis.Duration)} · {_analysis.Root.FileCount:#,##0} arquivos · {Fmt.DateTime(_analysis.CapturedAt)}");
        }
        catch (OperationCanceledException)
        {
            AnalysisStatus = $"Análise de {volume.Root} cancelada. Nada foi alterado.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na análise de espaço de {Root}", volume.Root);
            AnalysisStatus = $"Não foi possível analisar {volume.Root}: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_analysisCts, cts))
            {
                _analysisCts = null;
            }

            cts.Dispose();
            IsAnalyzing = false;
        }
    }

    private bool CanAnalyze(VolumeRow? volume) => !IsAnalyzing;

    [RelayCommand(CanExecute = nameof(IsAnalyzing))]
    private void CancelAnalysis() => _analysisCts?.Cancel();

    [RelayCommand]
    private void OpenFolder(FolderRow? row)
    {
        if (row?.Node is { Children.Count: > 0 } node)
        {
            _path.Add(node);
            ShowCurrent();
        }
    }

    [RelayCommand]
    private void NavigateTo(BreadcrumbItem? item)
    {
        var index = item is null ? -1 : _path.IndexOf(item.Node);
        if (index >= 0 && index < _path.Count - 1)
        {
            _path.RemoveRange(index + 1, _path.Count - index - 1);
            ShowCurrent();
        }
    }

    private async Task LoadAsync(bool forceRefresh)
    {
        IsLoading = true;
        Error = null;
        var measure = MeasureLocationsAsync();
        try
        {
            Apply(await _inventory.GetAsync(CancellationToken.None, forceRefresh));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao ler o inventário de armazenamento");
            Error = $"Não foi possível ler os discos: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }

        await measure;
    }

    private async Task MeasureLocationsAsync()
    {
        IsMeasuring = true;
        LocationsTitle = "Medindo temporários e caches…";
        LocationsCaption = string.Empty;
        try
        {
            ApplyLocations(await _analyzer.MeasureLocationsAsync(StorageLocations.All, CancellationToken.None));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao medir temporários e caches");
            LocationsTitle = "Não foi possível medir temporários e caches";
            LocationsCaption = ex.Message;
        }
        finally
        {
            IsMeasuring = false;
        }
    }

    private void Apply(StorageInventory inventory)
    {
        CapturedText = $"Lido em {Fmt.DateTime(inventory.CapturedAt)} via WMI de armazenamento do Windows.";
        Warnings = inventory.Warnings;

        Volumes = inventory.Volumes.Select(v =>
        {
            var disk = inventory.Disks.FirstOrDefault(d => d.Number == v.DiskNumber);
            var drive = v.Root.TrimEnd('\\');
            var title = v.Label is { } label ? $"{drive}  {label}" : drive;
            var caption = string.Join(" · ", new[]
            {
                v.IsSystem ? "Windows" : null,
                v.FileSystem,
                disk is null ? null : InventoryText.Clean(disk.Name),
                disk is null ? null : StorageText.Kind(disk.Media, disk.Bus),
                v.IsRemovable ? "removível" : null,
            }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var (free, size) = Shared.Formatting.ByteSize.FormatPair(v.FreeBytes, v.SizeBytes, CultureInfo.CurrentCulture);
            return new VolumeRow(v.Root, title, caption, $"{free} livres de {size}", v.SizeBytes > 0 ? v.UsedBytes * 100.0 / v.SizeBytes : 0, disk?.IsSolidState ?? false, v.UsedBytes);
        }).ToList();

        Disks = inventory.Disks.Select(d => DiskCard(d, inventory)).ToList();
        DisksEmptyText = inventory.Disks.Count == 0
            ? "O Windows não devolveu nenhum disco físico. Os volumes acima continuam corretos; use “Ler novamente” e, se persistir, veja o modo avançado nas Configurações."
            : string.Empty;
        DisksNote = inventory.ReadWithElevation
            ? "Temperatura e vida útil aparecem só quando o próprio disco informa. Nada é estimado."
            : "Vida útil restante e horas ligadas exigem administrador: sem ele, o Windows nega o acesso a esses dados. Temperatura aparece só quando o próprio disco informa. Nada é estimado.";
    }

    private static InfoCard DiskCard(PhysicalDiskInfo disk, StorageInventory inventory)
    {
        var volumes = inventory.Volumes.Where(v => v.DiskNumber == disk.Number).ToList();
        var used = volumes.Count == 0 ? (long?)null : volumes.Sum(v => v.UsedBytes);
        var free = volumes.Count == 0 ? (long?)null : volumes.Sum(v => v.FreeBytes);
        var health = StorageText.HealthText(disk.Health, disk.PredictedFailure);
        var caption = string.Join(" · ", new[]
        {
            StorageText.Kind(disk.Media, disk.Bus),
            disk.SizeBytes is { } size ? Fmt.Bytes(size) : null,
            disk.IsSystemDisk ? "disco do Windows" : null,
        }.Where(s => s is not null));

        // "Não se aplica" só quando o tipo é HDD comprovado; tipo desconhecido não autoriza a afirmação.
        var trim = disk.Media == DiskMediaKind.Hdd && disk.Bus != DiskBusKind.Nvme
            ? ("Não se aplica (HDD)", StatusTone.Normal)
            : (disk.TrimEnabled, inventory.TrimEnabledForNtfs) switch
            {
                (false, _) => ("Desativado pelo dispositivo", StatusTone.Warning),
                (_, false) when disk.IsSolidState => ("Desativado no Windows", StatusTone.Warning),
                (true, _) => ("Ativo", StatusTone.Normal),
                (null, true) when disk.IsSolidState => ("Ativo no Windows (o dispositivo não confirma)", StatusTone.Normal),
                _ => (Fmt.NotAvailable, StatusTone.Normal),
            };

        return new InfoCard(Fmt.Text(InventoryText.Clean(disk.Name) ?? disk.Name), caption,
        [
            new("Utilizado", volumes.Count == 0 ? "sem volume com letra" : Fmt.Bytes(used)),
            new("Livre", volumes.Count == 0 ? "sem volume com letra" : Fmt.Bytes(free)),
            new("Saúde (vida útil)", disk.WearPercent is { } wear ? $"{100 - wear}% restante" : Fmt.NotAvailable),
            new("Estado SMART", health?.Text ?? Fmt.NotAvailable, health?.Tone ?? StatusTone.Normal),
            new("Temperatura", disk.TemperatureCelsius is { } celsius ? string.Create(CultureInfo.CurrentCulture, $"{celsius:0} °C") : Fmt.NotAvailable),
            new("TRIM", trim.Item1, trim.Item2),
            new("Partições", Partitions(disk)),
            new("Firmware", Fmt.Text(disk.Firmware)),
        ]);
    }

    private static string Partitions(PhysicalDiskInfo disk)
    {
        if (disk.Partitions.Count == 0)
        {
            return Fmt.NotAvailable;
        }

        var parts = disk.Partitions
            .OrderBy(p => p.DriveLetter is null)
            .ThenBy(p => p.DriveLetter)
            .GroupBy(p => p.DriveLetter is { } letter ? $"{letter}:" : StorageText.PartitionText(p.Kind))
            .Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key);
        var style = disk.PartitionStyle is null ? string.Empty : $"{disk.PartitionStyle} · ";
        return $"{style}{Fmt.Count(disk.Partitions.Count, "partição", "partições")}: {string.Join(", ", parts)}";
    }

    private void ApplyLocations(IReadOnlyList<LocationMeasure> measures)
    {
        var counted = measures.Where(m => m.Location.Kind != StorageLocationKind.RecycleBin).ToList();
        var total = counted.Sum(m => m.SizeBytes);
        var recycle = measures.FirstOrDefault(m => m.Location.Kind == StorageLocationKind.RecycleBin);
        LocationsTitle = $"{Fmt.Bytes(total)} em temporários e caches";
        LocationsCaption = (recycle is { Found: true } ? $"A Lixeira ({Fmt.Bytes(recycle.SizeBytes)}) fica fora dessa conta. " : string.Empty)
            + "Só medição: nada é apagado aqui. A limpeza segura por categorias chega na tela Limpeza.";

        Locations = measures
            .OrderByDescending(m => m.SizeBytes)
            .Select(m => new LocationRow(
                m.Location.Title,
                m.Found ? Fmt.Bytes(m.SizeBytes) : "não encontrado",
                m.Location.Description + (m.InaccessibleCount > 0 ? " Parte das pastas não pôde ser lida sem administrador; o total pode ser maior." : string.Empty),
                m.ResolvedPaths.Count > 0 ? string.Join(Environment.NewLine, m.ResolvedPaths) : null))
            .ToList();
    }

    private void ShowCurrent()
    {
        var node = _path[^1];
        Breadcrumb = _path.Select((n, i) => new BreadcrumbItem(i == 0 ? n.Path.TrimEnd('\\') : n.Name, n, i == _path.Count - 1)).ToList();
        CurrentTitle = $"{Fmt.Bytes(node.SizeBytes)} em {(node.Path.Length <= 3 ? node.Path : node.Name)}";

        var notes = new List<string>
        {
            string.Create(CultureInfo.CurrentCulture, $"{node.FileCount:#,##0} arquivos"),
        };
        if (node.InaccessibleCount > 0)
        {
            notes.Add($"{Fmt.Count(node.InaccessibleCount, "pasta sem permissão de leitura", "pastas sem permissão de leitura")} (o total real pode ser maior)");
        }

        if (node.CloudOnlyBytes > 0)
        {
            notes.Add($"{Fmt.Bytes(node.CloudOnlyBytes)} só na nuvem (OneDrive), sem ocupar o disco");
        }

        // Links físicos (WinSxS) aparecem em mais de uma pasta: a soma pode passar do espaço realmente usado.
        if (_path.Count == 1 && _analyzedVolumeUsed is { } volumeUsed && node.SizeBytes > volumeUsed)
        {
            notes.Add($"a soma passa dos {Fmt.Bytes(volumeUsed)} usados no volume porque arquivos com links físicos (como os de Windows\\WinSxS) contam em cada pasta onde aparecem");
        }
        else if (node.Path.Contains(@"\Windows", StringComparison.OrdinalIgnoreCase))
        {
            notes.Add("pastas do Windows com links físicos (como WinSxS) aparecem maiores do que ocupam");
        }

        CurrentCaption = string.Join(" · ", notes);

        double Share(long bytes) => node.SizeBytes > 0 ? bytes * 100.0 / node.SizeBytes : 0;
        var rows = node.Children.Select(c => new FolderRow(
            c.Name,
            Fmt.Bytes(c.SizeBytes),
            c.Inaccessible ? "sem permissão de leitura" : string.Create(CultureInfo.CurrentCulture, $"{Share(c.SizeBytes):0}% · {c.FileCount:#,##0} arquivos"),
            Share(c.SizeBytes),
            c,
            c.Path)).ToList();

        if (node.FilesBytes > 0)
        {
            rows.Add(new FolderRow("Arquivos nesta pasta", Fmt.Bytes(node.FilesBytes), string.Create(CultureInfo.CurrentCulture, $"{Share(node.FilesBytes):0}%"), Share(node.FilesBytes), null, node.Path, ""));
        }

        if (node.HiddenChildrenCount > 0)
        {
            rows.Add(new FolderRow(
                Fmt.Count(node.HiddenChildrenCount, "pasta menor", "pastas menores"),
                Fmt.Bytes(node.HiddenChildrenBytes),
                "somadas para a lista não ficar longa",
                Share(node.HiddenChildrenBytes),
                null,
                node.Path));
        }

        Folders = rows;
    }

    private void ApplyFiles(SpaceAnalysis analysis)
    {
        LargestFiles = analysis.LargestFiles.Take(VisibleFiles).Select(f => ToFileRow(f, Fmt.Date(f.LastWrite))).ToList();
        LargestFilesEmptyText = LargestFiles.Count == 0 ? "Nenhum arquivo encontrado." : string.Empty;

        OldFiles = analysis.OldFiles.Take(VisibleFiles).Select(f => ToFileRow(f, $"sem alteração desde {Fmt.Date(f.LastWrite)}")).ToList();
        OldFilesEmptyText = OldFiles.Count == 0 ? $"Nenhum arquivo acima de {Fmt.Bytes(_options.OldFileMinimumBytes)} sem alteração há mais de {Fmt.Count(Math.Round(_options.OldFileAge.TotalDays / 365), "ano", "anos")}." : string.Empty;
    }

    private static FileRow ToFileRow(FileEntry file, string detail)
    {
        var name = Path.GetFileName(file.Path);
        var known = KnownSystemFiles.TryGetValue(name, out var description) ? $"{description} · " : string.Empty;
        return new FileRow(name, Path.GetDirectoryName(file.Path) ?? string.Empty, Fmt.Bytes(file.SizeBytes), known + detail, file.Path);
    }
}
