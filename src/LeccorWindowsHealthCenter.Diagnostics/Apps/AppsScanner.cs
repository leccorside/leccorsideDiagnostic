using System.Globalization;
using LeccorWindowsHealthCenter.Application.Apps;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Diagnostics.Apps;

public sealed class AppsScanner : DiagnosticScannerBase
{
    public const string ScannerId = "apps";

    private readonly IAppsProvider _appsProvider;

    public AppsScanner(IAppsProvider appsProvider, ILogger<AppsScanner> logger)
        : base(logger)
    {
        _appsProvider = appsProvider;
    }

    public override string Id => ScannerId;

    public override string Name => "Aplicativos Instalados";

    public override ScanModule Module => ScanModule.Applications;

    public override bool IncludedInQuickScan => true;

    public override int Order => 95;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var apps = await _appsProvider.GetInstalledAppsAsync(cancellationToken);

        collector.AddMetric("apps.total.count", apps.Count, "count");
        collector.AddMetric("apps.win32.count", apps.Count(a => a.SourceType == AppSourceType.Win32), "count");
        collector.AddMetric("apps.store.count", apps.Count(a => a.SourceType == AppSourceType.WindowsStore), "count");
        collector.AddMetric("apps.orphaned.count", apps.Count(a => a.IsOrphaned), "count");
        collector.AddMetric("apps.duplicates.count", apps.Count(a => a.HasDuplicateVersion), "count");

        foreach (var app in apps)
        {
            var attributes = new Dictionary<string, string>
            {
                [AppsNames.AttrName] = app.DisplayName,
                [AppsNames.AttrVersion] = app.DisplayVersion ?? string.Empty,
                [AppsNames.AttrPublisher] = app.Publisher ?? string.Empty,
                [AppsNames.AttrSource] = app.SourceType.ToString(),
                [AppsNames.AttrSize] = app.EstimatedSizeBytes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                [AppsNames.AttrInstallDate] = app.InstallDate?.ToString("O") ?? string.Empty,
                [AppsNames.AttrUninstallString] = app.UninstallString ?? string.Empty,
                [AppsNames.AttrIsOrphaned] = app.IsOrphaned ? "true" : "false",
                [AppsNames.AttrHasDuplicate] = app.HasDuplicateVersion ? "true" : "false",
            };

            collector.AddObservation(AppsNames.ObservationApp, app.DisplayName, attributes);
        }
    }
}
