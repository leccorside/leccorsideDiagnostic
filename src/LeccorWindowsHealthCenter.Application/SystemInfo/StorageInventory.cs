namespace LeccorWindowsHealthCenter.Application.SystemInfo;

/// <summary>
/// Discos físicos, partições e volumes (seção 14). Campos null significam "o dispositivo/Windows não informou" —
/// a UI mostra "não disponível" e nenhuma regra conclui nada a partir deles (seção 14: nunca inventar saúde).
/// </summary>
public sealed record StorageInventory
{
    public IReadOnlyList<PhysicalDiskInfo> Disks { get; init; } = [];

    public IReadOnlyList<VolumeInfo> Volumes { get; init; } = [];

    /// <summary>
    /// TRIM do NTFS no sistema (registro FileSystem\DisableDeleteNotification = 0). Null quando o valor não existe.
    /// Vale para todos os SSDs; o dispositivo também informa o próprio estado em <see cref="PhysicalDiskInfo.TrimEnabled"/>.
    /// </summary>
    public bool? TrimEnabledForNtfs { get; init; }

    /// <summary>
    /// Se o processo roda como administrador. Sem admin, desgaste, horas ligadas e SMART detalhado ficam indisponíveis
    /// (o Windows nega o acesso) — a UI explica isso em vez de mostrar "saúde 100%".
    /// </summary>
    public bool ReadWithElevation { get; init; }

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Fontes que falharam (ex.: "MSFT_StorageReliabilityCounter: acesso negado"). Exibidas no modo avançado.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public enum DiskMediaKind
{
    Unknown,
    Hdd,
    Ssd,

    /// <summary>Storage Class Memory (ex.: Optane em modo memória persistente).</summary>
    Scm,
}

public enum DiskBusKind
{
    Unknown,
    Sata,
    Nvme,
    Usb,
    Sas,
    Scsi,
    Ata,
    Raid,
    Virtual,
    SdCard,
    StorageSpaces,
    Other,
}

/// <summary>HealthStatus do MSFT_PhysicalDisk: o próprio Windows resume o SMART/NVMe do dispositivo nesse campo.</summary>
public enum DiskHealth
{
    Unknown,
    Healthy,
    Warning,
    Unhealthy,
}

public enum PartitionKind
{
    Data,
    Efi,
    Msr,
    Recovery,
    Other,
}

public sealed record PhysicalDiskInfo
{
    /// <summary>Número do disco no Windows (\\.\PhysicalDriveN).</summary>
    public required int Number { get; init; }

    public required string Name { get; init; }

    public string? Firmware { get; init; }

    public DiskBusKind Bus { get; init; }

    public DiskMediaKind Media { get; init; }

    public long? SizeBytes { get; init; }

    public DiskHealth Health { get; init; }

    /// <summary>OperationalStatus traduzido (ex.: "OK", "Falha prevista"). Vários valores podem coexistir.</summary>
    public IReadOnlyList<string> OperationalStatus { get; init; } = [];

    /// <summary>Verdadeiro quando algum OperationalStatus indica falha prevista/erro (SMART disparou).</summary>
    public bool PredictedFailure { get; init; }

    /// <summary>"GPT", "MBR" ou null (disco não inicializado/não informado).</summary>
    public string? PartitionStyle { get; init; }

    public bool IsSystemDisk { get; init; }

    /// <summary>TRIM informado pelo dispositivo (IOCTL StorageDeviceTrimProperty). Null quando não suportado (ex.: USB).</summary>
    public bool? TrimEnabled { get; init; }

    /// <summary>Temperatura atual informada pelo dispositivo. Null quando ele não fornece (comum em SATA e USB sem admin).</summary>
    public double? TemperatureCelsius { get; init; }

    /// <summary>Limite de aviso informado pelo próprio dispositivo (null quando não informa).</summary>
    public double? WarningTemperatureCelsius { get; init; }

    public double? CriticalTemperatureCelsius { get; init; }

    /// <summary>Desgaste (% da vida útil usada). Só com administrador; null quando não informado — nunca estimado.</summary>
    public int? WearPercent { get; init; }

    public long? PowerOnHours { get; init; }

    public IReadOnlyList<PartitionInfo> Partitions { get; init; } = [];

    public bool IsSolidState => Media is DiskMediaKind.Ssd or DiskMediaKind.Scm || Bus == DiskBusKind.Nvme;
}

public sealed record PartitionInfo
{
    public required int Number { get; init; }

    public char? DriveLetter { get; init; }

    public long SizeBytes { get; init; }

    public PartitionKind Kind { get; init; }
}

public sealed record VolumeInfo
{
    /// <summary>Raiz do volume, ex.: "C:\".</summary>
    public required string Root { get; init; }

    public string? Label { get; init; }

    public string? FileSystem { get; init; }

    public long SizeBytes { get; init; }

    public long FreeBytes { get; init; }

    /// <summary>Disco físico onde o volume está (null quando não foi possível relacionar, ex.: volume de rede).</summary>
    public int? DiskNumber { get; init; }

    /// <summary>Volume onde o Windows está instalado.</summary>
    public bool IsSystem { get; init; }

    /// <summary>Fixo (interno) ou removível (pen drive, cartão). Discos USB externos costumam aparecer como fixos.</summary>
    public bool IsRemovable { get; init; }

    /// <summary>HealthStatus do MSFT_Volume ("Healthy", "Warning", "Unhealthy") — Warning costuma significar "precisa de verificação".</summary>
    public string? Health { get; init; }

    public long UsedBytes => SizeBytes - FreeBytes;

    public double FreePercent => SizeBytes <= 0 ? 0 : FreeBytes * 100.0 / SizeBytes;
}

public interface IStorageInventoryProvider
{
    /// <summary>
    /// Inventário atual. Reaproveita a última leitura por até 30 s (o espaço livre muda, mas não a cada segundo);
    /// <paramref name="forceRefresh"/> ignora o cache.
    /// </summary>
    Task<StorageInventory> GetAsync(CancellationToken cancellationToken, bool forceRefresh = false);
}
