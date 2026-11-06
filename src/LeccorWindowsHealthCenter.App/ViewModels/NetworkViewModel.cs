using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Network;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public sealed class NetworkAdapterRowViewModel
{
    public NetworkAdapterRowViewModel(NetworkAdapterItem item)
    {
        Item = item;
    }

    public NetworkAdapterItem Item { get; }

    public string Name => Item.Name;

    public string Description => Item.Description;

    public NetworkAdapterCategory Category => Item.Category;

    public string CategoryText => Item.Category switch
    {
        NetworkAdapterCategory.Ethernet => "Ethernet (Cabo)",
        NetworkAdapterCategory.WiFi => "Wi-Fi (Sem Fio)",
        NetworkAdapterCategory.Vpn => "VPN / Túnel",
        NetworkAdapterCategory.Docker => "Docker Virtual",
        NetworkAdapterCategory.Wsl => "WSL (Linux)",
        NetworkAdapterCategory.HyperV => "Hyper-V Virtual Switch",
        NetworkAdapterCategory.Loopback => "Loopback Local",
        _ => "Outro Adaptador",
    };

    public bool IsUp => Item.IsUp;

    public string OperationalStatus => Item.OperationalStatus;

    public string StatusText => Item.IsUp ? "Conectado" : "Desconectado";

    public string SpeedText => Item.SpeedBps > 0
        ? FormatSpeed(Item.SpeedBps)
        : "—";

    public string MacAddress => Item.MacAddress ?? "—";

    public string Ipv4Text => Item.Ipv4Addresses.Count > 0 ? string.Join(", ", Item.Ipv4Addresses) : "Não atribuído";

    public string GatewayText => Item.GatewayAddresses.Count > 0 ? string.Join(", ", Item.GatewayAddresses) : "Nenhum";

    public string DnsText => Item.DnsAddresses.Count > 0 ? string.Join(", ", Item.DnsAddresses) : "Não configurado";

    private static string FormatSpeed(long bps)
    {
        if (bps >= 1_000_000_000)
        {
            return $"{(bps / 1_000_000_000.0):0.#} Gbps";
        }

        if (bps >= 1_000_000)
        {
            return $"{(bps / 1_000_000.0):0.#} Mbps";
        }

        return $"{bps} bps";
    }
}

public sealed partial class NetworkViewModel : ObservableObject, IPageLifecycle
{
    private readonly INetworkProvider _networkProvider;
    private readonly INetworkDiagnosticsService _diagnosticsService;
    private readonly ILogger<NetworkViewModel> _logger;

    public NetworkViewModel(
        INetworkProvider networkProvider,
        INetworkDiagnosticsService diagnosticsService,
        ILogger<NetworkViewModel> logger)
    {
        _networkProvider = networkProvider;
        _diagnosticsService = diagnosticsService;
        _logger = logger;
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isDiagnosing;

    [ObservableProperty]
    private NetworkAdapterRowViewModel? _selectedAdapter;

    [ObservableProperty]
    private int _totalAdaptersCount;

    [ObservableProperty]
    private int _activeAdaptersCount;

    [ObservableProperty]
    private bool _hasInternet;

    [ObservableProperty]
    private string _internetStatusText = "Aguardando teste...";

    [ObservableProperty]
    private string _gatewayLatencyText = "—";

    [ObservableProperty]
    private string _dnsResolutionText = "—";

    [ObservableProperty]
    private string _publicLatencyText = "—";

    [ObservableProperty]
    private string _packetLossText = "—";

    public ObservableCollection<NetworkAdapterRowViewModel> Adapters { get; } = [];

    public ObservableCollection<string> DiagnosticAlerts { get; } = [];

    public void Activate()
    {
        if (Adapters.Count == 0)
        {
            _ = RefreshAdaptersCommand.ExecuteAsync(null);
            _ = RunDiagnosticsCommand.ExecuteAsync(null);
        }
    }

    public void Deactivate()
    {
    }

    [RelayCommand]
    private async Task RefreshAdaptersAsync()
    {
        try
        {
            IsLoading = true;
            var adapters = await _networkProvider.GetAdaptersAsync(CancellationToken.None);
            Adapters.Clear();

            foreach (var a in adapters)
            {
                Adapters.Add(new NetworkAdapterRowViewModel(a));
            }

            TotalAdaptersCount = Adapters.Count;
            ActiveAdaptersCount = Adapters.Count(a => a.IsUp);

            if (SelectedAdapter == null || !Adapters.Contains(SelectedAdapter))
            {
                SelectedAdapter = Adapters.FirstOrDefault(a => a.IsUp && a.Item.HasGateway) ??
                                  (Adapters.Count > 0 ? Adapters[0] : null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter adaptadores de rede");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RunDiagnosticsAsync()
    {
        try
        {
            IsDiagnosing = true;
            DiagnosticAlerts.Clear();

            var result = await _diagnosticsService.RunDiagnosticsAsync(CancellationToken.None);

            HasInternet = result.HasInternetConnection;
            InternetStatusText = result.HasInternetConnection ? "Conexão Ativa com a Internet" : "Sem Acesso à Internet";

            GatewayLatencyText = result.GatewayLatencyMs.HasValue
                ? $"{result.GatewayLatencyMs.Value:0.#} ms"
                : (result.GatewayReachable ? "OK" : "Falhou");

            DnsResolutionText = result.DnsResolutionTimeMs.HasValue
                ? $"{result.DnsResolutionTimeMs.Value:0.#} ms"
                : (result.DnsResolutionSuccess ? "OK" : "Falha");

            PublicLatencyText = result.PublicDnsLatencyMs.HasValue
                ? $"{result.PublicDnsLatencyMs.Value:0.#} ms"
                : "—";

            PacketLossText = $"{result.PacketLossPercent:0}%";

            foreach (var w in result.DiagnosticWarnings)
            {
                DiagnosticAlerts.Add(w);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar diagnóstico de conectividade de rede");
            InternetStatusText = "Erro ao diagnosticar";
        }
        finally
        {
            IsDiagnosing = false;
        }
    }

    [RelayCommand]
    private void OpenNetworkConnections()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "control.exe",
                Arguments = "ncpa.cpl",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível abrir Conexões de Rede (ncpa.cpl)");
        }
    }
}
