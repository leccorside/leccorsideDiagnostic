using System.Globalization;
using System.Text;
using LeccorWindowsHealthCenter.Application.Reporting;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Reporting;

/// <summary>
/// Exporta achados e métricas da varredura em formato CSV com codificação UTF-8 com BOM para Excel (seção 54).
/// </summary>
public sealed class CsvReportExporter : IReportExporter
{
    public string Format => "CSV";

    public string FileExtension => ".csv";

    public async Task ExportAsync(ScanReport report, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        var csv = GenerateCsv(report);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    public static string GenerateCsv(ScanReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();

        // Cabeçalho da tabela de Achados
        sb.AppendLine("Tipo;Severidade;Categoria;Título;Descrição;Impacto;Recomendação;Causa Provável;Confiança;Regra;Módulo;Ação de Reparo;Risco do Reparo;Requer Elevação;Requer Reinício;Suporta Rollback;Data");

        foreach (var f in report.Findings)
        {
            var tipo = f.IsRecommendation ? "Recomendação" : "Problema";
            var row = new[]
            {
                tipo,
                f.Severity.ToString(),
                f.Category.ToString(),
                EscapeCsv(f.Title),
                EscapeCsv(f.Description),
                EscapeCsv(f.Impact),
                EscapeCsv(f.Recommendation),
                EscapeCsv(f.ProbableCause ?? string.Empty),
                $"{f.Confidence.Percent}%",
                f.RuleId,
                f.ScannerId,
                f.RepairActionId ?? "Nenhum",
                f.RepairRisk?.ToString() ?? "N/A",
                f.RequiresElevation ? "Sim" : "Não",
                f.RequiresRestart ? "Sim" : "Não",
                f.SupportsRollback ? "Sim" : "Não",
                f.DetectedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            };

            sb.AppendLine(string.Join(";", row));
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        if (normalized.Contains(';') || normalized.Contains('"') || normalized.Contains(','))
        {
            return $"\"{normalized.Replace("\"", "\"\"")}\"";
        }

        return normalized;
    }
}
