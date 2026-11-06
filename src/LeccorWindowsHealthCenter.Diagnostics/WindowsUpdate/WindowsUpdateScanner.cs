using System.Globalization;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.WindowsUpdate;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Diagnostics.WindowsUpdate;

public sealed class WindowsUpdateScanner : DiagnosticScannerBase
{
    public const string ScannerId = "windowsupdate";

    private readonly IWindowsUpdateProvider _windowsUpdateProvider;

    public WindowsUpdateScanner(IWindowsUpdateProvider windowsUpdateProvider, ILogger<WindowsUpdateScanner> logger)
        : base(logger)
    {
        _windowsUpdateProvider = windowsUpdateProvider;
    }

    public override string Id => ScannerId;

    public override string Name => "Windows Update";

    public override ScanModule Module => ScanModule.WindowsUpdate;

    public override bool IncludedInQuickScan => true;

    public override int Order => 110;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var status = await _windowsUpdateProvider.GetStatusAsync(cancellationToken);

        collector.AddMetric("windowsupdate.pending_count", status.PendingUpdatesCount, "count");
        collector.AddMetric("windowsupdate.errors_24h", status.RecentErrorCount24h, "count");

        var attributes = new Dictionary<string, string>
        {
            [WindowsUpdateNames.AttrRebootPending] = status.IsRebootPending ? "true" : "false",
            [WindowsUpdateNames.AttrRebootReason] = status.RebootReason ?? string.Empty,
            [WindowsUpdateNames.AttrLastScan] = status.LastSuccessfulScanTime?.ToString("O") ?? string.Empty,
            [WindowsUpdateNames.AttrLastInstall] = status.LastSuccessfulInstallTime?.ToString("O") ?? string.Empty,
            [WindowsUpdateNames.AttrPendingCount] = status.PendingUpdatesCount.ToString(CultureInfo.InvariantCulture),
            [WindowsUpdateNames.AttrWuauservState] = status.ServiceStatus.WuauservState,
            [WindowsUpdateNames.AttrBitsState] = status.ServiceStatus.BitsState,
            [WindowsUpdateNames.AttrDosvcState] = status.ServiceStatus.DosvcState,
            [WindowsUpdateNames.AttrRecentErrorCount] = status.RecentErrorCount24h.ToString(CultureInfo.InvariantCulture),
        };

        collector.AddObservation(WindowsUpdateNames.ObservationStatus, "WindowsUpdate", attributes);
    }
}
