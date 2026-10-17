namespace LeccorWindowsHealthCenter.Domain;

/// <summary>Mapeia categorias de finding e módulos de varredura para as categorias do score.</summary>
public static class CategoryMapping
{
    public static ScoreCategory ToScoreCategory(FindingCategory category) => category switch
    {
        FindingCategory.Hardware or FindingCategory.Gpu or FindingCategory.Disk or FindingCategory.Drivers => ScoreCategory.Hardware,
        FindingCategory.Windows or FindingCategory.WindowsUpdate or FindingCategory.Services or FindingCategory.Explorer => ScoreCategory.Windows,
        FindingCategory.Cpu or FindingCategory.Memory or FindingCategory.Processes or FindingCategory.Startup or FindingCategory.Network or FindingCategory.Development => ScoreCategory.Performance,
        FindingCategory.Storage => ScoreCategory.Storage,
        FindingCategory.Events or FindingCategory.Applications => ScoreCategory.Stability,
        FindingCategory.Security => ScoreCategory.Security,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    /// <summary>Quais categorias do score um módulo de varredura cobre (usado para saber se há dados).</summary>
    public static IReadOnlyList<ScoreCategory> CoveredBy(ScanModule module) => module switch
    {
        ScanModule.Hardware => [ScoreCategory.Hardware],
        ScanModule.Disks => [ScoreCategory.Hardware, ScoreCategory.Storage],
        ScanModule.Drivers => [ScoreCategory.Hardware],
        ScanModule.Performance or ScanModule.Processes or ScanModule.Startup or ScanModule.Network or ScanModule.Development => [ScoreCategory.Performance],
        ScanModule.Windows or ScanModule.WindowsUpdate or ScanModule.Services or ScanModule.Explorer => [ScoreCategory.Windows],
        ScanModule.Storage => [ScoreCategory.Storage],
        ScanModule.Events or ScanModule.Applications => [ScoreCategory.Stability],
        ScanModule.Security => [ScoreCategory.Security],
        _ => [],
    };
}
