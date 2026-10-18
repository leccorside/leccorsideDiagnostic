using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.App.ViewModels;

/// <summary>Textos pt-BR exibidos para enums do domínio.</summary>
public static class Display
{
    public static string Severity(Severity severity) => severity switch
    {
        Domain.Severity.Critical => "Crítico",
        Domain.Severity.High => "Alto",
        Domain.Severity.Medium => "Médio",
        Domain.Severity.Low => "Baixo",
        _ => "Recomendação",
    };

    public static string Risk(RiskLevel? risk) => risk switch
    {
        RiskLevel.Safe => "SAFE — seguro",
        RiskLevel.Low => "LOW — baixo, reversível",
        RiskLevel.Medium => "MEDIUM — exige confirmação",
        RiskLevel.High => "HIGH — exige confirmação explícita",
        RiskLevel.Critical => "CRITICAL — somente manual",
        _ => "Sem correção automática",
    };

    public static string Rating(HealthRating rating) => rating switch
    {
        HealthRating.Excellent => "EXCELENTE",
        HealthRating.Good => "BOM",
        HealthRating.Attention => "ATENÇÃO",
        HealthRating.Critical => "CRÍTICO",
        _ => "SEM DADOS",
    };

    public static string Confidence(Confidence confidence) => confidence.Level switch
    {
        ConfidenceLevel.Confirmed => $"Confirmado ({confidence.Percent}%)",
        ConfidenceLevel.Likely => $"Provável ({confidence.Percent}%)",
        _ => $"Possível ({confidence.Percent}%)",
    };

    /// <summary>Ícone Segoe Fluent/MDL2 do status do scanner (exibido com a fonte IconFont).</summary>
    public static string StatusGlyph(ScannerRunStatus status) => status switch
    {
        ScannerRunStatus.Completed => "", // CheckMark
        ScannerRunStatus.Running => "",   // Play
        ScannerRunStatus.Failed => "",    // Error
        ScannerRunStatus.Cancelled => "", // Blocked
        ScannerRunStatus.Skipped => "",   // Remove
        _ => "",                          // CircleRing (pendente)
    };

    public static string Status(ScannerRunStatus status) => status switch
    {
        ScannerRunStatus.Completed => "Concluído",
        ScannerRunStatus.Running => "Em execução",
        ScannerRunStatus.Failed => "Falhou",
        ScannerRunStatus.Cancelled => "Cancelado",
        ScannerRunStatus.Skipped => "Ignorado",
        _ => "Pendente",
    };

    public static string ScoreArea(ScoreCategory category) => category switch
    {
        ScoreCategory.Hardware => "Hardware",
        ScoreCategory.Windows => "Windows",
        ScoreCategory.Performance => "Performance",
        ScoreCategory.Storage => "Armazenamento",
        ScoreCategory.Stability => "Estabilidade",
        ScoreCategory.Security => "Segurança",
        _ => category.ToString(),
    };

    /// <summary>
    /// "O score avaliou X de 6 áreas" — um score alto só tem sentido sabendo o que foi medido.
    /// Null quando nada foi medido (o próprio score já diz "sem dados").
    /// </summary>
    public static string? Coverage(IEnumerable<ScoreCategory> measured)
    {
        var covered = measured.Distinct().Order().ToList();
        if (covered.Count == 0)
        {
            return null;
        }

        var all = Enum.GetValues<ScoreCategory>();
        var missing = all.Except(covered).ToList();
        var text = $"Avaliou {covered.Count} de {all.Length} áreas: {JoinPt(covered.Select(ScoreArea))}.";
        return missing.Count == 0 ? text : $"{text} Ainda não medidas: {JoinPt(missing.Select(ScoreArea))}.";
    }

    private static string JoinPt(IEnumerable<string> items)
    {
        var list = items.ToList();
        return list.Count <= 1 ? string.Concat(list) : $"{string.Join(", ", list.Take(list.Count - 1))} e {list[^1]}";
    }

    public static string Module(ScanModule module) => module switch
    {
        ScanModule.Hardware => "Hardware",
        ScanModule.Performance => "Performance",
        ScanModule.Disks => "Discos",
        ScanModule.Windows => "Windows",
        ScanModule.Explorer => "Explorer",
        ScanModule.Processes => "Processos",
        ScanModule.Startup => "Startup",
        ScanModule.Services => "Serviços",
        ScanModule.Drivers => "Drivers",
        ScanModule.Applications => "Aplicativos",
        ScanModule.Events => "Eventos",
        ScanModule.WindowsUpdate => "Windows Update",
        ScanModule.Network => "Rede",
        ScanModule.Security => "Segurança",
        ScanModule.Development => "Desenvolvimento",
        ScanModule.Storage => "Armazenamento",
        _ => module.ToString(),
    };
}
