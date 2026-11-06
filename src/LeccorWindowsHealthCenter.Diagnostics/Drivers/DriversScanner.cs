using System.Globalization;
using LeccorWindowsHealthCenter.Application.Drivers;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Diagnostics.Drivers;

public sealed class DriversScanner : DiagnosticScannerBase
{
    public const string ScannerId = "drivers";

    private readonly IDriversProvider _provider;

    public DriversScanner(IDriversProvider provider, ILogger<DriversScanner> logger)
        : base(logger)
    {
        _provider = provider;
    }

    public override string Id => ScannerId;

    public override string Name => "Drivers e Dispositivos";

    public override ScanModule Module => ScanModule.Drivers;

    public override bool IncludedInQuickScan => true;

    public override int Order => 90;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var devices = await _provider.GetDevicesAsync(cancellationToken).ConfigureAwait(false);

        var problemCount = devices.Count(d => d.IsProblemDevice && !d.IsMissingDriver);
        var missingCount = devices.Count(d => d.IsMissingDriver);

        collector.AddMetric("drivers.total_devices", devices.Count, "dispositivos");
        collector.AddMetric("drivers.problem_devices", problemCount, "dispositivos");
        collector.AddMetric("drivers.missing_drivers", missingCount, "dispositivos");

        foreach (var dev in devices)
        {
            // Registra observações para todos os dispositivos que apresentarem código de erro
            if (dev.IsProblemDevice)
            {
                var attributes = new Dictionary<string, string>
                {
                    [DriverNames.AttrDeviceId] = dev.DeviceId,
                    [DriverNames.AttrName] = dev.Name,
                    [DriverNames.AttrErrorCode] = dev.ConfigManagerErrorCode.ToString(CultureInfo.InvariantCulture),
                    [DriverNames.AttrIsProblem] = dev.IsProblemDevice.ToString(),
                    [DriverNames.AttrIsMissing] = dev.IsMissingDriver.ToString(),
                };

                if (!string.IsNullOrWhiteSpace(dev.ClassName))
                {
                    attributes[DriverNames.AttrClass] = dev.ClassName;
                }

                if (!string.IsNullOrWhiteSpace(dev.Manufacturer))
                {
                    attributes[DriverNames.AttrManufacturer] = dev.Manufacturer;
                }

                if (!string.IsNullOrWhiteSpace(dev.StatusMessage))
                {
                    attributes[DriverNames.AttrStatusMessage] = dev.StatusMessage;
                }

                if (!string.IsNullOrWhiteSpace(dev.DriverVersion))
                {
                    attributes[DriverNames.AttrDriverVersion] = dev.DriverVersion;
                }

                if (dev.DriverDate.HasValue)
                {
                    attributes[DriverNames.AttrDriverDate] = dev.DriverDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                collector.AddObservation(
                    DriverNames.ObservationDevice,
                    dev.Name,
                    attributes);
            }
        }
    }
}

