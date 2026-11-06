using System.Text;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Reporting;
using Xunit;

namespace LeccorWindowsHealthCenter.UnitTests.Reporting;

public sealed class ReportExportersTests
{
    private static ScanReport CreateSampleReport()
    {
        var finding1 = new Finding
        {
            RuleId = "security.antivirus-disabled",
            ScannerId = "security",
            Category = FindingCategory.Windows,
            Title = "Proteção em tempo real desativada",
            Description = "O antivírus principal está desligado.",
            Severity = Severity.Critical,
            Confidence = Confidence.Confirmed,
            Evidence = [new Evidence { Source = EvidenceSource.Registry, Description = "productState = 0x60000" }],
            Impact = "O computador está vulnerável a ameaças.",
            Recommendation = "Reative a proteção no aplicativo de segurança.",
            RepairActionId = null,
            RepairRisk = null,
            RequiresElevation = false,
            RequiresRestart = false,
            SupportsRollback = false,
            ProbableCause = "Desativação manual"
        };

        var recommendation1 = new Finding
        {
            RuleId = "dev.wsl-vhdx-large",
            ScannerId = "devtools",
            Category = FindingCategory.Development,
            Title = "Disco VHDX do WSL2 acima de 50 GB",
            Description = "O arquivo ext4.vhdx da distribuição Ubuntu está ocupando 55 GB.",
            Severity = Severity.Info,
            Confidence = Confidence.Confirmed,
            Evidence = [new Evidence { Source = EvidenceSource.Measurement, Description = "Tamanho: 55 GB", Value = 55, Unit = "GB" }],
            Impact = "Espaço ocupado no disco C:.",
            Recommendation = "Execute wsl --manage Ubuntu --set-sparse true para compactar.",
            RepairActionId = null,
            RepairRisk = null,
            RequiresElevation = false,
            RequiresRestart = false,
            SupportsRollback = false
        };

        var score = new HealthScore
        {
            Overall = 78,
            Rating = HealthRating.Attention,
            Categories = new Dictionary<ScoreCategory, int>
            {
                [ScoreCategory.Windows] = 60,
                [ScoreCategory.Performance] = 95
            }
        };

        var result = new DiagnosticResult
        {
            ScannerId = "security",
            ScannerName = "Segurança do Windows",
            Module = ScanModule.Windows,
            Status = ScannerRunStatus.Completed,
            Duration = TimeSpan.FromMilliseconds(120),
            Findings = [finding1, recommendation1]
        };

        return new ScanReport
        {
            Mode = ScanMode.Full,
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-10),
            CompletedAt = DateTimeOffset.UtcNow,
            WasCancelled = false,
            Results = [result],
            Score = score,
            MachineName = "DESKTOP-TESTE"
        };
    }

    [Fact]
    public async Task JsonReportExporter_ExportsValidJson()
    {
        var exporter = new JsonReportExporter();
        var report = CreateSampleReport();
        using var ms = new MemoryStream();

        await exporter.ExportAsync(report, ms, CancellationToken.None);
        var json = Encoding.UTF8.GetString(ms.ToArray());

        Assert.Equal("JSON", exporter.Format);
        Assert.Equal(".json", exporter.FileExtension);
        Assert.Contains("DESKTOP-TESTE", json);
        Assert.Contains("security.antivirus-disabled", json);
        Assert.Contains("dev.wsl-vhdx-large", json);
    }

    [Fact]
    public async Task HtmlReportExporter_ExportsValidHtmlWithSections()
    {
        var exporter = new HtmlReportExporter();
        var report = CreateSampleReport();
        using var ms = new MemoryStream();

        await exporter.ExportAsync(report, ms, CancellationToken.None);
        var html = Encoding.UTF8.GetString(ms.ToArray());

        Assert.Equal("HTML", exporter.Format);
        Assert.Equal(".html", exporter.FileExtension);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("DESKTOP-TESTE", html);
        Assert.Contains("Laudo de", html);
        Assert.Contains("tempo real desativada", html);
        Assert.Contains("Disco VHDX do WSL2 acima de 50 GB", html);
    }

    [Fact]
    public async Task CsvReportExporter_ExportsUtf8WithBomAndHeaders()
    {
        var exporter = new CsvReportExporter();
        var report = CreateSampleReport();
        using var ms = new MemoryStream();

        await exporter.ExportAsync(report, ms, CancellationToken.None);
        var bytes = ms.ToArray();

        // Verifica BOM UTF-8 (EF BB BF)
        Assert.True(bytes.Length >= 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);

        var csv = Encoding.UTF8.GetString(bytes[3..]);
        Assert.Equal("CSV", exporter.Format);
        Assert.Equal(".csv", exporter.FileExtension);
        Assert.Contains("Tipo;Severidade;Categoria;Título;Descrição", csv);
        Assert.Contains("Proteção em tempo real desativada", csv);
        Assert.Contains("Disco VHDX do WSL2 acima de 50 GB", csv);
    }

    [Fact]
    public async Task PdfReportExporter_ExportsValidPdf14Document()
    {
        var exporter = new PdfReportExporter();
        var report = CreateSampleReport();
        using var ms = new MemoryStream();

        await exporter.ExportAsync(report, ms, CancellationToken.None);
        var bytes = ms.ToArray();
        var pdfText = Encoding.Latin1.GetString(bytes);

        Assert.Equal("PDF", exporter.Format);
        Assert.Equal(".pdf", exporter.FileExtension);
        Assert.StartsWith("%PDF-1.4", pdfText);
        Assert.Contains("%%EOF", pdfText);
        Assert.Contains("Laudo de Saude do Computador", pdfText);
    }
}
