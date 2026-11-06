using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Drivers;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public sealed class DriverDeviceRowViewModel
{
    public DriverDeviceRowViewModel(DriverDeviceItem item)
    {
        Item = item;
    }

    public DriverDeviceItem Item { get; }

    public string DeviceId => Item.DeviceId;

    public string Name => Item.Name;

    public string ClassName => !string.IsNullOrWhiteSpace(Item.ClassName) ? Item.ClassName : "Outro";

    public string Manufacturer => !string.IsNullOrWhiteSpace(Item.Manufacturer) ? Item.Manufacturer : "Fabricante padrão do sistema";

    public string DriverVersion => !string.IsNullOrWhiteSpace(Item.DriverVersion) ? Item.DriverVersion : "—";

    public string DriverDateText => Item.DriverDate.HasValue ? Item.DriverDate.Value.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture) : "—";

    public uint StatusCode => Item.ConfigManagerErrorCode;

    public string StatusMessage => Item.StatusMessage ?? "Funcionando normalmente.";

    public bool IsProblem => Item.IsProblemDevice;

    public bool IsMissing => Item.IsMissingDriver;
}

public sealed partial class DriversViewModel : ObservableObject, IPageLifecycle
{
    private readonly IDriversProvider _driversProvider;
    private readonly ILogger<DriversViewModel> _logger;

    public DriversViewModel(
        IDriversProvider driversProvider,
        UiPreferences preferences,
        ILogger<DriversViewModel> logger)
    {
        _driversProvider = driversProvider;
        Preferences = preferences;
        _logger = logger;
    }

    public UiPreferences Preferences { get; }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "Todas";

    [ObservableProperty]
    private ObservableCollection<DriverDeviceRowViewModel> _devices = [];

    [ObservableProperty]
    private IReadOnlyList<DriverDeviceRowViewModel> _filteredDevices = [];

    [ObservableProperty]
    private IReadOnlyList<DriverDeviceRowViewModel> _problemDevices = [];

    [ObservableProperty]
    private int _problemCount;

    [ObservableProperty]
    private int _missingCount;

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
    private void OpenDeviceManager()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "devmgmt.msc",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao abrir devmgmt.msc");
        }
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var raw = await _driversProvider.GetDevicesAsync(CancellationToken.None).ConfigureAwait(true);
            var rows = raw.Select(d => new DriverDeviceRowViewModel(d)).OrderBy(d => d.Name).ToList();

            Devices = new ObservableCollection<DriverDeviceRowViewModel>(rows);

            ProblemDevices = rows.Where(d => d.IsProblem).ToList();
            ProblemCount = rows.Count(d => d.IsProblem && !d.IsMissing);
            MissingCount = rows.Count(d => d.IsMissing);

            SummaryText = ProblemDevices.Count > 0
                ? $"{rows.Count} dispositivos identificados ({ProblemDevices.Count} com atenção ou erro)"
                : $"{rows.Count} dispositivos identificados (todos funcionando normalmente)";

            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar dispositivos e drivers");
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

        var filtered = Devices.AsEnumerable();

        if (filter == "Com Erro")
        {
            filtered = filtered.Where(d => d.IsProblem);
        }
        else if (filter == "Vídeo")
        {
            filtered = filtered.Where(d => d.ClassName.Contains("Display", StringComparison.OrdinalIgnoreCase));
        }
        else if (filter == "Áudio")
        {
            filtered = filtered.Where(d => d.ClassName.Contains("Audio", StringComparison.OrdinalIgnoreCase) || d.ClassName.Contains("Media", StringComparison.OrdinalIgnoreCase));
        }
        else if (filter == "Rede")
        {
            filtered = filtered.Where(d => d.ClassName.Contains("Net", StringComparison.OrdinalIgnoreCase));
        }
        else if (filter == "Armazenamento")
        {
            filtered = filtered.Where(d => d.ClassName.Contains("Disk", StringComparison.OrdinalIgnoreCase) || d.ClassName.Contains("SCSI", StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(query))
        {
            filtered = filtered.Where(d =>
                d.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                d.Manufacturer.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                d.ClassName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                d.DeviceId.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredDevices = filtered.ToList();
    }
}
