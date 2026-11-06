namespace LeccorWindowsHealthCenter.Application.Storage;

/// <summary>
/// Exportador dedicado da árvore de análise de espaço em disco em formatos tabulares e estruturados (CSV e JSON).
/// </summary>
public interface ISpaceAnalysisExporter
{
    /// <summary>Exporta a hierarquia completa de diretórios e maiores arquivos em CSV formatado com UTF-8 com BOM.</summary>
    string ExportToCsv(SpaceAnalysis analysis);

    /// <summary>Exporta a árvore completa em JSON estruturado com formatação legível.</summary>
    string ExportToJson(SpaceAnalysis analysis);
}

