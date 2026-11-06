using System.Globalization;
using System.Net;
using System.Text;
using LeccorWindowsHealthCenter.Application.Reporting;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Reporting;

/// <summary>
/// Exporta o relatório consolidado de saúde em formato HTML autocontido com visual profissional (seções 54 e 55).
/// </summary>
public sealed class HtmlReportExporter : IReportExporter
{
    public string Format => "HTML";

    public string FileExtension => ".html";

    public async Task ExportAsync(ScanReport report, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        var html = GenerateHtml(report);
        var bytes = Encoding.UTF8.GetBytes(html);
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    public static string GenerateHtml(ScanReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        var scoreValue = report.Score.Overall?.ToString(CultureInfo.InvariantCulture) ?? "—";
        var ratingText = GetRatingText(report.Score.Rating);
        var ratingColor = GetRatingColor(report.Score.Rating);

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"pt-BR\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.Append("  <title>Laudo de Saúde do Computador — ").Append(WebUtility.HtmlEncode(report.MachineName)).AppendLine("</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("""
            :root {
              --bg: #0F172A;
              --card: #1E293B;
              --card-border: #334155;
              --text: #F8FAFC;
              --text-muted: #94A3B8;
              --accent: #2563EB;
              --critical: #EF4444;
              --high: #F97316;
              --medium: #EAB308;
              --low: #3B82F6;
              --info: #06B6D4;
              --success: #10B981;
            }
            @media (prefers-color-scheme: light) {
              :root {
                --bg: #F8FAFC;
                --card: #FFFFFF;
                --card-border: #E2E8F0;
                --text: #0F172A;
                --text-muted: #64748B;
              }
            }
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body {
              font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, sans-serif;
              background-color: var(--bg);
              color: var(--text);
              line-height: 1.5;
              padding: 24px;
            }
            .container { max-width: 1040px; margin: 0 auto; }
            header {
              display: flex;
              justify-content: space-between;
              align-items: center;
              padding-bottom: 24px;
              border-bottom: 1px solid var(--card-border);
              margin-bottom: 24px;
            }
            .brand h1 { font-size: 24px; font-weight: 700; }
            .brand p { color: var(--text-muted); font-size: 14px; margin-top: 4px; }
            .badge-rating {
              padding: 8px 16px;
              border-radius: 9999px;
              font-weight: 600;
              font-size: 14px;
              display: inline-flex;
              align-items: center;
              gap: 8px;
            }
            .score-circle {
              font-size: 28px;
              font-weight: 800;
            }
            .grid-summary {
              display: grid;
              grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
              gap: 16px;
              margin-bottom: 24px;
            }
            .summary-card {
              background: var(--card);
              border: 1px solid var(--card-border);
              border-radius: 8px;
              padding: 16px;
            }
            .summary-card .label { font-size: 13px; color: var(--text-muted); text-transform: uppercase; font-weight: 600; }
            .summary-card .val { font-size: 24px; font-weight: 700; margin-top: 4px; }
            section {
              background: var(--card);
              border: 1px solid var(--card-border);
              border-radius: 8px;
              padding: 24px;
              margin-bottom: 24px;
            }
            section h2 {
              font-size: 18px;
              font-weight: 700;
              margin-bottom: 16px;
              display: flex;
              align-items: center;
              gap: 8px;
            }
            .finding-card {
              background: rgba(0, 0, 0, 0.05);
              border: 1px solid var(--card-border);
              border-left-width: 4px;
              border-radius: 6px;
              padding: 16px;
              margin-bottom: 16px;
            }
            .finding-card:last-child { margin-bottom: 0; }
            .finding-header {
              display: flex;
              justify-content: space-between;
              align-items: center;
              margin-bottom: 8px;
            }
            .finding-title { font-weight: 600; font-size: 16px; }
            .severity-badge {
              font-size: 12px;
              font-weight: 700;
              padding: 2px 8px;
              border-radius: 4px;
              text-transform: uppercase;
              color: #FFF;
            }
            .sev-critical { background: var(--critical); }
            .sev-high { background: var(--high); }
            .sev-medium { background: var(--medium); color: #000; }
            .sev-low { background: var(--low); }
            .sev-info { background: var(--info); }
            .field-row { margin-top: 6px; font-size: 14px; }
            .field-label { font-weight: 600; color: var(--text-muted); }
            .evidence-list {
              margin: 8px 0 8px 16px;
              font-size: 13px;
              color: var(--text);
            }
            .evidence-list li { margin-bottom: 4px; }
            table {
              width: 100%;
              border-collapse: collapse;
              font-size: 14px;
            }
            th, td {
              text-align: left;
              padding: 8px 12px;
              border-bottom: 1px solid var(--card-border);
            }
            th { font-weight: 600; color: var(--text-muted); }
            footer {
              text-align: center;
              color: var(--text-muted);
              font-size: 12px;
              margin-top: 32px;
              padding-top: 16px;
              border-top: 1px solid var(--card-border);
            }
            @media print {
              body { background: #FFF !important; color: #000 !important; padding: 0; }
              .summary-card, section, .finding-card { border-color: #CBD5E1 !important; background: #FFF !important; }
            }
            """);
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"container\">");

        // Header
        sb.AppendLine("  <header>");
        sb.AppendLine("    <div class=\"brand\">");
        sb.AppendLine("      <h1>Laudo de Saúde do Computador</h1>");
        sb.Append("      <p>Computador: <strong>").Append(WebUtility.HtmlEncode(report.MachineName)).Append("</strong> &bull; Gerado em: ")
          .Append(report.CompletedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture)).AppendLine("</p>");
        sb.AppendLine("    </div>");
        sb.Append("    <div class=\"badge-rating\" style=\"background: ").Append(ratingColor).AppendLine("; color: #FFF;\">");
        sb.Append("      <span class=\"score-circle\">").Append(scoreValue).AppendLine("</span>");
        sb.Append("      <span>/ 100<br><strong>").Append(WebUtility.HtmlEncode(ratingText)).AppendLine("</strong></span>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </header>");

        // Grid Summary
        sb.AppendLine("  <div class=\"grid-summary\">");
        sb.AppendLine("    <div class=\"summary-card\">");
        sb.AppendLine("      <div class=\"label\">Problemas Críticos</div>");
        sb.Append("      <div class=\"val\" style=\"color: var(--critical);\">").Append(report.CountBySeverity(Severity.Critical)).AppendLine("</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"summary-card\">");
        sb.AppendLine("      <div class=\"label\">Problemas Altos / Médios</div>");
        sb.Append("      <div class=\"val\" style=\"color: var(--high);\">")
          .Append(report.CountBySeverity(Severity.High) + report.CountBySeverity(Severity.Medium)).AppendLine("</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"summary-card\">");
        sb.AppendLine("      <div class=\"label\">Problemas Baixos</div>");
        sb.Append("      <div class=\"val\" style=\"color: var(--low);\">").Append(report.CountBySeverity(Severity.Low)).AppendLine("</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"summary-card\">");
        sb.AppendLine("      <div class=\"label\">Recomendações</div>");
        sb.Append("      <div class=\"val\" style=\"color: var(--info);\">").Append(report.Recommendations.Count).AppendLine("</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");

        // Overview / Metadados
        sb.AppendLine("  <section>");
        sb.AppendLine("    <h2>Visão Geral da Varredura</h2>");
        sb.AppendLine("    <table>");
        sb.AppendLine("      <tr><th>Modo da Varredura</th><td>" + WebUtility.HtmlEncode(report.Mode.ToString()) + "</td></tr>");
        sb.AppendLine("      <tr><th>Duração</th><td>" + report.Duration.TotalSeconds.ToString("F1", CultureInfo.CurrentCulture) + " segundos</td></tr>");
        sb.AppendLine("      <tr><th>Scanners Executados</th><td>" + report.Results.Count.ToString(CultureInfo.CurrentCulture) + " módulos</td></tr>");
        sb.AppendLine("      <tr><th>Status</th><td>" + (report.WasCancelled ? "Cancelada pelo usuário (resultado parcial)" : "Concluída com sucesso") + "</td></tr>");
        sb.AppendLine("    </table>");
        sb.AppendLine("  </section>");

        // Problemas Confirmados (Crítico, Alto, Médio, Baixo)
        var problems = report.Problems;
        sb.AppendLine("  <section>");
        sb.Append("    <h2>Problemas Identificados (").Append(problems.Count).AppendLine(")</h2>");

        if (problems.Count == 0)
        {
            sb.AppendLine("    <p style=\"color: var(--success); font-weight: 600;\">Nenhum problema foi detectado na varredura.</p>");
        }
        else
        {
            foreach (var f in problems)
            {
                var sevClass = f.Severity switch
                {
                    Severity.Critical => "sev-critical",
                    Severity.High => "sev-high",
                    Severity.Medium => "sev-medium",
                    Severity.Low => "sev-low",
                    _ => "sev-info"
                };
                var borderCol = f.Severity switch
                {
                    Severity.Critical => "var(--critical)",
                    Severity.High => "var(--high)",
                    Severity.Medium => "var(--medium)",
                    Severity.Low => "var(--low)",
                    _ => "var(--info)"
                };

                sb.Append("    <div class=\"finding-card\" style=\"border-left-color: ").Append(borderCol).AppendLine(";\">");
                sb.AppendLine("      <div class=\"finding-header\">");
                sb.Append("        <div class=\"finding-title\">").Append(WebUtility.HtmlEncode(f.Title)).AppendLine("</div>");
                sb.Append("        <span class=\"severity-badge ").Append(sevClass).Append("\">")
                  .Append(WebUtility.HtmlEncode(f.Severity.ToString())).AppendLine("</span>");
                sb.AppendLine("      </div>");
                sb.Append("      <div class=\"field-row\"><span class=\"field-label\">O que aconteceu:</span> ")
                  .Append(WebUtility.HtmlEncode(f.Description)).AppendLine("</div>");

                if (!string.IsNullOrWhiteSpace(f.ProbableCause))
                {
                    sb.Append("      <div class=\"field-row\"><span class=\"field-label\">Causa provável:</span> ")
                      .Append(WebUtility.HtmlEncode(f.ProbableCause)).AppendLine("</div>");
                }

                sb.Append("      <div class=\"field-row\"><span class=\"field-label\">Impacto:</span> ")
                  .Append(WebUtility.HtmlEncode(f.Impact)).AppendLine("</div>");
                sb.Append("      <div class=\"field-row\"><span class=\"field-label\">Recomendação:</span> ")
                  .Append(WebUtility.HtmlEncode(f.Recommendation)).AppendLine("</div>");

                if (f.Evidence.Count > 0)
                {
                    sb.AppendLine("      <div class=\"field-row\"><span class=\"field-label\">Evidências:</span></div>");
                    sb.AppendLine("      <ul class=\"evidence-list\">");
                    foreach (var ev in f.Evidence)
                    {
                        var valStr = ev.Value.HasValue ? $" ({ev.Value.Value.ToString(CultureInfo.CurrentCulture)} {ev.Unit})" : string.Empty;
                        sb.Append("        <li><strong>").Append(WebUtility.HtmlEncode(ev.Source.ToString())).Append(":</strong> ")
                          .Append(WebUtility.HtmlEncode(ev.Description)).Append(WebUtility.HtmlEncode(valStr)).AppendLine("</li>");
                    }
                    sb.AppendLine("      </ul>");
                }

                if (f.HasRepair)
                {
                    sb.Append("      <div class=\"field-row\" style=\"margin-top: 8px; color: var(--accent); font-weight: 600;\">")
                      .Append("Correção automática disponível (Risco: ").Append(f.RepairRisk?.ToString() ?? "N/A")
                      .Append(", Rollback: ").Append(f.SupportsRollback ? "Sim" : "Não").AppendLine(")</div>");
                }

                sb.AppendLine("    </div>");
            }
        }
        sb.AppendLine("  </section>");

        // Recomendações
        if (report.Recommendations.Count > 0)
        {
            sb.AppendLine("  <section>");
            sb.Append("    <h2>Recomendações e Boas Práticas (").Append(report.Recommendations.Count).AppendLine(")</h2>");
            foreach (var r in report.Recommendations)
            {
                sb.AppendLine("    <div class=\"finding-card\" style=\"border-left-color: var(--info);\">");
                sb.AppendLine("      <div class=\"finding-header\">");
                sb.Append("        <div class=\"finding-title\">").Append(WebUtility.HtmlEncode(r.Title)).AppendLine("</div>");
                sb.AppendLine("        <span class=\"severity-badge sev-info\">Info</span>");
                sb.AppendLine("      </div>");
                sb.Append("      <div class=\"field-row\">").Append(WebUtility.HtmlEncode(r.Description)).AppendLine("</div>");
                sb.Append("      <div class=\"field-row\"><span class=\"field-label\">Sugestão:</span> ")
                  .Append(WebUtility.HtmlEncode(r.Recommendation)).AppendLine("</div>");
                sb.AppendLine("    </div>");
            }
            sb.AppendLine("  </section>");
        }

        // Rodapé
        sb.AppendLine("  <footer>");
        sb.AppendLine("    <p>Leccor Windows Health Center &bull; Diagnóstico baseado em evidências &bull; 100% Local e Seguro</p>");
        sb.AppendLine("  </footer>");

        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string GetRatingText(HealthRating rating) => rating switch
    {
        HealthRating.Excellent => "Excelente",
        HealthRating.Good => "Bom",
        HealthRating.Attention => "Atenção",
        HealthRating.Critical => "Crítico",
        _ => "Sem Dados"
    };

    private static string GetRatingColor(HealthRating rating) => rating switch
    {
        HealthRating.Excellent => "#10B981",
        HealthRating.Good => "#3B82F6",
        HealthRating.Attention => "#F97316",
        HealthRating.Critical => "#EF4444",
        _ => "#64748B"
    };
}
