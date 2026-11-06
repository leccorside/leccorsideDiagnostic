using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.Services;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Diagnostics.Services;

public sealed class ServicesScanner : DiagnosticScannerBase
{
    public const string ScannerId = "services";

    private readonly IServicesProvider _provider;

    public ServicesScanner(IServicesProvider provider, ILogger<ServicesScanner> logger)
        : base(logger)
    {
        _provider = provider;
    }

    public override string Id => ScannerId;

    public override string Name => "Serviços do Windows";

    public override ScanModule Module => ScanModule.Services;

    public override bool IncludedInQuickScan => true;

    public override int Order => 85;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var services = await _provider.GetServicesAsync(cancellationToken).ConfigureAwait(false);

        var runningCount = services.Count(s => s.State == WindowsServiceState.Running);
        var autoCount = services.Count(s => s.StartupType is WindowsServiceStartupType.Automatic or WindowsServiceStartupType.AutomaticDelayed);
        var missingCount = services.Count(s => !s.FileExists && s.StartupType != WindowsServiceStartupType.Disabled);

        collector.AddMetric("services.total", services.Count, "serviços");
        collector.AddMetric("services.running", runningCount, "serviços");
        collector.AddMetric("services.automatic", autoCount, "serviços");
        collector.AddMetric("services.missing_binary", missingCount, "serviços");

        foreach (var svc in services)
        {
            // Registra serviços com problemas, serviços automáticos ou serviços de terceiros
            if (!svc.FileExists || !svc.IsWindowsService || svc.StartupType is WindowsServiceStartupType.Automatic or WindowsServiceStartupType.AutomaticDelayed)
            {
                var attributes = new Dictionary<string, string>
                {
                    [WindowsServiceNames.AttrName] = svc.ServiceName,
                    [WindowsServiceNames.AttrDisplayName] = svc.DisplayName,
                    [WindowsServiceNames.AttrState] = svc.State.ToString(),
                    [WindowsServiceNames.AttrStartupType] = svc.StartupType.ToString(),
                    [WindowsServiceNames.AttrExists] = svc.FileExists.ToString(),
                    [WindowsServiceNames.AttrWindows] = svc.IsWindowsService.ToString(),
                };

                if (!string.IsNullOrWhiteSpace(svc.ExecutablePath))
                {
                    attributes[WindowsServiceNames.AttrExecutablePath] = svc.ExecutablePath;
                }

                if (!string.IsNullOrWhiteSpace(svc.Publisher))
                {
                    attributes[WindowsServiceNames.AttrPublisher] = svc.Publisher;
                }

                if (svc.IsSigned.HasValue)
                {
                    attributes[WindowsServiceNames.AttrSigned] = svc.IsSigned.Value.ToString();
                }

                collector.AddObservation(
                    WindowsServiceNames.ObservationService,
                    svc.ServiceName,
                    attributes);
            }
        }
    }
}

