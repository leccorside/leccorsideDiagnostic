using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Storage;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.App.ViewModels;

/// <summary>Linha com caminho real: "Mostrar no Explorer", "Copiar caminho" e Ctrl+C usam <see cref="Path"/>.</summary>
public interface IPathRow
{
    string? Path { get; }

    /// <summary>Comando útil para o item (consulta ou limpeza oficial), copiável pelo menu; o app não o executa.</summary>
    string? Command { get; }
}

/// <summary>
/// Volume com barra de uso. <see cref="UsedPercent"/> é a fração ocupada (0–100). <see cref="Tone"/> vem da MESMA decisão
/// da regra de pouco espaço (<see cref="LowFreeSpaceRule.Assess"/>): a barra só fica âmbar/vermelha quando a varredura acusaria.
/// </summary>
public sealed record VolumeRow(string Root, string Title, string Caption, string Value, double UsedPercent, bool SolidState, long UsedBytes, StatusTone Tone = StatusTone.Normal) : IPathRow
{
    public bool IsWarning => Tone == StatusTone.Warning;

    public bool IsCritical => Tone == StatusTone.Critical;

    public string? Path => Root;

    public string? Command => null;

    public string AccessibleName => $"{Title}, {Value}";
}

/// <summary>Disco na lista compacta (muitos discos): uma linha que abre o cartão completo. Disco crítico já vem aberto.</summary>
public sealed record DiskSummaryRow(string Title, string Summary, StatusTone Tone, InfoCard Card)
{
    public bool IsWarning => Tone == StatusTone.Warning;

    public bool IsCritical => Tone == StatusTone.Critical;

    public string AccessibleName => $"{Title}, {Summary}";
}

/// <summary>
/// Diagnóstico no topo da tela ("laudo primeiro"): o que aconteceu, como sabemos e o que fazer. Mesmos critérios das
/// regras da varredura (DiskHealthRule e LowFreeSpaceRule) — tela e varredura não podem discordar.
/// </summary>
public sealed record StorageAlert(string Title, string Body, string? Evidence, string Action, StatusTone Tone, VolumeRow? AnalyzeTarget)
{
    public bool IsCritical => Tone == StatusTone.Critical;

    /// <summary>Ícone Segoe Fluent: ErrorBadge (crítico) ou Warning.</summary>
    public string Glyph => IsCritical ? "\uEA39" : "\uE7BA";

    /// <summary>O nível dito em palavras (leitor de tela não depende da cor).</summary>
    public string Level => IsCritical ? "Crítico" : "Atenção";

    public string AnalyzeLabel => AnalyzeTarget is null ? string.Empty : $"Analisar {AnalyzeTarget.Root.TrimEnd('\\')}";

    /// <summary>O alerta inteiro numa frase, para quem chega a ele pelo Tab com leitor de tela.</summary>
    public string AccessibleName => $"{Level}: {Title}. {Body} {Action}";
}

/// <summary>Local de temporários/cache medido. <see cref="ToolTip"/> lista os caminhos reais.</summary>
public sealed record LocationRow(string Title, string Value, string Detail, string? ToolTip, string? Path, string? Command = null) : IPathRow
{
    public string AccessibleName => $"{Title}, {Value}";

    /// <summary>A Lixeira não tem uma pasta única para mostrar no Explorer.</summary>
    public bool HasPath => Path is not null;
}

/// <summary>Linha da árvore de pastas. <see cref="Node"/> null = linha de resumo. <see cref="Glyph"/>: ícone Segoe Fluent.</summary>
public sealed record FolderRow(string Name, string Value, string Detail, double Percent, FolderNode? Node, string? Path, string Glyph = "\uE8B7", string? Hint = null, string? Command = null) : IPathRow
{
    /// <summary>A linha tem UMA ação: entrar na pasta. Sem subpastas, não faz nada (o Explorer fica num botão próprio).</summary>
    public bool CanOpen => Node is { Children.Count: > 0 };

    public string AccessibleName => $"{Name}, {Value}, {Detail}{(CanOpen ? ", abre a pasta" : ", sem subpastas listadas")}";

    public string RevealName => $"Mostrar {Name} no Explorer";
}

public sealed record FileRow(string Name, string Folder, string Value, string Detail, string? Path, string? Hint, bool DoNotDelete, string? Command = null) : IPathRow
{
    public string AccessibleName => $"{Name}, {Value}, em {Folder}";
}

public sealed record BreadcrumbItem(string Name, FolderNode Node, bool IsLast);

/// <summary>Análise já feita (volume ou pasta), guardada para trocar sem ler o disco de novo.</summary>
public sealed record AnalysisChip(string Key, string Label, bool IsCurrent);

/// <summary>
/// Armazenamento (seções 14–15), ordenado como um laudo: primeiro o diagnóstico (disco com falha, pouco espaço), depois
/// volumes e a análise de espaço navegável, discos físicos e temporários/caches. Somente leitura — nada é apagado aqui.
/// </summary>
public sealed partial class StorageViewModel : ObservableObject, IPageLifecycle, IDisposable
{
    private const int VisibleFiles = 20;

    /// <summary>A partir daqui, cartões completos viram uma lista compacta (24 baias = 24 cartões de 9 linhas).</summary>
    private const int MaxDiskCards = 4;

    /// <summary>A partir daqui, cada volume ocupa uma linha só (24 volumes empurravam a análise três telas abaixo).</summary>
    private const int MaxFullVolumeRows = 6;

    private const long Mb = 1024L * 1024;

    /// <summary>Pastas mostradas antes de "Mostrar mais": até 500 linhas sem virtualização pesariam a tela.</summary>
    private const int VisibleFolders = 25;

    private readonly IStorageInventoryProvider _inventory;
    private readonly ISpaceAnalyzer _analyzer;
    private readonly IShellLauncher _shell;
    private readonly IFolderPicker _picker;
    private readonly UiPreferences _preferences;
    private readonly SpaceAnalysisOptions _options;
    private readonly IRuleSettings _ruleSettings;
    private readonly LowFreeSpaceRule _lowSpace = new();
    private readonly ILogger<StorageViewModel> _logger;
    private readonly Dictionary<string, AnalysisState> _analyses = new(StringComparer.OrdinalIgnoreCase);
    private string? _currentKey;
    private int _loadVersion;
    private CancellationTokenSource? _analysisCts;
    private bool _loaded;

    public StorageViewModel(
        IStorageInventoryProvider inventory,
        ISpaceAnalyzer analyzer,
        IShellLauncher shell,
        IFolderPicker picker,
        UiPreferences preferences,
        IOptions<SpaceAnalysisOptions> options,
        IRuleSettings ruleSettings,
        ILogger<StorageViewModel> logger)
    {
        _inventory = inventory;
        _analyzer = analyzer;
        _shell = shell;
        _picker = picker;
        _preferences = preferences;
        _options = options.Value;
        _ruleSettings = ruleSettings;
        _logger = logger;
        preferences.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(AdvancedModeWithWarnings));
            OnPropertyChanged(nameof(ShowTechnicalError));
        };
    }

    /// <summary>A tela pede para pôr o foco na primeira pasta (depois de analisar ou navegar) — teclado não se perde.</summary>
    public event EventHandler? FolderListChanged;

    public bool AdvancedModeWithWarnings => _preferences.AdvancedMode && Warnings.Count > 0;

    /// <summary>Detalhe técnico de uma falha (mensagem da exceção): só no modo avançado.</summary>
    public bool ShowTechnicalError => _preferences.AdvancedMode && !string.IsNullOrEmpty(TechnicalError);

    partial void OnWarningsChanged(IReadOnlyList<string> value) => OnPropertyChanged(nameof(AdvancedModeWithWarnings));

    partial void OnTechnicalErrorChanged(string? value) => OnPropertyChanged(nameof(ShowTechnicalError));

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _technicalError;
    [ObservableProperty] private string _capturedText = string.Empty;

    [ObservableProperty] private IReadOnlyList<StorageAlert> _alerts = [];

    /// <summary>Resumo dos alertas ("2 críticos, 1 de atenção"), anunciado ao leitor de tela quando há crítico.</summary>
    [ObservableProperty] private string _alertSummary = string.Empty;
    [ObservableProperty] private string _allClearText = string.Empty;
    [ObservableProperty] private string _inventoryEmptyText = string.Empty;

    [ObservableProperty] private IReadOnlyList<VolumeRow> _volumes = [];
    [ObservableProperty] private bool _compactVolumes;
    [ObservableProperty] private IReadOnlyList<InfoCard> _disks = [];
    [ObservableProperty] private IReadOnlyList<DiskSummaryRow> _diskRows = [];
    [ObservableProperty] private string _disksNote = string.Empty;
    [ObservableProperty] private string _disksEmptyText = string.Empty;
    [ObservableProperty] private IReadOnlyList<string> _warnings = [];

    [ObservableProperty] private bool _isMeasuring;
    [ObservableProperty] private string _locationsTitle = "Medindo temporários e caches…";
    [ObservableProperty] private string _locationsCaption = string.Empty;
    [ObservableProperty] private IReadOnlyList<LocationRow> _locations = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelAnalysisCommand))]
    private bool _isAnalyzing;

    [ObservableProperty] private bool _showAnalysisSection;

    /// <summary>Progresso aproximado da leitura (bytes lidos ÷ espaço usado), só quando o usado do volume é conhecido.</summary>
    [ObservableProperty] private double _analysisPercent;
    [ObservableProperty] private bool _hasDeterminateProgress;
    [ObservableProperty] private string _analysisStatus = "Escolha um volume e clique em Analisar para ver as maiores pastas e arquivos. Em discos grandes, a leitura pode levar alguns minutos.";
    [ObservableProperty] private bool _hasAnalysis;
    [ObservableProperty] private IReadOnlyList<AnalysisChip> _analysisChips = [];
    [ObservableProperty] private IReadOnlyList<BreadcrumbItem> _breadcrumb = [];
    [ObservableProperty] private string _currentTitle = string.Empty;
    [ObservableProperty] private string _currentCaption = string.Empty;
    [ObservableProperty] private IReadOnlyList<FolderRow> _folders = [];
    [ObservableProperty] private bool _showAllFolders;
    [ObservableProperty] private string _moreFoldersText = string.Empty;

    [ObservableProperty] private bool _sortBySize = true;
    [ObservableProperty] private bool _sortByName;
    [ObservableProperty] private string _fileFilter = string.Empty;
    [ObservableProperty] private bool _showAllFiles;
    [ObservableProperty] private string _filesScope = string.Empty;
    [ObservableProperty] private IReadOnlyList<FileRow> _largestFiles = [];
    [ObservableProperty] private IReadOnlyList<FileRow> _oldFiles = [];
    [ObservableProperty] private string _largestFilesEmptyText = string.Empty;
    [ObservableProperty] private string _oldFilesEmptyText = string.Empty;
    [ObservableProperty] private string _oldFilesCaption = string.Empty;
    [ObservableProperty] private string _showAllFilesLabel = string.Empty;
    [ObservableProperty] private bool _canShowAllFiles;

    partial void OnSortBySizeChanged(bool value)
    {
        SortByName = !value;
        ShowCurrent(focus: false);
    }

    partial void OnSortByNameChanged(bool value) => SortBySize = !value;

    partial void OnFileFilterChanged(string value) => ApplyFiles();

    partial void OnShowAllFilesChanged(bool value) => ApplyFiles();

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
    private Task Analyze(VolumeRow? volume) =>
        volume is null ? Task.CompletedTask : AnalyzeTargetAsync(volume.Root, volume.Root.TrimEnd('\\'), volume.SolidState, volume.UsedBytes);

    [RelayCommand(CanExecute = nameof(CanAnalyzeFolder))]
    private Task AnalyzeFolder()
    {
        var folder = _picker.PickFolder("Escolha a pasta para analisar");
        if (folder is null)
        {
            return Task.CompletedTask;
        }

        // Paralelismo pelo disco da pasta (SSD lê várias pastas ao mesmo tempo; HDD ou desconhecido, uma).
        var root = Path.GetPathRoot(folder) ?? folder;
        var volume = Volumes.FirstOrDefault(v => string.Equals(v.Root, root, StringComparison.OrdinalIgnoreCase));
        return AnalyzeTargetAsync(folder, folder, volume?.SolidState ?? false, volumeUsed: null);
    }

    private bool CanAnalyze(VolumeRow? volume) => !IsAnalyzing;

    private bool CanAnalyzeFolder() => !IsAnalyzing;

    [RelayCommand(CanExecute = nameof(IsAnalyzing))]
    private void CancelAnalysis() => _analysisCts?.Cancel();

    /// <summary>Linha de pasta: entra se tiver subpastas. "Mostrar no Explorer" é um botão separado (uma ação por controle).</summary>
    [RelayCommand]
    private void OpenFolder(FolderRow? row)
    {
        if (row is null || Current is not { } state)
        {
            return;
        }

        if (row.Node is { Children.Count: > 0 } node)
        {
            state.Path.Add(node);
            ShowAllFolders = false;
            ShowCurrent(focus: true);
        }
    }

    [RelayCommand]
    private void NavigateTo(BreadcrumbItem? item)
    {
        if (Current is not { } state || item is null)
        {
            return;
        }

        var index = state.Path.IndexOf(item.Node);
        if (index >= 0 && index < state.Path.Count - 1)
        {
            state.Path.RemoveRange(index + 1, state.Path.Count - index - 1);
            ShowAllFolders = false;
            ShowCurrent(focus: true);
        }
    }

    /// <summary>Backspace / Alt+← sobem um nível.</summary>
    [RelayCommand]
    private void NavigateUp()
    {
        if (Current is { Path.Count: > 1 } state)
        {
            state.Path.RemoveAt(state.Path.Count - 1);
            ShowAllFolders = false;
            ShowCurrent(focus: true);
        }
    }

    [RelayCommand]
    private void ShowAnalysis(AnalysisChip? chip)
    {
        if (chip is not null && _analyses.ContainsKey(chip.Key) && !IsAnalyzing)
        {
            _currentKey = chip.Key;
            ShowCurrent(focus: false);
            UpdateChips();
            AnalysisStatus = StatusFor(Current!);
        }
    }

    [RelayCommand]
    private void Reveal(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (!_shell.Reveal(path))
        {
            AnalysisStatus = $"Não foi possível mostrar {ShortPath(path)} no Explorer: o item pode ter sido movido ou apagado depois da análise.";
        }
    }

    [RelayCommand]
    private void CopyPath(string? path) => Copy(path, $"Caminho copiado: {ShortPath(path ?? string.Empty)}");

    [RelayCommand]
    private void CopyCommand(string? command) => Copy(command, $"Comando copiado: {command}. O Leccor não executa comandos por você.");

    [RelayCommand]
    private void ShowMoreFolders()
    {
        ShowAllFolders = true;
        ShowCurrent(focus: false);
    }

    /// <summary>Copia e confirma no status (que o leitor de tela anuncia), em vez de não dar retorno nenhum.</summary>
    private void Copy(string? text, string confirmation)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        AnalysisStatus = _picker.CopyToClipboard(text)
            ? confirmation
            : "A área de transferência está em uso por outro programa. Tente copiar de novo.";
    }

    [RelayCommand]
    private void ToggleShowAllFiles() => ShowAllFiles = !ShowAllFiles;

    /// <summary>
    /// Esquece as análises guardadas. Só para o seletor de dados de desenvolvimento: trocar a fonte de dados não pode
    /// deixar uma análise de teste sobre o C: real (no uso normal, "Ler novamente" mantém as análises).
    /// </summary>
    public void ResetAnalyses()
    {
        _analyses.Clear();
        _currentKey = null;
        HasAnalysis = false;
        AnalysisChips = [];
        AnalysisStatus = "Escolha um volume e clique em Analisar para ver as maiores pastas e arquivos. Em discos grandes, a leitura pode levar alguns minutos.";
    }

    private AnalysisState? Current => _currentKey is not null && _analyses.TryGetValue(_currentKey, out var state) ? state : null;

    private async Task AnalyzeTargetAsync(string path, string label, bool solidState, long? volumeUsed)
    {
        var cts = new CancellationTokenSource();
        _analysisCts = cts;
        IsAnalyzing = true;
        TechnicalError = null;

        // A análise anterior continua na tela enquanto a nova lê o disco; cancelar a nova não perde nada.
        AnalysisStatus = $"Lendo {label}…";
        AnalysisPercent = 0;
        HasDeterminateProgress = volumeUsed is > 0;
        var progress = new Progress<SpaceProgress>(p =>
        {
            // Aproximado: a soma lida pode passar do usado (links físicos), então para em 99% até terminar.
            var percent = volumeUsed is > 0 ? Math.Min(99, p.BytesScanned * 100.0 / volumeUsed.Value) : 0;
            AnalysisPercent = percent;
            AnalysisStatus = HasDeterminateProgress
                ? $"Lendo {label} — cerca de {percent:0}% · {Fmt.Bytes(p.BytesScanned)} de {Fmt.Bytes(volumeUsed)} · {ShortPath(p.CurrentFolder)}"
                : $"Lendo {label} — {Fmt.Count(p.FilesScanned, "arquivo", "arquivos")} · {Fmt.Bytes(p.BytesScanned)} · {ShortPath(p.CurrentFolder)}";
        });
        try
        {
            var analysis = await _analyzer.AnalyzeAsync(path, solidState, progress, cts.Token);
            var state = new AnalysisState(path, label, analysis, volumeUsed);
            _analyses[path] = state;
            _currentKey = path;
            FileFilter = string.Empty;
            HasAnalysis = true;
            ShowCurrent(focus: true);
            UpdateChips();
            AnalysisStatus = StatusFor(state);
        }
        catch (OperationCanceledException)
        {
            AnalysisStatus = $"Análise de {label} cancelada. Nada foi alterado.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na análise de espaço de {Root}", path);
            TechnicalError = ex.Message;
            AnalysisStatus = ex switch
            {
                UnauthorizedAccessException => $"Sem permissão para ler {label}. Pastas do sistema e de outros usuários exigem administrador.",
                DirectoryNotFoundException or DriveNotFoundException => $"{label} não existe mais (volume desconectado ou pasta removida).",
                IOException => $"{label} não está acessível agora (desconectado ou ocupado). Tente de novo em instantes.",
                _ => $"Não foi possível analisar {label}. O detalhe técnico ficou no log e aparece no modo avançado.",
            };
        }
        finally
        {
            if (ReferenceEquals(_analysisCts, cts))
            {
                _analysisCts = null;
            }

            cts.Dispose();
            HasDeterminateProgress = false;
            IsAnalyzing = false;
        }
    }

    private static string StatusFor(AnalysisState state) =>
        $"Análise de {state.Label} · {Fmt.Duration(state.Analysis.Duration)} de leitura · {Fmt.Count(state.Analysis.Root.FileCount, "arquivo", "arquivos")} · {Fmt.DateTime(state.Analysis.CapturedAt)}";

    private void UpdateChips() =>
        AnalysisChips = _analyses.Count < 2
            ? []
            : _analyses.Values.Select(a => new AnalysisChip(a.Key, a.Label, a.Key == _currentKey)).ToList();

    private async Task LoadAsync(bool forceRefresh)
    {
        // Só a leitura mais recente é aplicada: uma leitura anterior e mais lenta não pode sobrescrever a nova.
        var version = ++_loadVersion;
        IsLoading = true;
        Error = null;
        TechnicalError = null;
        var measure = MeasureLocationsAsync(version);
        try
        {
            var inventory = await _inventory.GetAsync(CancellationToken.None, forceRefresh);
            if (version == _loadVersion)
            {
                Apply(inventory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao ler o inventário de armazenamento");
            Error = "Não foi possível ler discos e volumes. Use “Ler novamente”; se persistir, o detalhe técnico ficou no log e aparece no modo avançado.";
            TechnicalError = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }

        await measure;
    }

    private async Task MeasureLocationsAsync(int version)
    {
        IsMeasuring = true;
        LocationsTitle = "Medindo temporários e caches…";
        LocationsCaption = string.Empty;
        try
        {
            var measures = await _analyzer.MeasureLocationsAsync(StorageLocations.All, CancellationToken.None);
            if (version == _loadVersion)
            {
                ApplyLocations(measures);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao medir temporários e caches");
            LocationsTitle = "Não foi possível medir temporários e caches";
            LocationsCaption = "Use “Ler novamente”; o detalhe técnico ficou no log.";
        }
        finally
        {
            IsMeasuring = false;
        }
    }

    private double RuleParameter(string key) => _lowSpace.ParameterOf(_ruleSettings, key);

    private void Apply(StorageInventory inventory)
    {
        CapturedText = $"Lido em {Fmt.DateTime(inventory.CapturedAt)}.";
        Warnings = inventory.Warnings;

        var failingDisks = inventory.Disks
            .Where(d => d.PredictedFailure || d.Health is DiskHealth.Unhealthy or DiskHealth.Warning)
            .ToDictionary(d => d.Number);

        Volumes = inventory.Volumes.Select(v =>
        {
            var disk = inventory.Disks.FirstOrDefault(d => d.Number == v.DiskNumber);
            var drive = v.Root.TrimEnd('\\');
            var title = v.Label is { } label ? $"{drive}  {label}" : drive;
            var severity = v.IsRemovable || !_ruleSettings.IsEnabled(_lowSpace.Id)
                ? null
                : LowFreeSpaceRule.Assess(v.IsSystem, v.SizeBytes, v.FreeBytes, RuleParameter);
            var tone = severity switch
            {
                Severity.High or Severity.Critical => StatusTone.Critical,
                not null => StatusTone.Warning,
                _ => StatusTone.Normal,
            };

            // Diagnóstico primeiro e em palavras (não só cor); tipo antes do nome do disco (o corte leva o fim do nome).
            var caption = string.Join(" · ", new[]
            {
                v.DiskNumber is { } n && failingDisks.TryGetValue(n, out var failing)
                    ? failing.PredictedFailure || failing.Health == DiskHealth.Unhealthy ? "disco com falha prevista" : "disco em atenção"
                    : null,
                tone switch { StatusTone.Critical => "espaço criticamente baixo", StatusTone.Warning => "pouco espaço livre", _ => null },
                v.IsSystem ? "Windows" : null,
                v.FileSystem,
                disk is null ? null : StorageText.Kind(disk.Media, disk.Bus),
                disk is null ? null : InventoryText.Clean(disk.Name),
                v.IsRemovable ? "removível" : null,
            }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var (free, size) = Shared.Formatting.ByteSize.FormatPair(v.FreeBytes, v.SizeBytes, CultureInfo.CurrentCulture);
            return new VolumeRow(v.Root, title, caption, $"{free} livres de {size}", v.SizeBytes > 0 ? v.UsedBytes * 100.0 / v.SizeBytes : 0, disk?.IsSolidState ?? false, v.UsedBytes, tone);
        }).ToList();
        CompactVolumes = Volumes.Count > MaxFullVolumeRows;

        Alerts = BuildAlerts(inventory, failingDisks);
        var critical = Alerts.Count(a => a.IsCritical);
        var attention = Alerts.Count - critical;
        AlertSummary = Alerts.Count == 0 ? string.Empty : string.Join(" e ", new[]
        {
            critical > 0 ? Fmt.Count(critical, "problema crítico", "problemas críticos") : null,
            attention > 0 ? Fmt.Count(attention, "ponto de atenção", "pontos de atenção") : null,
        }.Where(s => s is not null)) + " no armazenamento.";
        AllClearText = Alerts.Count == 0 && inventory.Disks.Count > 0
            ? "Nenhum disco sinaliza problema e nenhum volume está com pouco espaço."
            : string.Empty;

        DiscardAnalysesOfMissingVolumes();

        var cards = inventory.Disks.Select(d => DiskCard(d, inventory)).ToList();
        var compact = cards.Count > MaxDiskCards;
        Disks = compact ? [] : cards;
        DiskRows = compact ? inventory.Disks.Zip(cards, DiskSummary).ToList() : [];

        // Leitura vazia vai para o topo (é o diagnóstico da tela); disco ausente com volumes presentes fica na seção de discos.
        InventoryEmptyText = inventory.Disks.Count == 0 && inventory.Volumes.Count == 0
            ? "O Windows não devolveu discos nem volumes. Isso costuma ser temporário (serviço de armazenamento ocupado); use “Ler novamente” e, se persistir, veja o modo avançado nas Configurações."
            : string.Empty;
        DisksEmptyText = inventory.Disks.Count == 0 && inventory.Volumes.Count > 0
            ? "O Windows não devolveu nenhum disco físico. Os volumes acima continuam corretos; use “Ler novamente” e, se persistir, veja o modo avançado nas Configurações."
            : string.Empty;
        DisksNote = inventory.ReadWithElevation
            ? "Temperatura e vida útil aparecem só quando o próprio disco informa. Nada é estimado."
            : "Vida útil restante e horas ligadas exigem administrador: sem ele, o Windows nega o acesso a esses dados. Temperatura aparece só quando o próprio disco informa. Nada é estimado.";
        ShowAnalysisSection = Volumes.Count > 0 || HasAnalysis;
    }

    private List<StorageAlert> BuildAlerts(StorageInventory inventory, Dictionary<int, PhysicalDiskInfo> failingDisks)
    {
        var alerts = new List<StorageAlert>();
        foreach (var disk in failingDisks.Values.OrderByDescending(d => d.PredictedFailure || d.Health == DiskHealth.Unhealthy))
        {
            var critical = disk.PredictedFailure || disk.Health == DiskHealth.Unhealthy;
            var name = InventoryText.Clean(disk.Name) ?? disk.Name;
            var volumes = inventory.Volumes.Where(v => v.DiskNumber == disk.Number).Select(v => v.Root.TrimEnd('\\')).ToList();
            var health = StorageText.HealthText(disk.Health, disk.PredictedFailure);
            alerts.Add(new StorageAlert(
                critical ? $"O disco {name} indica falha" : $"O disco {name} indica atenção",
                critical
                    ? "O próprio disco informou ao Windows uma condição de falha (SMART). Discos nesse estado podem parar a qualquer momento."
                    : "O Windows marcou o disco com estado de atenção a partir dos dados de saúde que o dispositivo informa.",
                string.Join(" · ", new[]
                {
                    health is null ? null : $"Estado SMART: {health.Value.Text}",
                    disk.OperationalStatus.Count > 0 ? $"Estado operacional: {string.Join(", ", disk.OperationalStatus)}" : null,
                }.Where(s => s is not null)),
                volumes.Count > 0
                    ? $"Faça backup agora de {string.Join(", ", volumes)}. Depois, confirme com a ferramenta do fabricante e planeje a substituição."
                    : "Faça backup dos dados desse disco agora. Depois, confirme com a ferramenta do fabricante e planeje a substituição.",
                critical ? StatusTone.Critical : StatusTone.Warning,
                null));
        }

        foreach (var volume in Volumes.Where(v => v.Tone != StatusTone.Normal))
        {
            var info = inventory.Volumes.First(v => v.Root == volume.Root);
            var drive = volume.Root.TrimEnd('\\');
            var free = Fmt.Bytes(info.FreeBytes);
            alerts.Add(new StorageAlert(
                info.IsSystem ? $"Pouco espaço no disco do Windows ({drive})" : $"Pouco espaço livre em {drive}",
                info.IsSystem
                    ? $"{drive} tem {free} livres. Com menos de {RuleParameter("SystemMinimumFreeGb"):0} GB no volume do Windows, atualizações falham e o arquivo de paginação não consegue crescer."
                    : info.FreeBytes <= 0
                        ? $"{drive} está sem espaço livre. Cópias, downloads e gravações nesse volume falham."
                        : $"{drive} tem {free} livres ({info.FreePercent.ToString("0.#", CultureInfo.CurrentCulture)}% do volume). Cópias e gravações grandes podem falhar.",
                null,
                $"Analise {drive} para ver o que ocupa espaço. Nada é apagado automaticamente.",
                volume.Tone,
                volume));
        }

        return alerts;
    }

    private void DiscardAnalysesOfMissingVolumes()
    {
        var missing = _analyses.Keys
            .Where(key => !Volumes.Any(v => string.Equals(v.Root, Path.GetPathRoot(key), StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var key in missing)
        {
            _analyses.Remove(key);
        }

        if (_currentKey is not null && missing.Contains(_currentKey))
        {
            var discarded = _currentKey;
            _currentKey = _analyses.Keys.FirstOrDefault();
            AnalysisStatus = $"A análise de {discarded.TrimEnd('\\')} foi descartada: o volume não está mais disponível.";
            HasAnalysis = _currentKey is not null;
            if (HasAnalysis)
            {
                ShowCurrent(focus: false);
            }
        }

        UpdateChips();
    }

    private static InfoCard DiskCard(PhysicalDiskInfo disk, StorageInventory inventory)
    {
        var volumes = inventory.Volumes.Where(v => v.DiskNumber == disk.Number).ToList();
        var title = Fmt.Text(InventoryText.Clean(disk.Name) ?? disk.Name);

        // Leitor de cartão sem cartão: nada a medir; oito linhas de "não disponível" seriam só ruído.
        if (disk.SizeBytes is null && disk.Partitions.Count == 0 && volumes.Count == 0)
        {
            return new InfoCard(title, $"{StorageText.Kind(disk.Media, disk.Bus)} · sem mídia inserida", []);
        }

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

        var space = volumes.Count == 0
            ? new InfoRow[] { new("Volumes", "nenhum com letra de unidade") }
            : [new("Utilizado", Fmt.Bytes(used)), new("Livre", Fmt.Bytes(free))];

        return new InfoCard(title, caption,
        [
            .. space,
            new("Saúde (vida útil)", disk.WearPercent is { } wear ? $"{100 - wear}% restante" : Fmt.NotAvailable),
            new("Estado SMART", health?.Text ?? Fmt.NotAvailable, health?.Tone ?? StatusTone.Normal),
            new("Temperatura", disk.TemperatureCelsius is { } celsius ? string.Create(CultureInfo.CurrentCulture, $"{celsius:0} °C") : Fmt.NotAvailable),
            new("TRIM", trim.Item1, trim.Item2),
            new("Partições", Partitions(disk)),
            new("Firmware", Fmt.Text(disk.Firmware)),
        ]);
    }

    private static DiskSummaryRow DiskSummary(PhysicalDiskInfo disk, InfoCard card)
    {
        var health = StorageText.HealthText(disk.Health, disk.PredictedFailure);
        var summary = string.Join(" · ", new[]
        {
            StorageText.Kind(disk.Media, disk.Bus),
            disk.SizeBytes is { } size ? Fmt.Bytes(size) : "sem mídia",
            health?.Text,
        }.Where(s => s is not null));
        return new DiskSummaryRow(card.Title, summary, health?.Tone ?? StatusTone.Normal, card);
    }

    /// <summary>
    /// Caminho encurtado no meio ("C:\Users\…\models\blobs"): as pontas identificam o lugar; cortar no fim perderia a
    /// pasta final, que é o que diferencia dois caminhos. O caminho completo fica na dica e no nome acessível.
    /// </summary>
    public static string ShortPath(string path, int maxLength = 64)
    {
        if (path.Length <= maxLength)
        {
            return path;
        }

        var parts = path.Split(System.IO.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 3)
        {
            return path;
        }

        var head = string.Join(System.IO.Path.DirectorySeparatorChar, parts.Take(2));
        var tail = parts[^1];
        for (var i = parts.Length - 2; i >= 2; i--)
        {
            var longer = parts[i] + System.IO.Path.DirectorySeparatorChar + tail;
            if (head.Length + longer.Length + 3 > maxLength)
            {
                break;
            }

            tail = longer;
        }

        return $"{head}{System.IO.Path.DirectorySeparatorChar}…{System.IO.Path.DirectorySeparatorChar}{tail}";
    }

    /// <summary>Tamanho para totais e resumos: abaixo de 1 MB vira "menos de 1 MB" ("1 B" e "512 B" eram ruído).</summary>
    public static string ApproxBytes(long bytes) => bytes is > 0 and < Mb ? "menos de 1 MB" : Fmt.Bytes(bytes);

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
        var developer = counted.Where(m => m.Location.Kind == StorageLocationKind.DeveloperCache).Sum(m => m.SizeBytes);
        var recycle = measures.FirstOrDefault(m => m.Location.Kind == StorageLocationKind.RecycleBin);
        var missing = measures.Where(m => !m.Found).Select(m => m.Location.Title).ToList();
        LocationsTitle = missing.Count == measures.Count
            ? "Nenhum local conhecido de temporários ou caches encontrado"
            : $"{ApproxBytes(total)} em temporários e caches";
        LocationsCaption = string.Join(" ", new[]
        {
            "Só medição: nada é apagado aqui.",
            developer > 0 ? $"Inclui {ApproxBytes(developer)} de caches de ferramentas de desenvolvimento, cada um com o comando oficial de limpeza." : null,
            recycle is { Found: true } ? $"A Lixeira ({ApproxBytes(recycle.SizeBytes)}) fica fora da soma." : null,
            missing.Count > 0 && missing.Count < measures.Count ? $"Não encontrados nesta máquina: {string.Join(", ", missing)}." : null,
        }.Where(s => s is not null));

        // Só o que existe vira linha; os ausentes ficam numa frase (oito "não encontrado" seguidos eram ruído).
        Locations = measures
            .Where(m => m.Found)
            .OrderByDescending(m => m.SizeBytes)
            .Select(m => new LocationRow(
                m.Location.Title,
                m.SizeBytes == 0 && m.InaccessibleCount > 0 ? "sem permissão" : ApproxBytes(m.SizeBytes),
                m.Location.Description + (m.InaccessibleCount > 0 ? " Parte das pastas exige administrador para ser lida; o total pode ser maior." : string.Empty),
                m.ResolvedPaths.Count > 0 ? string.Join(Environment.NewLine, m.ResolvedPaths) : null,
                m.ResolvedPaths.Count > 0 ? m.ResolvedPaths[0] : null,
                m.Location.CleanupCommand))
            .ToList();
    }

    private void ShowCurrent(bool focus)
    {
        if (Current is not { } state)
        {
            return;
        }

        var node = state.Path[^1];
        Breadcrumb = state.Path.Select((n, i) => new BreadcrumbItem(i == 0 ? n.Path.TrimEnd('\\') : n.Name, n, i == state.Path.Count - 1)).ToList();
        // Na raiz de um volume o número principal é o USADO medido pelo Windows: a soma das pastas pode passar dele
        // (links físicos contam em cada pasta) e um título maior que o disco contradiria "medir antes de afirmar".
        var inflated = state.Path.Count == 1 && state.VolumeUsed is { } usedOnVolume && node.SizeBytes > usedOnVolume;
        CurrentTitle = inflated
            ? $"{Fmt.Bytes(state.VolumeUsed)} usados em {state.Label}"
            : $"{Fmt.Bytes(node.SizeBytes)} em {(state.Path.Count == 1 ? state.Label : node.Name)}";

        var notes = new List<string> { Fmt.Count(node.FileCount, "arquivo", "arquivos") };
        if (node.InaccessibleCount > 0)
        {
            notes.Add($"{Fmt.Count(node.InaccessibleCount, "pasta sem permissão de leitura", "pastas sem permissão de leitura")}: o total real pode ser maior (pastas do sistema e de outros usuários exigem administrador)");
        }

        if (node.CloudOnlyBytes > 0)
        {
            notes.Add($"{Fmt.Bytes(node.CloudOnlyBytes)} só na nuvem (OneDrive), sem ocupar o disco");
        }

        // Arquivos do Windows aparecem em mais de uma pasta (links físicos): a soma pode passar do espaço realmente usado.
        if (inflated)
        {
            notes.Add($"As pastas somam {Fmt.Bytes(node.SizeBytes)}, mais que o usado: arquivos do Windows que aparecem em mais de uma pasta (links físicos, como em Windows\\WinSxS) contam em cada uma. As porcentagens são sobre essa soma");
        }
        else if (node.Path.Contains(@"\Windows", StringComparison.OrdinalIgnoreCase))
        {
            notes.Add("Pastas do Windows aparecem maiores do que ocupam: arquivos compartilhados entre pastas (links físicos) contam em cada uma");
        }

        CurrentCaption = string.Join(Environment.NewLine, notes);

        double Share(long bytes) => node.SizeBytes > 0 ? bytes * 100.0 / node.SizeBytes : 0;
        var children = SortByName
            ? node.Children.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            : node.Children.OrderByDescending(c => c.SizeBytes);
        var rows = children.Select(c => new FolderRow(
            c.Name,
            Fmt.Bytes(c.SizeBytes),
            c.Inaccessible ? "sem permissão de leitura" : string.Create(CultureInfo.CurrentCulture, $"{Share(c.SizeBytes):0}% · {Fmt.Count(c.FileCount, "arquivo", "arquivos")}"),
            Share(c.SizeBytes),
            c,
            c.Path,
            Hint: StorageHints.ForFolder(c.Name)?.Text,
            Command: StorageHints.ForFolder(c.Name)?.Command)).ToList();

        // Lista longa (caso dominante pode ter 500): mostra as primeiras e oferece o resto.
        var hiddenByLimit = ShowAllFolders ? 0 : Math.Max(0, rows.Count - VisibleFolders);
        if (hiddenByLimit > 0)
        {
            rows = rows.Take(VisibleFolders).ToList();
        }

        MoreFoldersText = hiddenByLimit > 0 ? $"Mostrar mais {Fmt.Count(hiddenByLimit, "pasta", "pastas")}" : string.Empty;

        if (node.FilesBytes > 0)
        {
            rows.Add(new FolderRow("Arquivos nesta pasta", ApproxBytes(node.FilesBytes), string.Create(CultureInfo.CurrentCulture, $"{Share(node.FilesBytes):0}%"), Share(node.FilesBytes), null, node.Path, "\uE8A5"));
        }

        if (node.HiddenChildrenCount > 0)
        {
            // Diz por que não estão listadas (antes: "somadas para a lista não ficar longa"): ou estão abaixo do mínimo
            // da lista ou além do limite de linhas — nesse caso, menores que a última listada.
            var listed = node.Children.Where(c => !c.Inaccessible).ToList();
            var hiddenWhy = listed.Count < _options.MaxChildrenPerFolder || listed.Count == 0
                ? $"abaixo de {Fmt.Bytes(_options.MinFolderBytes)}"
                : $"menores que {Fmt.Bytes(listed.Min(c => c.SizeBytes))}";
            rows.Add(new FolderRow(
                Fmt.Count(node.HiddenChildrenCount, "pasta menor", "pastas menores"),
                ApproxBytes(node.HiddenChildrenBytes),
                node.HiddenChildrenCount == 1 ? $"somada aqui: {hiddenWhy}" : $"somadas aqui: cada uma {hiddenWhy}",
                Share(node.HiddenChildrenBytes),
                null,
                node.Path));
        }

        Folders = rows;
        ApplyFiles();
        if (focus)
        {
            FolderListChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Listas de arquivos da pasta atual (não do volume inteiro), com filtro por nome/extensão.</summary>
    private void ApplyFiles()
    {
        if (Current is not { } state)
        {
            return;
        }

        var node = state.Path[^1];
        var inRoot = state.Path.Count == 1;
        var prefix = node.Path.EndsWith('\\') ? node.Path : node.Path + "\\";
        bool InScope(FileEntry f) => inRoot || f.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        bool Matches(FileEntry f) => string.IsNullOrWhiteSpace(FileFilter)
            || System.IO.Path.GetFileName(f.Path).Contains(FileFilter.Trim(), StringComparison.CurrentCultureIgnoreCase);

        var largest = state.Analysis.LargestFiles.Where(InScope).Where(Matches).ToList();
        var old = state.Analysis.OldFiles.Where(InScope).Where(Matches).ToList();
        var take = ShowAllFiles ? int.MaxValue : VisibleFiles;
        var scope = inRoot ? state.Label : node.Name;
        FilesScope = inRoot ? $"Os maiores de {scope} inteiro." : $"Os maiores dentro de {scope} (entre os {state.Analysis.LargestFiles.Count} maiores do volume).";

        LargestFiles = largest.Take(take).Select(f => ToFileRow(f, Fmt.Date(f.LastWrite))).ToList();
        OldFiles = old.Take(take).Select(f => ToFileRow(f, $"sem alteração desde {Fmt.Date(f.LastWrite)}")).ToList();

        var filtered = !string.IsNullOrWhiteSpace(FileFilter);
        LargestFilesEmptyText = LargestFiles.Count > 0 ? string.Empty
            : filtered ? $"Nenhum arquivo com “{FileFilter.Trim()}” no nome."
            : inRoot ? "Nenhum arquivo encontrado."
            : $"Nenhum dos {state.Analysis.LargestFiles.Count} maiores arquivos do volume está em {scope}.";
        OldFilesCaption = $"Acima de {Fmt.Bytes(_options.OldFileMinimumBytes)} e sem alteração há mais de {Fmt.Count(Math.Round(_options.OldFileAge.TotalDays / 365), "ano", "anos")}. Antigo não quer dizer descartável: confira de quem é antes de apagar.";
        OldFilesEmptyText = OldFiles.Count > 0 ? string.Empty
            : filtered ? $"Nenhum arquivo com “{FileFilter.Trim()}” no nome."
            : "Nenhum arquivo nessa condição aqui.";

        var hidden = Math.Max(largest.Count, old.Count) - VisibleFiles;
        CanShowAllFiles = hidden > 0 || ShowAllFiles;
        ShowAllFilesLabel = ShowAllFiles ? $"Mostrar só os {VisibleFiles} maiores" : $"Mostrar todos ({Math.Max(largest.Count, old.Count)})";
    }

    private static FileRow ToFileRow(FileEntry file, string detail)
    {
        var hint = StorageHints.ForFile(file.Path);
        return new FileRow(
            System.IO.Path.GetFileName(file.Path),
            ShortPath(System.IO.Path.GetDirectoryName(file.Path) ?? string.Empty),
            Fmt.Bytes(file.SizeBytes),
            detail,
            file.Path,
            hint?.Text,
            hint?.DoNotDelete == true,
            hint?.Command);
    }

    /// <summary>Uma análise guardada: o volume ou pasta, o resultado e o caminho de navegação atual dentro dela.</summary>
    private sealed class AnalysisState(string key, string label, SpaceAnalysis analysis, long? volumeUsed)
    {
        public string Key { get; } = key;

        public string Label { get; } = label;

        public SpaceAnalysis Analysis { get; } = analysis;

        /// <summary>Espaço usado no volume (só para análise do volume inteiro: explica a soma acima do usado).</summary>
        public long? VolumeUsed { get; } = volumeUsed;

        public List<FolderNode> Path { get; } = [analysis.Root];
    }
}
