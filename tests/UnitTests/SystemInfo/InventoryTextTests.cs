using System.Globalization;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Shared.Formatting;

namespace LeccorWindowsHealthCenter.UnitTests.SystemInfo;

/// <summary>Regressões do break-ui (18/10/2026): valores reais que o firmware/WMI devolvem.</summary>
public class InventoryTextTests
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    [Theory]
    [InlineData("To Be Filled By O.E.M.")]
    [InlineData("System manufacturer")]
    [InlineData("System Product Name")]
    [InlineData("Default string")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Firmware_placeholders_are_not_data(string? raw) => Assert.Null(InventoryText.Clean(raw));

    [Fact]
    public void Real_values_are_kept_and_trimmed() =>
        Assert.Equal("MAXSUN", InventoryText.Clean("  MAXSUN  "));

    [Theory]
    [InlineData("00AD063200AD", "SK hynix")]
    [InlineData("80CE000080CE", "Samsung")]
    [InlineData("802C0000802C", "Micron")]
    [InlineData("0198", "Kingston")]
    [InlineData("Kingston", "Kingston")]
    [InlineData("Samsung", "Samsung")]
    public void Jedec_codes_become_vendor_names(string raw, string expected) =>
        Assert.Equal(expected, InventoryText.MemoryVendor(raw));

    [Fact]
    public void Unknown_jedec_code_is_flagged_not_shown_raw() =>
        Assert.Equal("não identificado (código JEDEC 7F7F7F7F)", InventoryText.MemoryVendor("7f7f7f7f"));

    [Theory]
    [InlineData("OK", "Funcionando", StatusTone.Normal)]
    [InlineData("Degraded", "Degradado", StatusTone.Warning)]
    [InlineData("Error", "Com erro", StatusTone.Critical)]
    [InlineData("Pred Fail", "Falha prevista", StatusTone.Critical)]
    public void Device_status_is_translated_with_tone(string raw, string text, StatusTone tone)
    {
        var status = InventoryText.DeviceStatus(raw);

        Assert.Equal(text, status!.Value.Text);
        Assert.Equal(tone, status.Value.Tone);
    }

    [Fact]
    public void Missing_status_is_null() => Assert.Null(InventoryText.DeviceStatus(null));

    [Theory]
    [InlineData(1_099_201_822_310L, 1_099_511_627_776L, "1.023,7 GB", "1.024 GB")] // 1 TB de RAM quase toda em uso
    [InlineData(1_063_256_064L, 1_073_741_824L, "0,99 GB", "1 GB")] // a partir de 1.000 a unidade sobe
    [InlineData(17_394_536_448L, 34_252_783_616L, "16,2 GB", "31,9 GB")]
    [InlineData(2_198_915_544_064L, 2_199_023_255_552L, "2.047,9 GB", "2.048 GB")] // ambos em TB: "2 TB de 2 TB" esconderia a diferença
    [InlineData(0L, 408_021_893_120L, "0 GB", "380 GB")] // break-ui: volume cheio virava "0 B livres de 408.021.893.120 B"
    [InlineData(2_147_483_136L, 2_147_483_648L, "2 GB", "2 GB")] // break-ui: 512 B de diferença virava "2.097.151,5 KB de 2.097.152 KB"
    public void Byte_pairs_share_the_unit_of_the_smaller_value(long part, long total, string expectedPart, string expectedTotal)
    {
        var (partText, totalText) = ByteSize.FormatPair(part, total, PtBr);

        Assert.Equal(expectedPart, partText);
        Assert.Equal(expectedTotal, totalText);
    }
}
