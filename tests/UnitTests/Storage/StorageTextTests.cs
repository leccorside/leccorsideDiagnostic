using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Infrastructure.SystemInfo;

namespace LeccorWindowsHealthCenter.UnitTests.Storage;

/// <summary>Códigos reais do WMI de armazenamento (valores vistos nesta máquina e na documentação do MSFT_PhysicalDisk).</summary>
public class StorageTextTests
{
    [Theory]
    [InlineData(3, DiskMediaKind.Hdd)]
    [InlineData(4, DiskMediaKind.Ssd)]
    [InlineData(5, DiskMediaKind.Scm)]
    [InlineData(0, DiskMediaKind.Unknown)] // USB: "não especificado" — o IOCTL de penalidade de busca decide
    [InlineData(null, DiskMediaKind.Unknown)]
    public void Media_type_codes(int? code, DiskMediaKind expected) => Assert.Equal(expected, StorageText.MediaOf(code));

    [Theory]
    [InlineData(17, DiskBusKind.Nvme)]
    [InlineData(11, DiskBusKind.Sata)]
    [InlineData(7, DiskBusKind.Usb)]
    [InlineData(8, DiskBusKind.Raid)]
    [InlineData(99, DiskBusKind.Other)]
    public void Bus_type_codes(int code, DiskBusKind expected) => Assert.Equal(expected, StorageText.BusOf(code));

    [Theory]
    [InlineData("{C12A7328-F81F-11D2-BA4B-00A0C93EC93B}", null, PartitionKind.Efi)] // GUID em maiúsculas também
    [InlineData("{e3c9e316-0b5c-4db8-817d-f92df00215ae}", null, PartitionKind.Msr)]
    [InlineData("{de94bba4-06d1-4d40-a16a-bfd50179d6ac}", null, PartitionKind.Recovery)]
    [InlineData("{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}", null, PartitionKind.Data)]
    [InlineData("", 7, PartitionKind.Data)] // MBR NTFS (o C: desta máquina)
    [InlineData(null, 39, PartitionKind.Recovery)]
    public void Partition_types(string? gpt, int? mbr, PartitionKind expected) => Assert.Equal(expected, StorageText.PartitionKindOf(gpt, mbr));

    [Theory]
    [InlineData(DiskMediaKind.Ssd, DiskBusKind.Nvme, "SSD NVMe")]
    [InlineData(DiskMediaKind.Ssd, DiskBusKind.Sata, "SSD SATA")]
    [InlineData(DiskMediaKind.Hdd, DiskBusKind.Usb, "HDD (USB)")]
    [InlineData(DiskMediaKind.Unknown, DiskBusKind.Nvme, "SSD NVMe")]
    [InlineData(DiskMediaKind.Unknown, DiskBusKind.Usb, "Tipo não identificado (USB)")]
    public void Kind_text(DiskMediaKind media, DiskBusKind bus, string expected) => Assert.Equal(expected, StorageText.Kind(media, bus));

    [Fact]
    public void Predicted_failure_wins_over_healthy_status()
    {
        var text = StorageText.HealthText(DiskHealth.Healthy, predictedFailure: true);

        Assert.Equal(StatusTone.Critical, text!.Value.Tone);
        Assert.Null(StorageText.HealthText(DiskHealth.Unknown, predictedFailure: false));
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(2, false)]
    [InlineData(3, false)] // Degradado (ex.: espelho sem uma cópia) não é falha prevista do dispositivo
    public void Failure_operational_codes(int code, bool failure) => Assert.Equal(failure, StorageText.IsFailureStatus(code));

    [Fact]
    public void Unknown_operational_code_is_shown_not_guessed() => Assert.Equal("Código 53261", StorageText.OperationalStatusText(53261));

    [Theory]
    [InlineData(43, 43.0)]
    [InlineData(0, null)]       // sentinela "não informado"
    [InlineData(-273, null)]
    [InlineData(short.MaxValue, null)]
    public void Device_temperature_ignores_firmware_sentinels(short raw, double? expected) =>
        Assert.Equal(expected, StorageDeviceQuery.Plausible(raw));
}
