using System.Globalization;
using System.Management;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LeccorWindowsHealthCenter.Infrastructure.SystemInfo;

/// <summary>
/// Inventário de hardware via WMI (somente leitura, sem administrador). Cada classe WMI é consultada isoladamente:
/// falha em uma vira aviso no inventário e as demais seguem. Valores ausentes ficam null — nunca estimados.
/// </summary>
public sealed class WmiHardwareInventoryProvider : IHardwareInventoryProvider, IDisposable
{
    /// <summary>Classe de dispositivos de vídeo no registro (GUID_DEVCLASS_DISPLAY).</summary>
    private const string DisplayClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    /// <summary>AdapterRAM é uint32: valores próximos de 4 GB indicam truncamento.</summary>
    private const long AdapterRamTruncationThreshold = 4L * 1024 * 1024 * 1024 - (64L * 1024 * 1024);

    // Só o timeout é alterado. No System.Management do .NET 10, Rewindable = false + ReturnImmediately devolve
    // coleções vazias em silêncio (verificado neste projeto; no .NET Framework funciona) — não "otimizar" de novo.
    private static readonly System.Management.EnumerationOptions QueryOptions = new() { Timeout = TimeSpan.FromSeconds(20) };

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private readonly ILogger<WmiHardwareInventoryProvider> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HardwareInventory? _cached;

    public WmiHardwareInventoryProvider(ILogger<WmiHardwareInventoryProvider> logger, TimeProvider timeProvider)
    {
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<HardwareInventory> GetAsync(CancellationToken cancellationToken, bool forceRefresh = false)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && _cached is not null && _timeProvider.GetUtcNow() - _cached.CapturedAt < CacheDuration)
            {
                return _cached;
            }

            var started = _timeProvider.GetTimestamp();
            _cached = await Task.Run(() => Collect(cancellationToken), cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Inventário de hardware lido via WMI em {DurationMs} ms ({Warnings} avisos)",
                (long)_timeProvider.GetElapsedTime(started).TotalMilliseconds, _cached.Warnings.Count);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private HardwareInventory Collect(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        var system = QueryFirst("SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem", warnings, cancellationToken);
        var os = QueryFirst("SELECT Caption, Version, BuildNumber, OSArchitecture, LastBootUpTime FROM Win32_OperatingSystem", warnings, cancellationToken);
        var bios = QueryFirst("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS", warnings, cancellationToken);
        var board = QueryFirst("SELECT Manufacturer, Product FROM Win32_BaseBoard", warnings, cancellationToken);

        var computer = new ComputerInfo
        {
            Manufacturer = Text(system, "Manufacturer"),
            Model = Text(system, "Model"),
            OperatingSystem = Text(os, "Caption"),
            OsVersion = Text(os, "Version"),
            OsBuild = Text(os, "BuildNumber"),
            OsArchitecture = Text(os, "OSArchitecture"),
            LastBoot = Date(os, "LastBootUpTime"),
            BiosVendor = Text(bios, "Manufacturer"),
            BiosVersion = Text(bios, "SMBIOSBIOSVersion"),
            BiosDate = Date(bios, "ReleaseDate"),
            Motherboard = Join(Text(board, "Manufacturer"), Text(board, "Product")),
        };

        var processors = Query("SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, L2CacheSize, L3CacheSize, VirtualizationFirmwareEnabled FROM Win32_Processor", warnings, cancellationToken)
            .Select(p => new ProcessorInfo
            {
                Name = Text(p, "Name") ?? "Processador desconhecido",
                Manufacturer = Text(p, "Manufacturer"),
                Cores = Int(p, "NumberOfCores"),
                LogicalProcessors = Int(p, "NumberOfLogicalProcessors"),
                MaxClockMHz = Int(p, "MaxClockSpeed"),
                L2CacheKb = Int(p, "L2CacheSize"),
                L3CacheKb = Int(p, "L3CacheSize"),
                VirtualizationFirmwareEnabled = p["VirtualizationFirmwareEnabled"] as bool?,
            })
            .ToList();

        var modules = Query("SELECT Capacity, Speed, ConfiguredClockSpeed, Manufacturer, PartNumber, DeviceLocator FROM Win32_PhysicalMemory", warnings, cancellationToken)
            .Select(m => new MemoryModuleInfo
            {
                CapacityBytes = Long(m, "Capacity") ?? 0,
                SpeedMTs = Int(m, "ConfiguredClockSpeed") is > 0 and var configured ? configured : Int(m, "Speed"),
                Manufacturer = Text(m, "Manufacturer"),
                PartNumber = Text(m, "PartNumber"),
                Slot = Text(m, "DeviceLocator"),
            })
            .Where(m => m.CapacityBytes > 0)
            .ToList();

        var vramByName = ReadVramFromRegistry(warnings);
        var gpus = Query("SELECT Name, AdapterCompatibility, AdapterRAM, DriverVersion, DriverDate, Status, PNPDeviceID FROM Win32_VideoController", warnings, cancellationToken)
            .Select(v =>
            {
                var name = Text(v, "Name") ?? "Adaptador de vídeo desconhecido";
                var adapterRam = Long(v, "AdapterRAM");
                long? vram = vramByName.TryGetValue(name, out var fromRegistry)
                    ? fromRegistry
                    : adapterRam is > 0 and < AdapterRamTruncationThreshold ? adapterRam : null;
                return new GpuInfo
                {
                    Name = name,
                    Vendor = Text(v, "AdapterCompatibility"),
                    DedicatedMemoryBytes = vram,
                    DriverVersion = Text(v, "DriverVersion"),
                    DriverDate = Date(v, "DriverDate"),
                    Status = Text(v, "Status"),
                    PnpDeviceId = Text(v, "PNPDeviceID"),
                };
            })
            .ToList();

        foreach (var warning in warnings)
        {
            _logger.LogWarning("Inventário de hardware incompleto: {Warning}", warning);
        }

        return new HardwareInventory
        {
            Computer = computer,
            Processors = processors,
            MemoryModules = modules,
            TotalPhysicalMemoryBytes = Long(system, "TotalPhysicalMemory"),
            Gpus = gpus,
            CapturedAt = _timeProvider.GetUtcNow(),
            Warnings = warnings,
        };
    }

    private static List<ManagementBaseObject> Query(string wql, List<string> warnings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var searcher = new ManagementObjectSearcher(new ManagementScope(@"\\.\root\cimv2"), new ObjectQuery(wql), QueryOptions);
            using var results = searcher.Get();
            return results.Cast<ManagementBaseObject>().ToList();
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            warnings.Add($"{ClassOf(wql)}: {ex.Message}");
            return [];
        }
    }

    private static ManagementBaseObject? QueryFirst(string wql, List<string> warnings, CancellationToken cancellationToken) =>
        Query(wql, warnings, cancellationToken).FirstOrDefault();

    /// <summary>VRAM real em HardwareInformation.qwMemorySize (QWORD) ou MemorySize (DWORD/binário), por nome do driver.</summary>
    private static Dictionary<string, long> ReadVramFromRegistry(List<string> warnings)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(DisplayClassKey);
            if (classKey is null)
            {
                return result;
            }

            foreach (var subName in classKey.GetSubKeyNames().Where(n => n.All(char.IsDigit)))
            {
                try
                {
                    using var device = classKey.OpenSubKey(subName);
                    if (device?.GetValue("DriverDesc") is not string description)
                    {
                        continue;
                    }

                    var size = device.GetValue("HardwareInformation.qwMemorySize") switch
                    {
                        long qword => qword,
                        byte[] { Length: 8 } bytes => BitConverter.ToInt64(bytes),
                        _ => device.GetValue("HardwareInformation.MemorySize") switch
                        {
                            int dword => (long)(uint)dword,
                            byte[] { Length: 4 } bytes => BitConverter.ToUInt32(bytes),
                            _ => 0L,
                        },
                    };

                    if (size > 0)
                    {
                        result[description] = size;
                    }
                }
                catch (System.Security.SecurityException)
                {
                    // Subchave protegida (ex.: "Properties"): ignorar.
                }
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            warnings.Add($"VRAM (registro): {ex.Message}");
        }

        return result;
    }

    private static string ClassOf(string wql)
    {
        var index = wql.IndexOf(" FROM ", StringComparison.OrdinalIgnoreCase);
        return index < 0 ? wql : wql[(index + 6)..].Trim();
    }

    private static string? Text(ManagementBaseObject? obj, string property)
    {
        var value = obj?[property]?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int? Int(ManagementBaseObject? obj, string property) =>
        obj?[property] is { } value && int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static long? Long(ManagementBaseObject? obj, string property) =>
        obj?[property] is { } value && long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static DateTimeOffset? Date(ManagementBaseObject? obj, string property)
    {
        var raw = Text(obj, property);
        if (raw is null)
        {
            return null;
        }

        try
        {
            return new DateTimeOffset(ManagementDateTimeConverter.ToDateTime(raw).ToUniversalTime(), TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string? Join(string? first, string? second) =>
        (first, second) switch
        {
            (null, null) => null,
            (null, _) => second,
            (_, null) => first,
            _ => $"{first} {second}",
        };
}
