using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Services;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public sealed class ServiceRowViewModel
{
    public ServiceRowViewModel(ServiceItem item)
    {
        Item = item;
    }

    public ServiceItem Item { get; }

    public string ServiceName => Item.ServiceName;

    public string DisplayName => Item.DisplayName;

    public string Description => !string.IsNullOrWhiteSpace(Item.Description) ? Item.Description : "Nenhuma descrição fornecida pelo serviço.";

    public WindowsServiceState State => Item.State;

    public string StateText => Item.State switch
    {
        WindowsServiceState.Running => "Em Execução",
        WindowsServiceState.Stopped => "Parado",
        WindowsServiceState.Paused => "Pausado",
        WindowsServiceState.StartPending => "Iniciando",
        WindowsServiceState.StopPending => "Parando",
        _ => "Desconhecido",
    };

    public WindowsServiceStartupType StartupType => Item.StartupType;

    public string StartupTypeText => Item.StartupType switch
    {
        WindowsServiceStartupType.Automatic => "Automático",
        WindowsServiceStartupType.AutomaticDelayed => "Automático (Atrasado)",
        WindowsServiceStartupType.Manual => "Manual",
        WindowsServiceStartupType.Disabled => "Desativado",
        WindowsServiceStartupType.Boot => "Inicialização (Boot)",
        WindowsServiceStartupType.System => "Sistema",
        _ => "Desconhecido",
    };

    public string ExecutablePath => Item.ExecutablePath ?? Item.RawImagePath ?? string.Empty;

    public string ExecutableFileName => !string.IsNullOrWhiteSpace(Item.ExecutablePath)
        ? System.IO.Path.GetFileName(Item.ExecutablePath)
        : string.Empty;

    public bool FileExists => Item.FileExists;

    public bool IsOrphan => !Item.FileExists && Item.StartupType != WindowsServiceStartupType.Disabled;

    public bool HasProblem => IsOrphan || Item.HasRecentFailure;

    public string Publisher => !string.IsNullOrWhiteSpace(Item.Publisher) ? Item.Publisher : Item.IsWindowsService ? "Microsoft Windows" : "Desconhecido";

    public string SignedText => Item.IsSigned == true ? "Assinado" : Item.IsSigned == false ? "Não assinado" : "—";

    public string AccountText => !string.IsNullOrWhiteSpace(Item.ServiceAccount) ? Item.ServiceAccount : "LocalSystem";

    public string DependenciesText => Item.Dependencies.Count > 0 ? string.Join(", ", Item.Dependencies) : "Nenhuma";
}

public sealed partial class ServicesViewModel : ObservableObject, IPageLifecycle
{
    private readonly IServicesProvider _servicesProvider;
    private readonly ILogger<ServicesViewModel> _logger;

    public ServicesViewModel(
        IServicesProvider servicesProvider,
        UiPreferences preferences,
        ILogger<ServicesViewModel> logger)
    {
        _servicesProvider = servicesProvider;
        Preferences = preferences;
        _logger = logger;
    }

    public UiPreferences Preferences { get; }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "Todos";

    [ObservableProperty]
    private ObservableCollection<ServiceRowViewModel> _services = [];

    [ObservableProperty]
    private IReadOnlyList<ServiceRowViewModel> _filteredServices = [];

    [ObservableProperty]
    private ServiceRowViewModel? _selectedService;

    [ObservableProperty]
    private string _summaryText = string.Empty;

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
    private void OpenServicesMsc()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "services.msc",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao abrir services.msc");
        }
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var raw = await _servicesProvider.GetServicesAsync(CancellationToken.None).ConfigureAwait(true);
            var rows = raw.Select(s => new ServiceRowViewModel(s)).OrderBy(s => s.DisplayName).ToList();
            Services = new ObservableCollection<ServiceRowViewModel>(rows);

            var running = rows.Count(s => s.State == WindowsServiceState.Running);
            var orphans = rows.Count(s => s.IsOrphan);

            SummaryText = orphans > 0
                ? $"{rows.Count} serviços instalados ({running} em execução · {orphans} órfãos)"
                : $"{rows.Count} serviços instalados ({running} em execução)";

            ApplyFilter();

            if (SelectedService is null || !rows.Any(s => s.ServiceName == SelectedService.ServiceName))
            {
                SelectedService = FilteredServices.Count > 0 ? FilteredServices[0] : null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar inventário de serviços");
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

        var filtered = Services.AsEnumerable();

        if (filter == "Com Falha / Órfãos")
        {
            filtered = filtered.Where(s => s.HasProblem);
        }
        else if (filter == "Automáticos")
        {
            filtered = filtered.Where(s => s.StartupType is WindowsServiceStartupType.Automatic or WindowsServiceStartupType.AutomaticDelayed);
        }
        else if (filter == "Em Execução")
        {
            filtered = filtered.Where(s => s.State == WindowsServiceState.Running);
        }
        else if (filter == "Terceiros")
        {
            filtered = filtered.Where(s => !s.Item.IsWindowsService);
        }

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(s =>
                s.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.ServiceName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.ExecutablePath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                s.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredServices = filtered.ToList();
    }
}
