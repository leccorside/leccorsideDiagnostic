namespace LeccorWindowsHealthCenter.Application.Network;

public enum NetworkAdapterCategory
{
    Ethernet,
    WiFi,
    Vpn,
    Docker,
    Wsl,
    HyperV,
    Virtual,
    Loopback,
    Other,
}

public sealed record NetworkAdapterItem
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required NetworkAdapterCategory Category { get; init; }

    public required string OperationalStatus { get; init; }

    public bool IsUp => string.Equals(OperationalStatus, "Up", StringComparison.OrdinalIgnoreCase);

    public long SpeedBps { get; init; }

    public string? MacAddress { get; init; }

    public IReadOnlyList<string> Ipv4Addresses { get; init; } = [];

    public IReadOnlyList<string> Ipv6Addresses { get; init; } = [];

    public IReadOnlyList<string> GatewayAddresses { get; init; } = [];

    public IReadOnlyList<string> DnsAddresses { get; init; } = [];

    public bool HasGateway => GatewayAddresses.Count > 0;
}

public sealed record NetworkDiagnosticResult
{
    public bool HasInternetConnection { get; init; }

    public double? GatewayLatencyMs { get; init; }

    public bool GatewayReachable { get; init; }

    public double? DnsResolutionTimeMs { get; init; }

    public bool DnsResolutionSuccess { get; init; }

    public double? PublicDnsLatencyMs { get; init; }

    public double PacketLossPercent { get; init; }

    public IReadOnlyList<string> DiagnosticWarnings { get; init; } = [];
}

public static class NetworkNames
{
    public const string ObservationAdapter = "network.adapter";
    public const string ObservationDiagnostic = "network.diagnostic";

    public const string AttrAdapterName = "adapterName";
    public const string AttrDescription = "description";
    public const string AttrCategory = "category";
    public const string AttrStatus = "status";
    public const string AttrSpeed = "speed";
    public const string AttrIpv4 = "ipv4";
    public const string AttrGateway = "gateway";
    public const string AttrDns = "dns";
    public const string AttrHasGateway = "hasGateway";

    public const string AttrInternet = "internet";
    public const string AttrGatewayLatency = "gatewayLatency";
    public const string AttrDnsResolutionTime = "dnsResolutionTime";
    public const string AttrPublicLatency = "publicLatency";
    public const string AttrPacketLoss = "packetLoss";
}

public interface INetworkProvider
{
    Task<IReadOnlyList<NetworkAdapterItem>> GetAdaptersAsync(CancellationToken cancellationToken);
}

public interface INetworkDiagnosticsService
{
    Task<NetworkDiagnosticResult> RunDiagnosticsAsync(CancellationToken cancellationToken);
}

