namespace LeccorWindowsHealthCenter.Application.SystemInfo;

/// <summary>Traduz os códigos do WMI de armazenamento (MSFT_PhysicalDisk, MSFT_Partition, MSFT_Disk) em tipos e textos.</summary>
public static class StorageText
{
    // GPT: GUIDs de tipo de partição definidos pela especificação UEFI e pela Microsoft.
    private const string GptEfi = "{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}";
    private const string GptMsr = "{e3c9e316-0b5c-4db8-817d-f92df00215ae}";
    private const string GptRecovery = "{de94bba4-06d1-4d40-a16a-bfd50179d6ac}";
    private const string GptBasicData = "{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}";

    /// <summary>MSFT_PhysicalDisk.MediaType: 3 HDD, 4 SSD, 5 SCM; 0 = não especificado.</summary>
    public static DiskMediaKind MediaOf(int? mediaType) => mediaType switch
    {
        3 => DiskMediaKind.Hdd,
        4 => DiskMediaKind.Ssd,
        5 => DiskMediaKind.Scm,
        _ => DiskMediaKind.Unknown,
    };

    /// <summary>MSFT_PhysicalDisk.BusType (STORAGE_BUS_TYPE).</summary>
    public static DiskBusKind BusOf(int? busType) => busType switch
    {
        null or 0 => DiskBusKind.Unknown,
        1 => DiskBusKind.Scsi,
        2 or 3 => DiskBusKind.Ata,
        7 => DiskBusKind.Usb,
        8 => DiskBusKind.Raid,
        10 => DiskBusKind.Sas,
        11 => DiskBusKind.Sata,
        12 or 13 => DiskBusKind.SdCard,
        14 or 15 => DiskBusKind.Virtual,
        16 => DiskBusKind.StorageSpaces,
        17 => DiskBusKind.Nvme,
        _ => DiskBusKind.Other,
    };

    /// <summary>MSFT_PhysicalDisk.HealthStatus: 0 Healthy, 1 Warning, 2 Unhealthy, 5 Unknown.</summary>
    public static DiskHealth HealthOf(int? healthStatus) => healthStatus switch
    {
        0 => DiskHealth.Healthy,
        1 => DiskHealth.Warning,
        2 => DiskHealth.Unhealthy,
        _ => DiskHealth.Unknown,
    };

    /// <summary>MSFT_Disk.PartitionStyle: 1 MBR, 2 GPT; 0 = não inicializado.</summary>
    public static string? PartitionStyleOf(int? style) => style switch
    {
        1 => "MBR",
        2 => "GPT",
        _ => null,
    };

    public static PartitionKind PartitionKindOf(string? gptType, int? mbrType)
    {
        if (!string.IsNullOrWhiteSpace(gptType))
        {
            return gptType.ToLowerInvariant() switch
            {
                GptEfi => PartitionKind.Efi,
                GptMsr => PartitionKind.Msr,
                GptRecovery => PartitionKind.Recovery,
                GptBasicData => PartitionKind.Data,
                _ => PartitionKind.Other,
            };
        }

        // MBR: 7 = NTFS/exFAT, 6/14 = FAT16, 11/12 = FAT32, 39 (0x27) = recuperação do Windows, 239 (0xEF) = EFI.
        return mbrType switch
        {
            7 or 6 or 14 or 11 or 12 => PartitionKind.Data,
            39 => PartitionKind.Recovery,
            239 => PartitionKind.Efi,
            null => PartitionKind.Data,
            _ => PartitionKind.Other,
        };
    }

    /// <summary>OperationalStatus (CIM): os valores que indicam falha prevista ou erro do dispositivo.</summary>
    public static bool IsFailureStatus(int code) => code is 5 or 6 or 7 or 12 or 13 or 16;

    /// <summary>Valores padrão do CIM; extensões específicas do fornecedor aparecem como "Código N" em vez de um palpite.</summary>
    public static string OperationalStatusText(int code) => code switch
    {
        0 => "Desconhecido",
        1 => "Outro",
        2 => "OK",
        3 => "Degradado",
        4 => "Sob estresse",
        5 => "Falha prevista",
        6 => "Erro",
        7 => "Erro não recuperável",
        8 => "Iniciando",
        9 => "Parando",
        10 => "Parado",
        11 => "Em manutenção",
        12 => "Sem contato",
        13 => "Comunicação perdida",
        14 => "Abortado",
        15 => "Inativo",
        16 => "Componente com erro",
        17 => "Concluído",
        _ => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Código {code}"),
    };

    /// <summary>"SSD NVMe", "HDD SATA", "SSD (USB)", "Tipo não identificado (USB)".</summary>
    public static string Kind(DiskMediaKind media, DiskBusKind bus)
    {
        var mediaText = media switch
        {
            DiskMediaKind.Hdd => "HDD",
            DiskMediaKind.Ssd => "SSD",
            DiskMediaKind.Scm => "Memória persistente (SCM)",
            _ => null,
        };

        return (mediaText, bus) switch
        {
            (null, DiskBusKind.Nvme) => "SSD NVMe",
            (null, DiskBusKind.Unknown) => "Tipo não identificado",
            (null, _) => $"Tipo não identificado ({BusText(bus)})",
            (_, DiskBusKind.Nvme or DiskBusKind.Sata or DiskBusKind.Sas) => $"{mediaText} {BusText(bus)}",
            (_, DiskBusKind.Unknown) => mediaText,
            _ => $"{mediaText} ({BusText(bus)})",
        };
    }

    public static string BusText(DiskBusKind bus) => bus switch
    {
        DiskBusKind.Sata => "SATA",
        DiskBusKind.Nvme => "NVMe",
        DiskBusKind.Usb => "USB",
        DiskBusKind.Sas => "SAS",
        DiskBusKind.Scsi => "SCSI",
        DiskBusKind.Ata => "ATA",
        DiskBusKind.Raid => "RAID",
        DiskBusKind.Virtual => "virtual",
        DiskBusKind.SdCard => "cartão SD",
        DiskBusKind.StorageSpaces => "Espaços de Armazenamento",
        DiskBusKind.Other => "outro barramento",
        _ => "barramento não informado",
    };

    /// <summary>Saúde como o Windows resume (não é percentual de vida útil).</summary>
    public static (string Text, StatusTone Tone)? HealthText(DiskHealth health, bool predictedFailure) =>
        (health, predictedFailure) switch
        {
            (_, true) => ("Falha prevista pelo próprio disco", StatusTone.Critical),
            (DiskHealth.Unhealthy, _) => ("Com problema", StatusTone.Critical),
            (DiskHealth.Warning, _) => ("Atenção", StatusTone.Warning),
            (DiskHealth.Healthy, _) => ("Sem alertas", StatusTone.Normal),
            _ => null,
        };

    public static string PartitionText(PartitionKind kind) => kind switch
    {
        PartitionKind.Efi => "EFI",
        PartitionKind.Msr => "Reservada da Microsoft",
        PartitionKind.Recovery => "Recuperação",
        PartitionKind.Data => "Dados",
        _ => "Outra",
    };
}
