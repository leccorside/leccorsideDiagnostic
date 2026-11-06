using System.Globalization;
using LeccorWindowsHealthCenter.Application.Network;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Network;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.Network;

public class NetworkRulesTests
{
    private static List<Finding> Run(IRule rule, IEnumerable<Observation> observations)
    {
        var engine = new RuleEngine([rule], new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance);
        var result = new DiagnosticResult
        {
            ScannerId = NetworkScanner.ScannerId,
            ScannerName = NetworkScanner.ScannerId,
            Module = ScanModule.Network,
            Status = ScannerRunStatus.Completed,
            Metrics = [],
            Observations = observations.ToList(),
        };
        return engine.Evaluate([result]).SelectMany(r => r.Findings).ToList();
    }

    private static Observation CreateDiagnosticObservation(
        bool hasInternet,
        double? gatewayLatency,
        double? dnsTime,
        double packetLoss) => new()
    {
        Kind = NetworkNames.ObservationDiagnostic,
        Subject = "NetworkConnectivity",
        ScannerId = NetworkScanner.ScannerId,
        Attributes = new Dictionary<string, string>
        {
            [NetworkNames.AttrInternet] = hasInternet ? "true" : "false",
            [NetworkNames.AttrGatewayLatency] = gatewayLatency?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty,
            [NetworkNames.AttrDnsResolutionTime] = dnsTime?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty,
            [NetworkNames.AttrPacketLoss] = packetLoss.ToString("0.#", CultureInfo.InvariantCulture),
        },
    };

    [Fact]
    public void NetworkNoGatewayRule_Triggers_WhenHasInternetIsFalse()
    {
        var rule = new NetworkNoGatewayRule();
        var obs = CreateDiagnosticObservation(hasInternet: false, gatewayLatency: null, dnsTime: null, packetLoss: 100);

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Equal(FindingCategory.Network, finding.Category);
    }

    [Fact]
    public void NetworkNoGatewayRule_DoesNotTrigger_WhenInternetIsConnected()
    {
        var rule = new NetworkNoGatewayRule();
        var obs = CreateDiagnosticObservation(hasInternet: true, gatewayLatency: 5, dnsTime: 20, packetLoss: 0);

        var findings = Run(rule, [obs]);

        Assert.Empty(findings);
    }

    [Fact]
    public void NetworkDnsSlowRule_Triggers_WhenDnsResolutionTimeExceedsThreshold()
    {
        var rule = new NetworkDnsSlowRule();
        var obs = CreateDiagnosticObservation(hasInternet: true, gatewayLatency: 5, dnsTime: 450.5, packetLoss: 0);

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.Low, finding.Severity);
        Assert.Equal(FindingCategory.Network, finding.Category);
        Assert.Contains("451 ms", finding.Title);
    }

    [Fact]
    public void NetworkDnsSlowRule_DoesNotTrigger_WhenDnsTimeIsFast()
    {
        var rule = new NetworkDnsSlowRule();
        var obs = CreateDiagnosticObservation(hasInternet: true, gatewayLatency: 5, dnsTime: 35.0, packetLoss: 0);

        var findings = Run(rule, [obs]);

        Assert.Empty(findings);
    }
}

