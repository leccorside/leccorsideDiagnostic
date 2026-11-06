using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Crash;
using LeccorWindowsHealthCenter.Application.Security;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public sealed partial class SecurityViewModel : ObservableObject, IPageLifecycle
{
    private readonly ISecurityProvider _securityProvider;
    private readonly ICrashAnalyzerProvider _crashProvider;
    private readonly ILogger<SecurityViewModel> _logger;

    public ObservableCollection<AntivirusProductInfo> AntivirusProducts { get; } = [];
    public ObservableCollection<FirewallProductInfo> FirewallProducts { get; } = [];
    public ObservableCollection<SuspiciousPersistenceItem> SuspiciousPersistences { get; } = [];
    public ObservableCollection<DumpFileInfo> RecentDumps { get; } = [];
    public ObservableCollection<AppCrashCount> TopCrashingApps { get; } = [];

    [ObservableProperty]
    private SecurityStatusSummary? _security;

    [ObservableProperty]
    private CrashSummary? _crash;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    public SecurityViewModel(
        ISecurityProvider securityProvider,
        ICrashAnalyzerProvider crashProvider,
        ILogger<SecurityViewModel> logger)
    {
        _securityProvider = securityProvider;
        _crashProvider = crashProvider;
        _logger = logger;
    }

    public void Activate()
    {
        _ = LoadSecurityAndCrashAsync();
    }

    public void Deactivate()
    {
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadSecurityAndCrashAsync();
    }

    private async Task LoadSecurityAndCrashAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Auditando Segurança do Windows e Crash Analyzer...";

        try
        {
            var secTask = _securityProvider.GetSecurityStatusAsync(CancellationToken.None);
            var crashTask = _crashProvider.GetCrashSummaryAsync(CancellationToken.None);

            await Task.WhenAll(secTask, crashTask).ConfigureAwait(true);

            Security = secTask.Result;
            Crash = crashTask.Result;

            AntivirusProducts.Clear();
            foreach (var av in Security.AntivirusProducts)
            {
                AntivirusProducts.Add(av);
            }

            FirewallProducts.Clear();
            foreach (var fw in Security.FirewallProducts)
            {
                FirewallProducts.Add(fw);
            }

            SuspiciousPersistences.Clear();
            foreach (var p in Security.SuspiciousPersistences)
            {
                SuspiciousPersistences.Add(p);
            }

            RecentDumps.Clear();
            foreach (var d in Crash.DumpFiles.Take(10))
            {
                RecentDumps.Add(d);
            }

            TopCrashingApps.Clear();
            foreach (var app in Crash.TopCrashingApps.Take(10))
            {
                TopCrashingApps.Add(app);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar auditoria de segurança e travamentos");
            StatusMessage = "Falha ao carregar informações de segurança.";
        }
        finally
        {
            IsBusy = false;
            StatusMessage = null;
        }
    }
}

