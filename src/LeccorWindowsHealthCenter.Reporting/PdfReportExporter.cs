using System.Globalization;
using System.Text;
using LeccorWindowsHealthCenter.Application.Reporting;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Reporting;

/// <summary>
/// Exporta o laudo de saúde em formato PDF 1.4 estruturado, autocontido e sem dependências externas (seção 54 e 55).
/// </summary>
public sealed class PdfReportExporter : IReportExporter
{
    public string Format => "PDF";

    public string FileExtension => ".pdf";

    public async Task ExportAsync(ScanReport report, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        var bytes = GeneratePdf(report);
        await destination.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    public static byte[] GeneratePdf(ScanReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var writer = new SimplePdfWriter();
        writer.BuildDocument(report);
        return writer.ToByteArray();
    }

    private sealed class SimplePdfWriter
    {
        private const double PageWidth = 595.28;
        private const double PageHeight = 841.89;
        private const double Margin = 40.0;
        private const double UsableWidth = PageWidth - (Margin * 2);

        private readonly List<PageContent> _pages = [];
        private PageContent _currentPage = new();
        private double _y = PageHeight - Margin;

        public void BuildDocument(ScanReport report)
        {
            _pages.Add(_currentPage);

            // Banner do Cabeçalho
            DrawHeaderBanner(report);

            // Cards de Resumo
            DrawSummaryCards(report);

            // Metadados
            DrawMetadata(report);

            // Lista de Problemas
            DrawProblemsSection(report);

            // Recomendações
            if (report.Recommendations.Count > 0)
            {
                DrawRecommendationsSection(report);
            }

            // Rodapés com paginação
            for (int i = 0; i < _pages.Count; i++)
            {
                var pageNum = i + 1;
                var total = _pages.Count;
                _pages[i].AddText(Margin, 25, $"/F1 9 Tf 0.5 0.5 0.5 rg (Leccor Windows Health Center - Pagina {pageNum.ToString(CultureInfo.InvariantCulture)} de {total.ToString(CultureInfo.InvariantCulture)}) Tj");
            }
        }

        private void EnsureSpace(double heightNeeded)
        {
            if (_y - heightNeeded < Margin + 30)
            {
                _currentPage = new PageContent();
                _pages.Add(_currentPage);
                _y = PageHeight - Margin;
            }
        }

        private void DrawHeaderBanner(ScanReport report)
        {
            var bannerHeight = 70.0;
            _y -= bannerHeight;

            // Fundo escuro azulado
            _currentPage.AddRect(Margin, _y, UsableWidth, bannerHeight, 0.08, 0.12, 0.22, true);

            // Título
            _currentPage.AddText(Margin + 16, _y + 40, "/F2 18 Tf 1 1 1 rg (Laudo de Saude do Computador) Tj");

            // Subtítulo
            var dateStr = report.CompletedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
            _currentPage.AddText(Margin + 16, _y + 20, $"/F1 10 Tf 0.7 0.75 0.85 rg (Computador: {EscapePdf(report.MachineName)}  |  Data: {dateStr}) Tj");

            // Score Badge à direita
            var scoreVal = report.Score.Overall?.ToString(CultureInfo.InvariantCulture) ?? "N/A";
            var ratingText = GetRatingText(report.Score.Rating);
            _currentPage.AddText(PageWidth - Margin - 110, _y + 36, $"/F2 20 Tf 1 1 1 rg (Score: {scoreVal}) Tj");
            _currentPage.AddText(PageWidth - Margin - 110, _y + 20, $"/F1 10 Tf 0.7 0.9 0.7 rg ({ratingText}) Tj");

            _y -= 20;
        }

        private void DrawSummaryCards(ScanReport report)
        {
            var cardWidth = (UsableWidth - 30) / 4;
            var cardHeight = 44.0;
            _y -= cardHeight;

            DrawCard(Margin, _y, cardWidth, cardHeight, "Criticos", report.CountBySeverity(Severity.Critical).ToString(CultureInfo.InvariantCulture), 0.93, 0.27, 0.27);
            DrawCard(Margin + cardWidth + 10, _y, cardWidth, cardHeight, "Altos / Medios", (report.CountBySeverity(Severity.High) + report.CountBySeverity(Severity.Medium)).ToString(CultureInfo.InvariantCulture), 0.97, 0.45, 0.09);
            DrawCard(Margin + (cardWidth * 2) + 20, _y, cardWidth, cardHeight, "Baixos", report.CountBySeverity(Severity.Low).ToString(CultureInfo.InvariantCulture), 0.23, 0.51, 0.96);
            DrawCard(Margin + (cardWidth * 3) + 30, _y, cardWidth, cardHeight, "Recomendacoes", report.Recommendations.Count.ToString(CultureInfo.InvariantCulture), 0.02, 0.71, 0.83);

            _y -= 20;
        }

        private void DrawCard(double x, double y, double w, double h, string label, string val, double r, double g, double b)
        {
            _currentPage.AddRect(x, y, w, h, 0.95, 0.96, 0.98, true);
            _currentPage.AddRect(x, y, w, h, 0.85, 0.88, 0.92, false);
            _currentPage.AddText(x + 10, y + 26, $"/F1 9 Tf 0.4 0.45 0.5 rg ({EscapePdf(label)}) Tj");
            _currentPage.AddText(x + 10, y + 10, $"/F2 14 Tf {r.ToString("F2", CultureInfo.InvariantCulture)} {g.ToString("F2", CultureInfo.InvariantCulture)} {b.ToString("F2", CultureInfo.InvariantCulture)} rg ({EscapePdf(val)}) Tj");
        }

        private void DrawMetadata(ScanReport report)
        {
            EnsureSpace(40);
            _y -= 16;
            _currentPage.AddText(Margin, _y, "/F2 12 Tf 0.1 0.1 0.1 rg (Resumo da Execucao) Tj");
            _y -= 14;
            var info = $"Modo: {report.Mode}  |  Duracao: {report.Duration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)}s  |  Scanners: {report.Results.Count.ToString(CultureInfo.InvariantCulture)}  |  Status: {(report.WasCancelled ? "Cancelada" : "Concluida")}";
            _currentPage.AddText(Margin, _y, $"/F1 9 Tf 0.3 0.3 0.3 rg ({EscapePdf(info)}) Tj");
            _y -= 20;
        }

        private void DrawProblemsSection(ScanReport report)
        {
            EnsureSpace(30);
            _y -= 16;
            _currentPage.AddText(Margin, _y, $"/F2 13 Tf 0.1 0.1 0.1 rg (Problemas Identificados ({report.Problems.Count.ToString(CultureInfo.InvariantCulture)})) Tj");
            _y -= 12;

            if (report.Problems.Count == 0)
            {
                _currentPage.AddText(Margin, _y, "/F1 10 Tf 0.1 0.6 0.2 rg (Nenhum problema foi identificado na varredura.) Tj");
                _y -= 20;
                return;
            }

            foreach (var p in report.Problems)
            {
                var needed = 65.0 + (p.Evidence.Count > 0 ? 25.0 : 0.0);
                EnsureSpace(needed);

                _y -= 16;
                // Barra colorida de severidade
                var (r, g, b) = GetSeverityColor(p.Severity);
                _currentPage.AddRect(Margin, _y - 4, 4, 18, r, g, b, true);

                _currentPage.AddText(Margin + 10, _y, $"/F2 11 Tf 0.1 0.1 0.1 rg ([{p.Severity}] {EscapePdf(p.Title)}) Tj");
                _y -= 14;

                _currentPage.AddText(Margin + 10, _y, $"/F1 9 Tf 0.25 0.25 0.25 rg (O que houve: {EscapePdf(Truncate(p.Description, 95))}) Tj");
                _y -= 12;

                _currentPage.AddText(Margin + 10, _y, $"/F1 9 Tf 0.25 0.25 0.25 rg (Impacto: {EscapePdf(Truncate(p.Impact, 95))}) Tj");
                _y -= 12;

                _currentPage.AddText(Margin + 10, _y, $"/F1 9 Tf 0.15 0.4 0.15 rg (Recomendacao: {EscapePdf(Truncate(p.Recommendation, 95))}) Tj");
                _y -= 12;

                if (p.HasRepair)
                {
                    _currentPage.AddText(Margin + 10, _y, $"/F2 8 Tf 0.15 0.35 0.8 rg (Correcao automatica disponivel: Risco {p.RepairRisk} | Rollback: {(p.SupportsRollback ? "Sim" : "Nao")}) Tj");
                    _y -= 10;
                }

                _y -= 6;
            }
        }

        private void DrawRecommendationsSection(ScanReport report)
        {
            EnsureSpace(30);
            _y -= 16;
            _currentPage.AddText(Margin, _y, $"/F2 13 Tf 0.1 0.1 0.1 rg (Recomendacoes e Melhores Praticas ({report.Recommendations.Count.ToString(CultureInfo.InvariantCulture)})) Tj");
            _y -= 14;

            foreach (var r in report.Recommendations)
            {
                EnsureSpace(35);
                _y -= 14;
                _currentPage.AddText(Margin, _y, $"/F2 10 Tf 0.1 0.4 0.6 rg (* {EscapePdf(r.Title)}) Tj");
                _y -= 12;
                _currentPage.AddText(Margin + 10, _y, $"/F1 9 Tf 0.3 0.3 0.3 rg ({EscapePdf(Truncate(r.Description, 100))}) Tj");
                _y -= 8;
            }
        }

        public byte[] ToByteArray()
        {
            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms, Encoding.Latin1);

            writer.Write("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
            writer.Flush();

            var offsets = new List<long>();

            // Obj 1: Catalog
            offsets.Add(ms.Position);
            writer.Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            writer.Flush();

            // Obj 2: Pages
            offsets.Add(ms.Position);
            var kids = string.Join(" ", Enumerable.Range(0, _pages.Count).Select(i => $"{5 + (i * 2)} 0 R"));
            writer.Write($"2 0 obj\n<< /Type /Pages /Kids [{kids}] /Count {_pages.Count.ToString(CultureInfo.InvariantCulture)} >>\nendobj\n");
            writer.Flush();

            // Obj 3: Font F1 (Helvetica)
            offsets.Add(ms.Position);
            writer.Write("3 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");
            writer.Flush();

            // Obj 4: Font F2 (Helvetica-Bold)
            offsets.Add(ms.Position);
            writer.Write("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");
            writer.Flush();

            // Páginas e Streams
            for (int i = 0; i < _pages.Count; i++)
            {
                var pageObj = 5 + (i * 2);
                var streamObj = pageObj + 1;

                offsets.Add(ms.Position);
                writer.Write($"{pageObj.ToString(CultureInfo.InvariantCulture)} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth.ToString("F2", CultureInfo.InvariantCulture)} {PageHeight.ToString("F2", CultureInfo.InvariantCulture)}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {streamObj.ToString(CultureInfo.InvariantCulture)} 0 R >>\nendobj\n");
                writer.Flush();

                offsets.Add(ms.Position);
                var streamData = _pages[i].GetContentBytes();
                writer.Write($"{streamObj.ToString(CultureInfo.InvariantCulture)} 0 obj\n<< /Length {streamData.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n");
                writer.Flush();
                ms.Write(streamData);
                writer.Write("\nendstream\nendobj\n");
                writer.Flush();
            }

            // xref
            var xrefOffset = ms.Position;
            var totalObjs = 5 + (_pages.Count * 2);
            writer.Write($"xref\n0 {totalObjs.ToString(CultureInfo.InvariantCulture)}\n0000000000 65535 f \n");
            foreach (var off in offsets)
            {
                writer.Write($"{off:D10} 00000 n \n");
            }
            writer.Write($"trailer\n<< /Size {totalObjs.ToString(CultureInfo.InvariantCulture)} /Root 1 0 R >>\nstartxref\n{xrefOffset.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
            writer.Flush();

            return ms.ToArray();
        }

        private static string Truncate(string val, int maxLen)
        {
            if (string.IsNullOrEmpty(val))
            {
                return string.Empty;
            }

            var clean = val.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
            return clean.Length <= maxLen ? clean : clean[..maxLen] + "...";
        }

        private static (double r, double g, double b) GetSeverityColor(Severity s) => s switch
        {
            Severity.Critical => (0.93, 0.27, 0.27),
            Severity.High => (0.97, 0.45, 0.09),
            Severity.Medium => (0.92, 0.70, 0.03),
            Severity.Low => (0.23, 0.51, 0.96),
            _ => (0.02, 0.71, 0.83)
        };

        private static string GetRatingText(HealthRating r) => r switch
        {
            HealthRating.Excellent => "Excelente",
            HealthRating.Good => "Bom",
            HealthRating.Attention => "Atencao",
            HealthRating.Critical => "Critico",
            _ => "Sem Dados"
        };

        private static string EscapePdf(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var c in s)
            {
                switch (c)
                {
                    case '(':
                        sb.Append("\\(");
                        break;
                    case ')':
                        sb.Append("\\)");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    default:
                        if (c >= 32 && c <= 255)
                        {
                            sb.Append(c);
                        }
                        else
                        {
                            sb.Append('?');
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }

    private sealed class PageContent
    {
        private readonly StringBuilder _sb = new();

        public void AddRect(double x, double y, double w, double h, double r, double g, double b, bool fill)
        {
            _sb.Append(CultureInfo.InvariantCulture, $"q {r.ToString("F2", CultureInfo.InvariantCulture)} {g.ToString("F2", CultureInfo.InvariantCulture)} {b.ToString("F2", CultureInfo.InvariantCulture)} {(fill ? "rg" : "RG")} {x.ToString("F2", CultureInfo.InvariantCulture)} {y.ToString("F2", CultureInfo.InvariantCulture)} {w.ToString("F2", CultureInfo.InvariantCulture)} {h.ToString("F2", CultureInfo.InvariantCulture)} re {(fill ? "f" : "S")} Q\n");
        }

        public void AddText(double x, double y, string textCommand)
        {
            _sb.Append(CultureInfo.InvariantCulture, $"BT 1 0 0 1 {x.ToString("F2", CultureInfo.InvariantCulture)} {y.ToString("F2", CultureInfo.InvariantCulture)} Tm {textCommand} ET\n");
        }

        public byte[] GetContentBytes() => Encoding.Latin1.GetBytes(_sb.ToString());
    }
}
