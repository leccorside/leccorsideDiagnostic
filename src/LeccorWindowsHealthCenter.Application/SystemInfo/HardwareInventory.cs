namespace LeccorWindowsHealthCenter.Application.SystemInfo;

/// <summary>
/// Inventário de hardware. Campos null significam "a fonte não informou" — a UI mostra
/// "não disponível" e nenhuma regra conclui nada a partir deles.
/// </summary>
public sealed record HardwareInventory
{
    /// <summary>Tipo da observação que carrega o inventário serializado (atributo "json"); persistida em HardwareSnapshot.</summary>
    public const string ObservationKind = "hardware.inventory";

    public required ComputerInfo Computer { get; init; }

    public IReadOnlyList<ProcessorInfo> Processors { get; init; } = [];

    public IReadOnlyList<MemoryModuleInfo> MemoryModules { get; init; } = [];

    public long? TotalPhysicalMemoryBytes { get; init; }

    public IReadOnlyList<GpuInfo> Gpus { get; init; } = [];

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Fontes que falharam (ex.: "Win32_PhysicalMemory: acesso negado"). Exibidas no modo avançado.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record ComputerInfo
{
    public string? Manufacturer { get; init; }

    public string? Model { get; init; }

    public string? OperatingSystem { get; init; }

    public string? OsVersion { get; init; }

    public string? OsBuild { get; init; }

    public string? OsArchitecture { get; init; }

    public DateTimeOffset? LastBoot { get; init; }

    public string? BiosVendor { get; init; }

    public string? BiosVersion { get; init; }

    public DateTimeOffset? BiosDate { get; init; }

    public string? Motherboard { get; init; }
}

public sealed record ProcessorInfo
{
    public required string Name { get; init; }

    public string? Manufacturer { get; init; }

    public int? Cores { get; init; }

    public int? LogicalProcessors { get; init; }

    public int? MaxClockMHz { get; init; }

    public int? L2CacheKb { get; init; }

    public int? L3CacheKb { get; init; }

    public bool? VirtualizationFirmwareEnabled { get; init; }
}

public sealed record MemoryModuleInfo
{
    public long CapacityBytes { get; init; }

    public int? SpeedMTs { get; init; }

    public string? Manufacturer { get; init; }

    public string? PartNumber { get; init; }

    public string? Slot { get; init; }
}

public sealed record GpuInfo
{
    public required string Name { get; init; }

    /// <summary>Fabricante (AdapterCompatibility), ex.: "NVIDIA", "Advanced Micro Devices, Inc.", "Intel Corporation".</summary>
    public string? Vendor { get; init; }

    /// <summary>VRAM dedicada. Lida do registro (qwMemorySize) porque o WMI trunca em 4 GB; null quando não confiável.</summary>
    public long? DedicatedMemoryBytes { get; init; }

    public string? DriverVersion { get; init; }

    public DateTimeOffset? DriverDate { get; init; }

    public string? Status { get; init; }

    public string? PnpDeviceId { get; init; }
}

public interface IHardwareInventoryProvider
{
    /// <summary>
    /// Inventário atual. Reaproveita a última leitura por até 60 s (hardware raramente muda e o WMI é lento);
    /// <paramref name="forceRefresh"/> ignora o cache.
    /// </summary>
    Task<HardwareInventory> GetAsync(CancellationToken cancellationToken, bool forceRefresh = false);
}
