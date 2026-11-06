using System.Globalization;
using LeccorWindowsHealthCenter.Application.Network;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Diagnostics.Network;

public sealed class NetworkScanner : DiagnosticScannerBase
{
    public const string ScannerId = "network";

    private readonly INetworkProvider _networkProvider;
    private readonly INetworkDiagnosticsService _diagnosticsService;

    public NetworkScanner(
        INetworkProvider networkProvider,
        INetworkDiagnosticsService diagnosticsService,
        ILogger<NetworkScanner> logger)
        : base(logger)
    {
        _networkProvider = networkProvider;
        _diagnosticsService = diagnosticsService;
    }

    public override string Id => ScannerId;

    public override string Name => "Rede e Conectividade";

    public override ScanModule Module => ScanModule.Network;

    public override bool IncludedInQuickScan => true;

    public override int Order => 105;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var adapters = await _networkProvider.GetAdaptersAsync(cancellationToken);

        collector.AddMetric("network.adapters.count", adapters.Count, "count");
        collector.AddMetric("network.adapters.up", adapters.Count(a => a.IsUp), "count");

        foreach (var adapter in adapters)
        {
            var attributes = new Dictionary<string, string>
            {
                [NetworkNames.AttrAdapterName] = adapter.Name,
                [NetworkNames.AttrDescription] = adapter.Description,
                [NetworkNames.AttrCategory] = adapter.Category.ToString(),
                [NetworkNames.AttrStatus] = adapter.OperationalStatus,
                [NetworkNames.AttrSpeed] = adapter.SpeedBps.ToString(CultureInfo.InvariantCulture),
                [NetworkNames.AttrIpv4] = string.Join(", ", adapter.Ipv4Addresses),
                [NetworkNames.AttrGateway] = string.Join(", ", adapter.GatewayAddresses),
                [NetworkNames.AttrDns] = string.Join(", ", adapter.DnsAddresses),
                [NetworkNames.AttrHasGateway] = adapter.HasGateway ? "true" : "false",
            };

            collector.AddObservation(NetworkNames.ObservationAdapter, adapter.Name, attributes);
        }

        // Executar diagnóstico rápido de conectividade
        var diag = await _diagnosticsService.RunDiagnosticsAsync(cancellationToken);

        collector.AddMetric("network.packet_loss", diag.PacketLossPercent, "%");
        if (diag.DnsResolutionTimeMs.HasValue)
        {
            collector.AddMetric("network.dns.latency", diag.DnsResolutionTimeMs.Value, "ms");
        }

        if (diag.GatewayLatencyMs.HasValue)
        {
            collector.AddMetric("network.gateway.latency", diag.GatewayLatencyMs.Value, "ms");
        }

        var diagAttributes = new Dictionary<string, string>
        {
            [NetworkNames.AttrInternet] = diag.HasInternetConnection ? "true" : "false",
            [NetworkNames.AttrGatewayLatency] = diag.GatewayLatencyMs?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty,
            [NetworkNames.AttrDnsResolutionTime] = diag.DnsResolutionTimeMs?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty,
            [NetworkNames.AttrPublicLatency] = diag.PublicDnsLatencyMs?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty,
            [NetworkNames.AttrPacketLoss] = diag.PacketLossPercent.ToString("0.#", CultureInfo.InvariantCulture),
        };

        collector.AddObservation(NetworkNames.ObservationDiagnostic, "NetworkConnectivity", diagAttributes);
    }
}
