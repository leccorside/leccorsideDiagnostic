using System.Management;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using static LeccorWindowsHealthCenter.Infrastructure.SystemInfo.WmiQuery;

namespace LeccorWindowsHealthCenter.Infrastructure.SystemInfo;

/// <summary>
/// Discos, partições e volumes via WMI de armazenamento (root\Microsoft\Windows\Storage) + IOCTL sem acesso a dados.
/// Sem administrador, o Windows nega SMART detalhado e contadores de confiabilidade (temperatura, desgaste, horas):
/// esses campos ficam null — nunca estimados (seção 14).
/// </summary>
public sealed class WmiStorageInventoryProvider : IStorageInventoryProvider, IDisposable
{
    private const string FileSystemKey = @"SYSTEM\CurrentControlSet\Control\FileSystem";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly IPrivilegeService _privileges;
    private readonly ILogger<WmiStorageInventoryProvider> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private StorageInventory? _cached;

    public WmiStorageInventoryProvider(IPrivilegeService privileges, ILogger<WmiStorageInventoryProvider> logger, TimeProvider timeProvider)
    {
        _privileges = privileges;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<StorageInventory> GetAsync(CancellationToken cancellationToken, bool forceRefresh = false)
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
            _logger.LogInformation("Inventário de armazenamento lido em {DurationMs} ms ({Disks} discos, {Volumes} volumes, {Warnings} avisos)",
                (long)_timeProvider.GetElapsedTime(started).TotalMilliseconds, _cached.Disks.Count, _cached.Volumes.Count, _cached.Warnings.Count);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private StorageInventory Collect(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var elevated = _privileges.IsElevated;

        var partitions = Query(StorageScope, "SELECT DiskNumber, PartitionNumber, DriveLetter, Size, GptType, MbrType, IsBoot, IsSystem FROM MSFT_Partition", warnings, cancellationToken)
            .Select(p => (Disk: Int(p, "DiskNumber"), Partition: new PartitionInfo
            {
                Number = Int(p, "PartitionNumber") ?? 0,
                DriveLetter = Letter(p),
                SizeBytes = Long(p, "Size") ?? 0,
                Kind = StorageText.PartitionKindOf(Text(p, "GptType"), Int(p, "MbrType")),
            }, IsBoot: Bool(p, "IsBoot") == true))
            .Where(p => p.Disk is not null)
            .ToList();

        var disksByNumber = Query(StorageScope, "SELECT Number, PartitionStyle, IsBoot, IsSystem FROM MSFT_Disk", warnings, cancellationToken)
            .Where(d => Int(d, "Number") is not null)
            .ToDictionary(d => Int(d, "Number")!.Value);

        var reliability = elevated ? ReadReliability(warnings, cancellationToken) : [];

        var disks = new List<PhysicalDiskInfo>();
        foreach (var d in Query(StorageScope, "SELECT DeviceId, FriendlyName, Model, MediaType, BusType, Size, HealthStatus, OperationalStatus, FirmwareVersion FROM MSFT_PhysicalDisk", warnings, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!int.TryParse(Text(d, "DeviceId"), out var number))
            {
                continue; // Disco de pool do Storage Spaces sem número próprio: aparece pelo disco virtual.
            }

            disksByNumber.TryGetValue(number, out var disk);
            var media = StorageText.MediaOf(Int(d, "MediaType"));
            if (media == DiskMediaKind.Unknown)
            {
                // WMI "Unspecified" (comum em USB): a penalidade de busca do próprio dispositivo diferencia SSD de HDD.
                media = StorageDeviceQuery.IncursSeekPenalty(number) switch
                {
                    true => DiskMediaKind.Hdd,
                    false => DiskMediaKind.Ssd,
                    null => DiskMediaKind.Unknown,
                };
            }

            var operational = OperationalCodes(d);
            var temperature = StorageDeviceQuery.Temperature(number);
            reliability.TryGetValue(Text(d, "DeviceId") ?? string.Empty, out var counters);
            var diskPartitions = partitions.Where(p => p.Disk == number).Select(p => p.Partition).OrderBy(p => p.Number).ToList();

            disks.Add(new PhysicalDiskInfo
            {
                Number = number,
                Name = Text(d, "FriendlyName") ?? Text(d, "Model") ?? $"Disco {number}",
                Firmware = Text(d, "FirmwareVersion"),
                Bus = StorageText.BusOf(Int(d, "BusType")),
                Media = media,
                SizeBytes = Long(d, "Size") is > 0 and var size ? size : null,
                Health = StorageText.HealthOf(Int(d, "HealthStatus")),
                OperationalStatus = operational.Select(StorageText.OperationalStatusText).Distinct().ToList(),
                PredictedFailure = operational.Any(StorageText.IsFailureStatus),
                PartitionStyle = StorageText.PartitionStyleOf(Int(disk, "PartitionStyle")),
                IsSystemDisk = Bool(disk, "IsBoot") == true || partitions.Any(p => p.Disk == number && p.IsBoot),
                TrimEnabled = StorageDeviceQuery.TrimEnabled(number),
                TemperatureCelsius = temperature.Current ?? counters?.Temperature,
                WarningTemperatureCelsius = temperature.Warning,
                CriticalTemperatureCelsius = temperature.Critical ?? counters?.TemperatureMax,
                WearPercent = counters?.Wear,
                PowerOnHours = counters?.PowerOnHours,
                Partitions = diskPartitions,
            });
        }

        var diskOfLetter = partitions.Where(p => p.Partition.DriveLetter is not null)
            .GroupBy(p => p.Partition.DriveLetter!.Value)
            .ToDictionary(g => g.Key, g => g.First().Disk);
        var volumes = ReadVolumes(diskOfLetter, warnings, cancellationToken);

        foreach (var warning in warnings)
        {
            _logger.LogWarning("Inventário de armazenamento incompleto: {Warning}", warning);
        }

        return new StorageInventory
        {
            Disks = disks.OrderBy(d => d.Number).ToList(),
            Volumes = volumes,
            TrimEnabledForNtfs = ReadNtfsTrim(warnings),
            ReadWithElevation = elevated,
            CapturedAt = _timeProvider.GetUtcNow(),
            Warnings = warnings,
        };
    }

    private static List<VolumeInfo> ReadVolumes(Dictionary<char, int?> diskOfLetter, List<string> warnings, CancellationToken cancellationToken)
    {
        var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? @"C:\";
        var fromWmi = Query(StorageScope, "SELECT DriveLetter, FileSystemLabel, FileSystem, Size, SizeRemaining, HealthStatus, DriveType FROM MSFT_Volume", warnings, cancellationToken)
            .Where(v => Letter(v) is not null && Long(v, "Size") is > 0)
            .Select(v =>
            {
                var letter = Letter(v)!.Value;
                var root = $@"{letter}:\";
                return new VolumeInfo
                {
                    Root = root,
                    Label = Text(v, "FileSystemLabel"),
                    FileSystem = Text(v, "FileSystem"),
                    SizeBytes = Long(v, "Size") ?? 0,
                    FreeBytes = Long(v, "SizeRemaining") ?? 0,
                    // IOCTL primeiro (rápido, sem WMI); a partição do WMI fica de reserva.
                    DiskNumber = StorageDeviceQuery.VolumeDiskNumber(letter) ?? (diskOfLetter.TryGetValue(letter, out var disk) ? disk : null),
                    IsSystem = string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase),
                    IsRemovable = Int(v, "DriveType") == 2,
                    Health = Int(v, "HealthStatus") switch { 0 => "Healthy", 1 => "Warning", 2 => "Unhealthy", _ => null },
                };
            })
            .ToList();

        if (fromWmi.Count > 0)
        {
            return fromWmi.OrderBy(v => v.Root, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Sem o WMI de armazenamento (serviço parado/corrompido): DriveInfo ainda dá espaço livre, sem relação com o disco físico.
        return DriveInfo.GetDrives()
            .Where(d => d.DriveType is DriveType.Fixed or DriveType.Removable && SafeIsReady(d))
            .Select(d => new VolumeInfo
            {
                Root = d.RootDirectory.FullName,
                DiskNumber = StorageDeviceQuery.VolumeDiskNumber(d.RootDirectory.FullName[0]),
                Label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? null : d.VolumeLabel,
                FileSystem = d.DriveFormat,
                SizeBytes = d.TotalSize,
                FreeBytes = d.TotalFreeSpace,
                IsSystem = string.Equals(d.RootDirectory.FullName, systemRoot, StringComparison.OrdinalIgnoreCase),
                IsRemovable = d.DriveType == DriveType.Removable,
            })
            .ToList();
    }

    private static bool SafeIsReady(DriveInfo drive)
    {
        try
        {
            return drive.IsReady;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private sealed record Reliability(double? Temperature, double? TemperatureMax, int? Wear, long? PowerOnHours);

    /// <summary>Só com administrador (sem ele o Windows responde "acesso negado" — verificado neste projeto).</summary>
    private static Dictionary<string, Reliability> ReadReliability(List<string> warnings, CancellationToken cancellationToken) =>
        Query(StorageScope, "SELECT DeviceId, Temperature, TemperatureMax, Wear, PowerOnHours FROM MSFT_StorageReliabilityCounter", warnings, cancellationToken)
            .Where(r => Text(r, "DeviceId") is not null)
            .GroupBy(r => Text(r, "DeviceId")!)
            .ToDictionary(g => g.Key, g =>
            {
                var r = g.First();
                return new Reliability(
                    Int(r, "Temperature") is > 0 and < 150 and var t ? t : null,
                    Int(r, "TemperatureMax") is > 0 and < 150 and var max ? max : null,
                    Int(r, "Wear") is >= 0 and <= 100 and var wear ? wear : null,
                    Long(r, "PowerOnHours") is > 0 and var hours ? hours : null);
            });

    /// <summary>DisableDeleteNotification = 0 → TRIM ativo para NTFS (mesmo valor que "fsutil behavior query DisableDeleteNotify" mostra).</summary>
    private static bool? ReadNtfsTrim(List<string> warnings)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(FileSystemKey);
            return key?.GetValue("DisableDeleteNotification") is int value ? value == 0 : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            warnings.Add($"TRIM (registro): {ex.Message}");
            return null;
        }
    }

    private static List<int> OperationalCodes(ManagementBaseObject disk) =>
        disk["OperationalStatus"] is ushort[] codes ? codes.Select(c => (int)c).ToList() : [];

    private static char? Letter(ManagementBaseObject obj) =>
        obj["DriveLetter"] switch
        {
            char c when char.IsLetter(c) => char.ToUpperInvariant(c),
            ushort u when char.IsLetter((char)u) => char.ToUpperInvariant((char)u),
            _ => null,
        };
}
