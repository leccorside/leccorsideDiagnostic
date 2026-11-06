using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.WindowsUpdate;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public sealed class UpdateHistoryRowViewModel
{
    public UpdateHistoryRowViewModel(UpdateHistoryItem item)
    {
        Item = item;
    }

    public UpdateHistoryItem Item { get; }

    public string HotFixId => Item.HotFixId;

    public string Description => !string.IsNullOrWhiteSpace(Item.Description) ? Item.Description : "Atualização do Windows";

    public string InstalledOnText => Item.InstalledOn.HasValue
        ? Item.InstalledOn.Value.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)
        : "—";

    public string InstalledBy => !string.IsNullOrWhiteSpace(Item.InstalledBy) ? Item.InstalledBy : "Sistema (NT AUTHORITY\\SYSTEM)";
}

public sealed partial class WindowsUpdateViewModel : ObservableObject, IPageLifecycle
{
    private readonly IWindowsUpdateProvider _windowsUpdateProvider;
    private readonly ILogger<WindowsUpdateViewModel> _logger;

    public WindowsUpdateViewModel(IWindowsUpdateProvider windowsUpdateProvider, ILogger<WindowsUpdateViewModel> logger)
    {
        _windowsUpdateProvider = windowsUpdateProvider;
        _logger = logger;
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRebootPending;

    [ObservableProperty]
    private string _rebootReason = string.Empty;

    [ObservableProperty]
    private string _lastScanText = "—";

    [ObservableProperty]
    private string _lastInstallText = "—";

    [ObservableProperty]
    private string _wuauservState = "—";

    [ObservableProperty]
    private string _bitsState = "—";

    [ObservableProperty]
    private string _dosvcState = "—";

    [ObservableProperty]
    private int _recentErrorCount;

    public ObservableCollection<UpdateHistoryRowViewModel> History { get; } = [];

    public ObservableCollection<string> RecentErrorMessages { get; } = [];

    public void Activate()
    {
        if (History.Count == 0)
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

            var status = await _windowsUpdateProvider.GetStatusAsync(CancellationToken.None);
            IsRebootPending = status.IsRebootPending;
            RebootReason = status.RebootReason ?? string.Empty;

            LastScanText = status.LastSuccessfulScanTime.HasValue
                ? status.LastSuccessfulScanTime.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)
                : "Não registrado";

            LastInstallText = status.LastSuccessfulInstallTime.HasValue
                ? status.LastSuccessfulInstallTime.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)
                : "Não registrado";

            WuauservState = status.ServiceStatus.WuauservState switch
            {
                "Automatic" => "Automático (Ativo)",
                "Manual" => "Manual (Sob Demanda)",
                "Disabled" => "Desativado (Alerta)",
                _ => status.ServiceStatus.WuauservState,
            };

            BitsState = status.ServiceStatus.BitsState;
            DosvcState = status.ServiceStatus.DosvcState;

            RecentErrorCount = status.RecentErrorCount24h;
            RecentErrorMessages.Clear();
            foreach (var err in status.RecentErrorMessages)
            {
                RecentErrorMessages.Add(err);
            }

            var historyItems = await _windowsUpdateProvider.GetHistoryAsync(CancellationToken.None);
            History.Clear();
            foreach (var h in historyItems)
            {
                History.Add(new UpdateHistoryRowViewModel(h));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar status do Windows Update");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenWindowsUpdateSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:windowsupdate",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível abrir as configurações do Windows Update");
        }
    }
}
