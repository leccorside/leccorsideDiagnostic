using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LeccorWindowsHealthCenter.Application.Network;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.Network;

public sealed class NetworkDiagnosticsService : INetworkDiagnosticsService
{
    private readonly INetworkProvider _networkProvider;
    private readonly ILogger<NetworkDiagnosticsService> _logger;

    public NetworkDiagnosticsService(INetworkProvider networkProvider, ILogger<NetworkDiagnosticsService> logger)
    {
        _networkProvider = networkProvider;
        _logger = logger;
    }

    public async Task<NetworkDiagnosticResult> RunDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var adapters = await _networkProvider.GetAdaptersAsync(cancellationToken);
        var activeAdapter = adapters.FirstOrDefault(a => a.IsUp && a.HasGateway);

        if (activeAdapter == null)
        {
            warnings.Add("Nenhum adaptador de rede ativo com Gateway padrão foi encontrado.");
            return new NetworkDiagnosticResult
            {
                HasInternetConnection = false,
                GatewayReachable = false,
                DnsResolutionSuccess = false,
                PacketLossPercent = 100,
                DiagnosticWarnings = warnings,
            };
        }

        // 1. Teste de Gateway
        double? gatewayLatency = null;
        var gatewayReachable = false;
        var gatewayIp = activeAdapter.GatewayAddresses.Count > 0 ? activeAdapter.GatewayAddresses[0] : null;

        if (!string.IsNullOrWhiteSpace(gatewayIp))
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(IPAddress.Parse(gatewayIp), 1500);
                if (reply.Status == IPStatus.Success)
                {
                    gatewayLatency = reply.RoundtripTime;
                    gatewayReachable = true;
                }
                else
                {
                    warnings.Add($"O Gateway padrão ({gatewayIp}) não respondeu ao teste de ping ({reply.Status}).");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro ao testar ping no gateway {Gateway}", gatewayIp);
                warnings.Add($"Não foi possível alcançar o Gateway padrão ({gatewayIp}).");
            }
        }

        // 2. Teste de Resolução DNS
        double? dnsResolutionTime = null;
        var dnsSuccess = false;

        try
        {
            var sw = Stopwatch.StartNew();
            var dnsTask = Dns.GetHostAddressesAsync("www.microsoft.com", AddressFamily.InterNetwork, cancellationToken);
            var completedTask = await Task.WhenAny(dnsTask, Task.Delay(2500, cancellationToken));

            if (completedTask == dnsTask)
            {
                var addresses = await dnsTask;
                sw.Stop();
                if (addresses.Length > 0)
                {
                    dnsResolutionTime = sw.Elapsed.TotalMilliseconds;
                    dnsSuccess = true;
                    if (dnsResolutionTime > 200)
                    {
                        warnings.Add($"Tempo de resposta do DNS elevado ({dnsResolutionTime:0} ms).");
                    }
                }
            }
            else
            {
                warnings.Add("A resolução de nomes DNS atingiu o tempo limite (> 2,5 s).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha na resolução de nomes DNS");
            warnings.Add("Falha na resolução de nomes via DNS.");
        }

        // 3. Teste de Ping para DNS Público (1.1.1.1 e 8.8.8.8) com 3 amostras para perda e latência
        var publicIps = new[] { IPAddress.Parse("1.1.1.1"), IPAddress.Parse("8.8.8.8") };
        var totalPackets = 0;
        var lostPackets = 0;
        var latencies = new List<long>();

        foreach (var ip in publicIps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var i = 0; i < 2; i++)
            {
                totalPackets++;
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(ip, 1200);
                    if (reply.Status == IPStatus.Success)
                    {
                        latencies.Add(reply.RoundtripTime);
                    }
                    else
                    {
                        lostPackets++;
                    }
                }
                catch
                {
                    lostPackets++;
                }
            }
        }

        var packetLoss = totalPackets > 0 ? (lostPackets / (double)totalPackets) * 100 : 100;
        double? publicDnsLatency = latencies.Count > 0 ? latencies.Average() : null;

        if (packetLoss > 0 && packetLoss < 100)
        {
            warnings.Add($"Detectada perda de pacotes de {packetLoss:0}% na conexão.");
        }

        var hasInternet = (dnsSuccess || latencies.Count > 0) && packetLoss < 100;

        return new NetworkDiagnosticResult
        {
            HasInternetConnection = hasInternet,
            GatewayLatencyMs = gatewayLatency,
            GatewayReachable = gatewayReachable,
            DnsResolutionTimeMs = dnsResolutionTime,
            DnsResolutionSuccess = dnsSuccess,
            PublicDnsLatencyMs = publicDnsLatency,
            PacketLossPercent = packetLoss,
            DiagnosticWarnings = warnings,
        };
    }
}
