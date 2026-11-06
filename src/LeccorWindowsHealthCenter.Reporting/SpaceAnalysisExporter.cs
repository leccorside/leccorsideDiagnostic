using System.Globalization;
using System.Text;
using System.Text.Json;
using LeccorWindowsHealthCenter.Application.Storage;

namespace LeccorWindowsHealthCenter.Reporting;

/// <summary>
/// Exportador da análise de espaço em disco em CSV e JSON (Pacote B).
/// </summary>
public sealed class SpaceAnalysisExporter : ISpaceAnalysisExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string ExportToJson(SpaceAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        return JsonSerializer.Serialize(analysis, JsonOptions);
    }

    public string ExportToCsv(SpaceAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        // Cabeçalho CSV padrão com ponto-e-vírgula para abertura direta no Excel pt-BR
        sb.AppendLine("Tipo;Caminho;Nome;Tamanho_Bytes;Tamanho_Formatado;Total_Arquivos;Inacessivel;HardLinks_Deduplicados_Bytes");

        // Percurso recursivo da árvore de pastas
        WriteFolderNode(sb, analysis.Root);

        // Seção dos maiores arquivos
        if (analysis.LargestFiles.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("# MAIORES ARQUIVOS DETECTADOS");
            sb.AppendLine("Tipo;Caminho;Nome;Tamanho_Bytes;Tamanho_Formatado;Data_Modificacao;Inacessivel;HardLinks_Deduplicados_Bytes");
            foreach (var file in analysis.LargestFiles)
            {
                var fileName = Path.GetFileName(file.Path);
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "Arquivo;\"{0}\";\"{1}\";{2};\"{3}\";\"{4:yyyy-MM-dd HH:mm:ss}\";Nao;0",
                    Escape(file.Path),
                    Escape(fileName),
                    file.SizeBytes,
                    FormatBytes(file.SizeBytes),
                    file.LastWrite));
            }
        }

        // Seção dos arquivos grandes antigos
        if (analysis.OldFiles.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("# ARQUIVOS GRANDES ANTIGOS (> 2 ANOS)");
            sb.AppendLine("Tipo;Caminho;Nome;Tamanho_Bytes;Tamanho_Formatado;Data_Modificacao;Inacessivel;HardLinks_Deduplicados_Bytes");
            foreach (var file in analysis.OldFiles)
            {
                var fileName = Path.GetFileName(file.Path);
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "ArquivoAntigo;\"{0}\";\"{1}\";{2};\"{3}\";\"{4:yyyy-MM-dd HH:mm:ss}\";Nao;0",
                    Escape(file.Path),
                    Escape(fileName),
                    file.SizeBytes,
                    FormatBytes(file.SizeBytes),
                    file.LastWrite));
            }
        }

        return sb.ToString();
    }

    private static void WriteFolderNode(StringBuilder sb, FolderNode node)
    {
        sb.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "Pasta;\"{0}\";\"{1}\";{2};\"{3}\";{4};{5};{6}",
            Escape(node.Path),
            Escape(node.Name),
            node.SizeBytes,
            FormatBytes(node.SizeBytes),
            node.FileCount,
            node.Inaccessible ? "Sim" : "Nao",
            node.HardLinkDeduplicatedBytes));

        foreach (var child in node.Children)
        {
            WriteFolderNode(sb, child);
        }
    }

    private static string Escape(string text) => text.Replace("\"", "\"\"");

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024L * 1024 * 1024 * 1024 => $"{(double)bytes / (1024L * 1024 * 1024 * 1024):0.0} TB",
            >= 1024L * 1024 * 1024 => $"{(double)bytes / (1024L * 1024 * 1024):0.0} GB",
            >= 1024L * 1024 => $"{(double)bytes / (1024L * 1024):0.0} MB",
            >= 1024L => $"{(double)bytes / 1024L:0.0} KB",
            _ => $"{bytes} B",
        };
    }
}
