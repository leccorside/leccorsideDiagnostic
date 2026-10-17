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

    public static string StatusGlyph(ScannerRunStatus status) => status switch
    {
        ScannerRunStatus.Completed => "✓",
        ScannerRunStatus.Running => "→",
        ScannerRunStatus.Failed => "✗",
        ScannerRunStatus.Cancelled => "⊘",
        ScannerRunStatus.Skipped => "–",
        _ => "○",
    };

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
