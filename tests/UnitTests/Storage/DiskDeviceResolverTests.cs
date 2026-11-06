using LeccorWindowsHealthCenter.Infrastructure.Storage;
using Xunit;

namespace LeccorWindowsHealthCenter.UnitTests.Storage;

public sealed class DiskDeviceResolverTests
{
    [Theory]
    [InlineData(@"\Device\Harddisk3\DR3", 3)]
    [InlineData(@"\Device\Harddisk0", 0)]
    [InlineData("Disk 1", 1)]
    [InlineData("Disco 2", 2)]
    [InlineData("Disco3", 3)]
    [InlineData("DISK  4", 4)]
    public void ExtractDiskNumber_CorrectlyExtractsIndex(string input, int expected)
    {
        var result = DiskDeviceResolver.ExtractDiskNumber(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Armazenamento")]
    [InlineData("Driver de Vídeo")]
    public void ExtractDiskNumber_ReturnsNull_ForNonDiskInputs(string? input)
    {
        var result = DiskDeviceResolver.ExtractDiskNumber(input);
        Assert.Null(result);
    }

    [Fact]
    public void ResolveDisplay_FormatsWithLetters_WhenMappingConfigured()
    {
        DiskDeviceResolver.SetMappingsForTesting(new Dictionary<int, DiskMapping>
        {
            [3] = new(3, ["F:"], "USB3.0 high speed"),
            [0] = new(0, ["C:"], "KINGSTON SA400S37960G"),
            [1] = new(1, ["D:", "E:"], "NVME SSD 1TB"),
        });

        try
        {
            Assert.Equal("Disco 3 [F:]", DiskDeviceResolver.ResolveDisplay("Disco 3"));
            Assert.Equal("Disco 3 [F:]", DiskDeviceResolver.ResolveDisplay(@"\Device\Harddisk3\DR3"));
            Assert.Equal("Disco 0 [C:]", DiskDeviceResolver.ResolveDisplay("Disk 0"));
            Assert.Equal("Disco 1 [D:, E:]", DiskDeviceResolver.ResolveDisplay("Disco 1"));
        }
        finally
        {
            DiskDeviceResolver.ClearCache();
        }
    }

    [Fact]
    public void ResolveDisplay_PreservesAlreadyResolvedTargets()
    {
        var target = "Disco 3 [F:]";
        var result = DiskDeviceResolver.ResolveDisplay(target);
        Assert.Equal(target, result);
    }
}

