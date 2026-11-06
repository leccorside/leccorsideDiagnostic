using System.Text.Json;
using LeccorWindowsHealthCenter.Application.Storage;
using LeccorWindowsHealthCenter.Reporting;
using Xunit;

namespace LeccorWindowsHealthCenter.UnitTests.Reporting;

public sealed class SpaceAnalysisExporterTests
{
    private static SpaceAnalysis CreateSampleAnalysis()
    {
        var capturedAt = new DateTimeOffset(2026, 11, 6, 14, 30, 0, TimeSpan.Zero);
        var subFolder = new FolderNode
        {
            Path = @"C:\Windows\WinSxS",
            Name = "WinSxS",
            SizeBytes = 8L * 1024 * 1024 * 1024,
            FileCount = 50000,
            FilesBytes = 2L * 1024 * 1024 * 1024,
            HardLinkDeduplicatedBytes = 3L * 1024 * 1024 * 1024, // 3 GB deduplicados em hard links
            Children = [],
        };

        var rootNode = new FolderNode
        {
            Path = @"C:\",
            Name = @"C:\",
            SizeBytes = 100L * 1024 * 1024 * 1024,
            FileCount = 250000,
            FilesBytes = 10L * 1024 * 1024 * 1024,
            HardLinkDeduplicatedBytes = 3L * 1024 * 1024 * 1024,
            Children = [subFolder],
        };

        var largestFiles = new List<FileEntry>
        {
            new(@"C:\pagefile.sys", 16L * 1024 * 1024 * 1024, capturedAt),
            new(@"C:\hiberfil.sys", 12L * 1024 * 1024 * 1024, capturedAt),
        };

        var oldFiles = new List<FileEntry>
        {
            new(@"C:\old_setup.exe", 500L * 1024 * 1024, capturedAt.AddYears(-3)),
        };

        return new SpaceAnalysis
        {
            Root = rootNode,
            LargestFiles = largestFiles,
            OldFiles = oldFiles,
            Duration = TimeSpan.FromSeconds(12.4),
            CapturedAt = capturedAt,
        };
    }

    [Fact]
    public void ExportToCsv_StartsWithUtf8Bom_UsesSemicolonDelimiter_AndContainsExpectedSections()
    {
        var exporter = new SpaceAnalysisExporter();
        var analysis = CreateSampleAnalysis();

        var csv = exporter.ExportToCsv(analysis);

        Assert.NotNull(csv);
        Assert.NotEmpty(csv);
        // Deve começar com UTF-8 BOM (\uFEFF) para Excel abrir perfeitamente sem problemas de acentuação
        Assert.StartsWith("\uFEFF", csv);

        // Deve conter delimitador ponto-e-vírgula compatível com Excel pt-BR
        Assert.Contains(";", csv);

        // Deve conter as seções estruturadas
        Assert.Contains("Tipo;Caminho;Nome;Tamanho_Bytes;Tamanho_Formatado;Total_Arquivos;Inacessivel;HardLinks_Deduplicados_Bytes", csv);
        Assert.Contains("# MAIORES ARQUIVOS DETECTADOS", csv);
        Assert.Contains("# ARQUIVOS GRANDES ANTIGOS", csv);

        // Deve conter colunas e caminhos
        Assert.Contains(@"C:\Windows\WinSxS", csv);
        Assert.Contains(@"C:\pagefile.sys", csv);
        Assert.Contains(@"C:\old_setup.exe", csv);
        Assert.Contains("HardLinks_Deduplicados_Bytes", csv);
    }

    [Fact]
    public void ExportToJson_ProducesValidIndentedJson_AndDeserializesEqually()
    {
        var exporter = new SpaceAnalysisExporter();
        var analysis = CreateSampleAnalysis();

        var json = exporter.ExportToJson(analysis);

        Assert.NotNull(json);
        Assert.NotEmpty(json);
        Assert.Contains("\n  ", json); // Indentado

        var deserialized = JsonSerializer.Deserialize<SpaceAnalysis>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(analysis.Root.FileCount, deserialized.Root.FileCount);
        Assert.Equal(analysis.Root.SizeBytes, deserialized.Root.SizeBytes);
        Assert.Equal(analysis.Duration, deserialized.Duration);
        Assert.Equal(analysis.Root.Path, deserialized.Root.Path);
        Assert.Equal(analysis.Root.HardLinkDeduplicatedBytes, deserialized.Root.HardLinkDeduplicatedBytes);
        Assert.Equal(2, deserialized.LargestFiles.Count);
        Assert.Single(deserialized.OldFiles);
        Assert.Equal(analysis.OldFiles[0].Path, deserialized.OldFiles[0].Path);
    }
}
