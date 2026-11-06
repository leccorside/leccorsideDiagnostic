using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.DevTools;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public sealed partial class DevToolsViewModel : ObservableObject, IPageLifecycle
{
    private readonly IDevToolsProvider _devToolsProvider;
    private readonly ILogger<DevToolsViewModel> _logger;

    public ObservableCollection<DevToolItem> Tools { get; } = [];
    public ObservableCollection<WslDistributionInfo> WslDistributions { get; } = [];

    [ObservableProperty]
    private DockerStatusInfo? _docker;

    [ObservableProperty]
    private PathEnvironmentAnalysis? _pathAnalysis;

    [ObservableProperty]
    private bool _hasHyperV;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    public DevToolsViewModel(IDevToolsProvider devToolsProvider, ILogger<DevToolsViewModel> logger)
    {
        _devToolsProvider = devToolsProvider;
        _logger = logger;
    }

    public void Activate()
    {
        _ = LoadDevToolsAsync();
    }

    public void Deactivate()
    {
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadDevToolsAsync();
    }

    private async Task LoadDevToolsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Detectando ferramentas de desenvolvimento, Docker e WSL...";

        try
        {
            var summary = await _devToolsProvider.GetDevEnvironmentAsync(CancellationToken.None).ConfigureAwait(true);

            Tools.Clear();
            foreach (var t in summary.Tools.OrderByDescending(t => t.IsInstalled).ThenBy(t => t.Category))
            {
                Tools.Add(t);
            }

            WslDistributions.Clear();
            foreach (var w in summary.WslDistributions)
            {
                WslDistributions.Add(w);
            }

            Docker = summary.Docker;
            PathAnalysis = summary.PathAnalysis;
            HasHyperV = summary.HasHyperV;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar dados de ferramentas de desenvolvimento");
            StatusMessage = "Falha ao carregar informações de desenvolvimento.";
        }
        finally
        {
            IsBusy = false;
            StatusMessage = null;
        }
    }
}

