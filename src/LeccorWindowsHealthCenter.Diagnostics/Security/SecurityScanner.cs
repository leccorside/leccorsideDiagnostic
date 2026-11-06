using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.Security;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Diagnostics.Security;

public sealed class SecurityScanner : DiagnosticScannerBase
{
    public const string ScannerId = "security";

    private readonly ISecurityProvider _securityProvider;

    public SecurityScanner(ISecurityProvider securityProvider, ILogger<SecurityScanner> logger)
        : base(logger)
    {
        _securityProvider = securityProvider;
    }

    public override string Id => ScannerId;

    public override string Name => "Segurança do Windows e Persistências";

    public override ScanModule Module => ScanModule.Windows;

    public override bool IncludedInQuickScan => true;

    public override int Order => 120;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var status = await _securityProvider.GetSecurityStatusAsync(cancellationToken);

        collector.AddMetric("security.antivirus_count", status.AntivirusProducts.Count, "count");
        collector.AddMetric("security.suspicious_count", status.SuspiciousPersistences.Count, "count");

        // Antivírus
        foreach (var av in status.AntivirusProducts)
        {
            var attrs = new Dictionary<string, string>
            {
                [SecurityNames.AttrName] = av.Name,
                [SecurityNames.AttrEnabled] = av.IsEnabled ? "true" : "false",
                [SecurityNames.AttrUpToDate] = av.IsUpToDate ? "true" : "false"
            };
            collector.AddObservation(SecurityNames.ObservationAntivirus, av.Name, attrs);
        }

        // UAC
        var uacAttrs = new Dictionary<string, string>
        {
            [SecurityNames.AttrUacEnabled] = status.IsUacEnabled ? "true" : "false"
        };
        collector.AddObservation(SecurityNames.ObservationUac, "UAC", uacAttrs);

        // Persistências
        foreach (var item in status.SuspiciousPersistences)
        {
            var attrs = new Dictionary<string, string>
            {
                [SecurityNames.AttrPersistenceType] = item.Type,
                [SecurityNames.AttrTarget] = item.Target,
                [SecurityNames.AttrSuspicious] = item.IsSuspicious ? "true" : "false"
            };
            collector.AddObservation(SecurityNames.ObservationPersistence, item.Target, attrs);
        }
    }
}

