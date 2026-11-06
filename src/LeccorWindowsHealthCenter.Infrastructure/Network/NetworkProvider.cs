using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LeccorWindowsHealthCenter.Application.Network;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.Network;

public sealed class NetworkProvider : INetworkProvider
{
    private readonly ILogger<NetworkProvider> _logger;

    public NetworkProvider(ILogger<NetworkProvider> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<NetworkAdapterItem>> GetAdaptersAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var result = new List<NetworkAdapterItem>();

            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var iface in interfaces)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var category = DetermineCategory(iface);
                    var ipProperties = iface.GetIPProperties();

                    var ipv4List = new List<string>();
                    var ipv6List = new List<string>();

                    foreach (var addr in ipProperties.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            ipv4List.Add(addr.Address.ToString());
                        }
                        else if (addr.Address.AddressFamily == AddressFamily.InterNetworkV6 && !addr.Address.IsIPv6LinkLocal)
                        {
                            ipv6List.Add(addr.Address.ToString());
                        }
                    }

                    var gateways = ipProperties.GatewayAddresses
                        .Select(g => g.Address.ToString())
                        .Where(g => !string.IsNullOrWhiteSpace(g) && g != "0.0.0.0")
                        .ToList();

                    var dnsServers = ipProperties.DnsAddresses
                        .Select(d => d.ToString())
                        .Where(d => !string.IsNullOrWhiteSpace(d))
                        .ToList();

                    var mac = string.Join(":", iface.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
                    if (string.IsNullOrWhiteSpace(mac) || mac == "00:00:00:00:00:00")
                    {
                        mac = null;
                    }

                    result.Add(new NetworkAdapterItem
                    {
                        Id = iface.Id,
                        Name = iface.Name,
                        Description = iface.Description,
                        Category = category,
                        OperationalStatus = iface.OperationalStatus.ToString(),
                        SpeedBps = iface.Speed > 0 ? iface.Speed : 0,
                        MacAddress = mac,
                        Ipv4Addresses = ipv4List,
                        Ipv6Addresses = ipv6List,
                        GatewayAddresses = gateways,
                        DnsAddresses = dnsServers,
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao obter interfaces de rede do sistema");
            }

            // Ordenar: adaptadores ativos com gateway primeiro, depois por nome
            return (IReadOnlyList<NetworkAdapterItem>)result
                .OrderByDescending(a => a.IsUp && a.HasGateway)
                .ThenByDescending(a => a.IsUp)
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, cancellationToken);
    }

    private static NetworkAdapterCategory DetermineCategory(NetworkInterface iface)
    {
        var nameDesc = $"{iface.Name} {iface.Description}".ToLowerInvariant();

        if (nameDesc.Contains("wsl"))
        {
            return NetworkAdapterCategory.Wsl;
        }

        if (nameDesc.Contains("docker"))
        {
            return NetworkAdapterCategory.Docker;
        }

        if (nameDesc.Contains("hyper-v") || nameDesc.Contains("vethernet"))
        {
            return NetworkAdapterCategory.HyperV;
        }

        if (nameDesc.Contains("vpn") || nameDesc.Contains("wireguard") || nameDesc.Contains("openvpn") ||
            nameDesc.Contains("tailscale") || nameDesc.Contains("zerotier") || nameDesc.Contains("tap-") ||
            nameDesc.Contains("tun-"))
        {
            return NetworkAdapterCategory.Vpn;
        }

        if (iface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
        {
            return NetworkAdapterCategory.WiFi;
        }

        if (iface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
        {
            if (nameDesc.Contains("virtual") || nameDesc.Contains("pseudo"))
            {
                return NetworkAdapterCategory.Virtual;
            }

            return NetworkAdapterCategory.Ethernet;
        }

        if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
        {
            return NetworkAdapterCategory.Loopback;
        }

        return NetworkAdapterCategory.Other;
    }
}
