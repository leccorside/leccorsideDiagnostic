using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Explorer;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public sealed class ShellExtensionRowViewModel
{
    public ShellExtensionRowViewModel(ShellExtensionItem item)
    {
        Item = item;
    }

    public ShellExtensionItem Item { get; }

    public string Name => Item.Name;

    public string TypeText => Item.Type switch
    {
        ShellExtensionType.ContextMenu => "Menu de Contexto",
        ShellExtensionType.PropertySheet => "Propriedades",
        ShellExtensionType.IconOverlay => "Sobreposição de Ícone",
        ShellExtensionType.ThumbnailHandler => "Miniatura",
        ShellExtensionType.PreviewHandler => "Visualização",
        ShellExtensionType.DragAndDrop => "Arrastar e Soltar",
        _ => "Outro",
    };

    public string DllPath => Item.DllPath;

    public string DllFileName => System.IO.Path.GetFileName(Item.DllPath);

    public bool FileExists => Item.FileExists;

    public string StatusText => !Item.FileExists ? "DLL Inexistente (Órfã)" : Item.IsBlocked ? "Bloqueada" : "Ativa";

    public string Publisher => !string.IsNullOrWhiteSpace(Item.Publisher) ? Item.Publisher : Item.IsWindowsOwned ? "Microsoft Corporation" : "Desconhecido";

    public string SignedText => Item.IsSigned == true ? "Assinado" : Item.IsSigned == false ? "Não assinado" : "—";

    public bool IsOrphan => !Item.FileExists;

    public bool IsProblem => !Item.FileExists || (Item.IsSigned == false && !Item.IsWindowsOwned);
}

public sealed partial class ExplorerViewModel : ObservableObject, IPageLifecycle
{
    private readonly IExplorerProvider _explorerProvider;
    private readonly IFolderBenchmarkService _benchmarkService;
    private readonly IFolderPicker _folderPicker;
    private readonly ILogger<ExplorerViewModel> _logger;

    public ExplorerViewModel(
        IExplorerProvider explorerProvider,
        IFolderBenchmarkService benchmarkService,
        IFolderPicker folderPicker,
        UiPreferences preferences,
        ILogger<ExplorerViewModel> logger)
    {
        _explorerProvider = explorerProvider;
        _benchmarkService = benchmarkService;
        _folderPicker = folderPicker;
        Preferences = preferences;
        _logger = logger;

        SelectedFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is { } user
            ? System.IO.Path.Combine(user, "Downloads")
            : string.Empty;
    }

    public UiPreferences Preferences { get; }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "Todas";

    [ObservableProperty]
    private ObservableCollection<ShellExtensionRowViewModel> _extensions = [];

    [ObservableProperty]
    private IReadOnlyList<ShellExtensionRowViewModel> _filteredExtensions = [];

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _cacheStatusText = "Calculando tamanho dos caches do Explorer...";

    // Detector de pastas lentas
    [ObservableProperty]
    private string _selectedFolderPath = string.Empty;

    [ObservableProperty]
    private bool _isBenchmarking;

    [ObservableProperty]
    private FolderBenchmarkResult? _benchmarkResult;

    public void Activate()
    {
        _ = LoadAsync();
    }

    public void Deactivate()
    {
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private void SelectFolder()
    {
        var selected = _folderPicker.PickFolder("Selecione a pasta para análise de velocidade");
        if (!string.IsNullOrWhiteSpace(selected))
        {
            SelectedFolderPath = selected;
        }
    }

    [RelayCommand]
    private void QuickSelect(string folderKind)
    {
        SelectedFolderPath = folderKind switch
        {
            "Downloads" => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            "Documentos" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Imagens" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Desktop" => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            _ => SelectedFolderPath,
        };
    }

    [RelayCommand]
    private async Task RunBenchmarkAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolderPath) || !System.IO.Directory.Exists(SelectedFolderPath))
        {
            return;
        }

        IsBenchmarking = true;
        BenchmarkResult = null;

        try
        {
            BenchmarkResult = await _benchmarkService.BenchmarkFolderAsync(SelectedFolderPath, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no benchmark da pasta {Path}", SelectedFolderPath);
        }
        finally
        {
            IsBenchmarking = false;
        }
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var raw = await _explorerProvider.GetShellExtensionsAsync(CancellationToken.None).ConfigureAwait(true);
            var cache = await _explorerProvider.GetCacheStatusAsync(CancellationToken.None).ConfigureAwait(true);

            var rows = raw.Select(r => new ShellExtensionRowViewModel(r)).OrderBy(r => r.Name).ToList();
            Extensions = new ObservableCollection<ShellExtensionRowViewModel>(rows);

            var totalMb = (cache.IconCacheSizeBytes + cache.ThumbnailCacheSizeBytes) / (1024.0 * 1024.0);
            var iconMb = cache.IconCacheSizeBytes / (1024.0 * 1024.0);
            var thumbMb = cache.ThumbnailCacheSizeBytes / (1024.0 * 1024.0);

            CacheStatusText = string.Create(CultureInfo.CurrentCulture,
                $"Caches do Explorer: {totalMb:F1} MB total ({iconMb:F1} MB de ícones · {thumbMb:F1} MB de miniaturas · {cache.CacheFilesCount} arquivos de banco de dados)");

            var orphanCount = rows.Count(r => r.IsOrphan);
            SummaryText = orphanCount > 0
                ? $"{rows.Count} extensões instaladas ({orphanCount} com DLL inexistente)"
                : $"{rows.Count} extensões de Shell instaladas";

            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar extensões do Explorer");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var filter = SelectedFilter;

        var filtered = Extensions.AsEnumerable();

        if (filter == "Problemas")
        {
            filtered = filtered.Where(e => e.IsProblem);
        }
        else if (filter == "Menu de Contexto")
        {
            filtered = filtered.Where(e => e.Item.Type == ShellExtensionType.ContextMenu);
        }
        else if (filter == "Miniaturas e Visualização")
        {
            filtered = filtered.Where(e => e.Item.Type is ShellExtensionType.ThumbnailHandler or ShellExtensionType.PreviewHandler);
        }
        else if (filter == "Ícones")
        {
            filtered = filtered.Where(e => e.Item.Type == ShellExtensionType.IconOverlay);
        }

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(e =>
                e.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.DllPath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredExtensions = filtered.ToList();
    }
}
