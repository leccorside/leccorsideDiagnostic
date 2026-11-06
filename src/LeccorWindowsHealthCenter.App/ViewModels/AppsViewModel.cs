using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Apps;
using LeccorWindowsHealthCenter.Shared;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public sealed class AppRowViewModel
{
    public AppRowViewModel(InstalledAppItem item)
    {
        Item = item;
    }

    public InstalledAppItem Item { get; }

    public string DisplayName => Item.DisplayName;

    public string Version => !string.IsNullOrWhiteSpace(Item.DisplayVersion) ? Item.DisplayVersion : "—";

    public string Publisher => !string.IsNullOrWhiteSpace(Item.Publisher) ? Item.Publisher : "Não informado";

    public string InstallDateText => Item.InstallDate.HasValue
        ? Item.InstallDate.Value.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)
        : "—";

    public string SizeText => Fmt.Bytes(Item.EstimatedSizeBytes);

    public AppSourceType SourceType => Item.SourceType;

    public string SourceText => Item.SourceType switch
    {
        AppSourceType.Win32 => "Win32 (Desktop)",
        AppSourceType.WindowsStore => "Microsoft Store",
        AppSourceType.SystemComponent => "Sistema",
        _ => "Outro",
    };

    public bool IsOrphaned => Item.IsOrphaned;

    public bool HasDuplicateVersion => Item.HasDuplicateVersion;

    public bool HasProblem => Item.IsOrphaned || Item.HasDuplicateVersion;

    public string StatusText
    {
        get
        {
            if (Item.IsOrphaned)
            {
                return "Desinstalador Órfão";
            }

            if (Item.HasDuplicateVersion)
            {
                return "Versão Duplicada";
            }

            return "Instalado";
        }
    }

    public string UninstallCommand => Item.UninstallString ?? Item.QuietUninstallString ?? "Nenhum comando de desinstalação registrado.";

    public string InstallLocation => !string.IsNullOrWhiteSpace(Item.InstallLocation) ? Item.InstallLocation : "Não informada";
}

public sealed partial class AppsViewModel : ObservableObject, IPageLifecycle
{
    private readonly IAppsProvider _appsProvider;
    private readonly ILogger<AppsViewModel> _logger;
    private List<AppRowViewModel> _allApps = [];

    public AppsViewModel(IAppsProvider appsProvider, ILogger<AppsViewModel> logger)
    {
        _appsProvider = appsProvider;
        _logger = logger;
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "Todos";

    [ObservableProperty]
    private AppRowViewModel? _selectedApp;

    [ObservableProperty]
    private int _totalAppsCount;

    [ObservableProperty]
    private int _win32AppsCount;

    [ObservableProperty]
    private int _storeAppsCount;

    [ObservableProperty]
    private int _orphanedCount;

    [ObservableProperty]
    private int _duplicatesCount;

    public ObservableCollection<AppRowViewModel> FilteredApps { get; } = [];

    public IReadOnlyList<string> FilterOptions { get; } = ["Todos", "Win32", "Microsoft Store", "Com Problema", "Órfãos", "Duplicados"];

    public void Activate()
    {
        if (_allApps.Count == 0)
        {
            _ = RefreshCommand.ExecuteAsync(null);
        }
    }

    public void Deactivate()
    {
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsLoading = true;
            var apps = await _appsProvider.GetInstalledAppsAsync(CancellationToken.None);
            _allApps = apps.Select(a => new AppRowViewModel(a)).ToList();

            TotalAppsCount = _allApps.Count;
            Win32AppsCount = _allApps.Count(a => a.SourceType == AppSourceType.Win32);
            StoreAppsCount = _allApps.Count(a => a.SourceType == AppSourceType.WindowsStore);
            OrphanedCount = _allApps.Count(a => a.IsOrphaned);
            DuplicatesCount = _allApps.Count(a => a.HasDuplicateVersion);

            ApplyFilters();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar lista de aplicativos");
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();

    partial void OnSelectedFilterChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        var filtered = _allApps.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.Trim();
            filtered = filtered.Where(a =>
                a.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Publisher.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        filtered = SelectedFilter switch
        {
            "Win32" => filtered.Where(a => a.SourceType == AppSourceType.Win32),
            "Microsoft Store" => filtered.Where(a => a.SourceType == AppSourceType.WindowsStore),
            "Com Problema" => filtered.Where(a => a.HasProblem),
            "Órfãos" => filtered.Where(a => a.IsOrphaned),
            "Duplicados" => filtered.Where(a => a.HasDuplicateVersion),
            _ => filtered,
        };

        FilteredApps.Clear();
        foreach (var app in filtered)
        {
            FilteredApps.Add(app);
        }

        if (SelectedApp == null || !FilteredApps.Contains(SelectedApp))
        {
            SelectedApp = FilteredApps.Count > 0 ? FilteredApps[0] : null;
        }
    }

    [RelayCommand]
    private void CopyUninstallString()
    {
        if (SelectedApp != null && !string.IsNullOrWhiteSpace(SelectedApp.Item.UninstallString))
        {
            Clipboard.SetText(SelectedApp.Item.UninstallString);
        }
    }

    [RelayCommand]
    private void OpenInstallLocation()
    {
        if (SelectedApp != null && !string.IsNullOrWhiteSpace(SelectedApp.Item.InstallLocation) && Directory.Exists(SelectedApp.Item.InstallLocation))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = SelectedApp.Item.InstallLocation,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não foi possível abrir a pasta de instalação");
            }
        }
    }
}
