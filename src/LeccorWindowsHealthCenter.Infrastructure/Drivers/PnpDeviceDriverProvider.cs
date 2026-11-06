using System.Globalization;
using System.Management;
using LeccorWindowsHealthCenter.Application.Drivers;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.Drivers;

public sealed class PnpDeviceDriverProvider : IDriversProvider
{
    private readonly ILogger<PnpDeviceDriverProvider> _logger;

    public PnpDeviceDriverProvider(ILogger<PnpDeviceDriverProvider> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<DriverDeviceItem>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var driverMap = LoadSignedDrivers(cancellationToken);
            var list = new List<DriverDeviceItem>();

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\cimv2",
                    "SELECT DeviceID, Name, Caption, ClassGuid, ConfigManagerErrorCode, Status, Manufacturer FROM Win32_PnPEntity");

                using var results = searcher.Get();

                foreach (var item in results.Cast<ManagementObject>())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var devId = item["DeviceID"]?.ToString();
                        if (string.IsNullOrWhiteSpace(devId))
                        {
                            continue;
                        }

                        var name = item["Name"]?.ToString() ?? item["Caption"]?.ToString() ?? devId;
                        var classGuid = item["ClassGuid"]?.ToString();
                        var manufacturer = item["Manufacturer"]?.ToString();
                        var errCode = Convert.ToUInt32(item["ConfigManagerErrorCode"] ?? 0, CultureInfo.InvariantCulture);

                        driverMap.TryGetValue(devId, out var driverInfo);

                        list.Add(new DriverDeviceItem
                        {
                            DeviceId = devId,
                            Name = name,
                            ClassName = driverInfo?.ClassName,
                            ClassGuid = classGuid,
                            Manufacturer = !string.IsNullOrWhiteSpace(manufacturer) ? manufacturer : driverInfo?.Manufacturer,
                            DriverProvider = driverInfo?.DriverProvider,
                            DriverVersion = driverInfo?.DriverVersion,
                            DriverDate = driverInfo?.DriverDate,
                            ConfigManagerErrorCode = errCode,
                            StatusMessage = DescribeErrorCode(errCode),
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Falha ao processar dispositivo PnP");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao consultar Win32_PnPEntity");
            }

            return (IReadOnlyList<DriverDeviceItem>)list;
        }, cancellationToken).ConfigureAwait(false);
    }

    private Dictionary<string, SignedDriverInfo> LoadSignedDrivers(CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, SignedDriverInfo>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\cimv2",
                "SELECT DeviceID, DeviceClass, DriverProviderName, DriverVersion, DriverDate, Manufacturer FROM Win32_PnPSignedDriver");

            using var results = searcher.Get();

            foreach (var item in results.Cast<ManagementObject>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var devId = item["DeviceID"]?.ToString();
                    if (string.IsNullOrWhiteSpace(devId))
                    {
                        continue;
                    }

                    var className = item["DeviceClass"]?.ToString();
                    var provider = item["DriverProviderName"]?.ToString();
                    var version = item["DriverVersion"]?.ToString();
                    var mfg = item["Manufacturer"]?.ToString();
                    var dateStr = item["DriverDate"]?.ToString();

                    DateTimeOffset? driverDate = null;
                    if (!string.IsNullOrWhiteSpace(dateStr) && dateStr.Length >= 8)
                    {
                        if (DateTime.TryParseExact(dateStr[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        {
                            driverDate = new DateTimeOffset(dt, TimeSpan.Zero);
                        }
                    }

                    map[devId] = new SignedDriverInfo(className, provider, version, driverDate, mfg);
                }
                catch
                {
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao consultar Win32_PnPSignedDriver");
        }

        return map;
    }

    public static string DescribeErrorCode(uint code) => code switch
    {
        0 => "Funcionando corretamente.",
        10 => "O dispositivo não pode ser iniciado. Possível conflito de driver ou firmware desatualizado.",
        14 => "O computador precisa ser reiniciado para que o dispositivo funcione.",
        18 => "Reinstale os drivers para este dispositivo.",
        22 => "O dispositivo está desativado pelo usuário ou pelo sistema.",
        28 => "Os drivers para este dispositivo não estão instalados (Dispositivo Desconhecido).",
        31 => "O dispositivo não está funcionando corretamente porque o Windows não pôde carregar os drivers necessários.",
        43 => "O Windows interrompeu este dispositivo porque ele reportou problemas (Código 43).",
        _ => $"Código de status do Gerenciador de Dispositivos: {code}.",
    };

    private sealed record SignedDriverInfo(
        string? ClassName,
        string? DriverProvider,
        string? DriverVersion,
        DateTimeOffset? DriverDate,
        string? Manufacturer);
}

