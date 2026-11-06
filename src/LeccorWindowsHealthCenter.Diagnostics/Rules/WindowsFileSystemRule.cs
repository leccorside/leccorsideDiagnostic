using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.WindowsHealth;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Erros de integridade detectados no sistema de arquivos do volume (CHKDSK) (seção 22 do PROMPT).
/// </summary>
public sealed class WindowsFileSystemRule : IRule
{
    public string Id => "windows.filesystem-errors";

    public string Name => "Erros de integridade no sistema de arquivos";

    public IReadOnlyList<string> RequiredScanners { get; } = ["windows.integrity"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("RequireCheckSuccess", 1, "booleano", "A regra avalia quando o utilitário CHKDSK concluiu a análise do volume e reportou inconsistências."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var obs = context.Observations.FirstOrDefault(o => o.Kind == WindowsIntegrityNames.ObservationFileSystem);
        if (obs is null)
        {
            yield break;
        }

        var statusStr = obs.Attributes.GetValueOrDefault(WindowsIntegrityNames.AttrStatus);
        if (!Enum.TryParse<FileSystemStatus>(statusStr, out var status) || status != FileSystemStatus.ErrorsFound)
        {
            yield break;
        }

        var drive = obs.Attributes.GetValueOrDefault(WindowsIntegrityNames.AttrDrive) ?? "C:";
        var message = obs.Attributes.GetValueOrDefault(WindowsIntegrityNames.AttrMessage) ?? $"Erros encontrados no volume {drive}.";
        var cmd = obs.Attributes.GetValueOrDefault(WindowsIntegrityNames.AttrCommandLine) ?? $"chkdsk.exe {drive}";

        var evidence = new List<Evidence>(obs.Evidence)
        {
            new()
            {
                Source = EvidenceSource.Command,
                Description = message,
                Value = (int)status,
                Unit = "status",
                Reference = cmd,
                SampleCount = 1,
                CollectedAt = DateTimeOffset.UtcNow,
            },
        };

        yield return new Finding
        {
            Id = Guid.NewGuid(),
            RuleId = Id,
            ScannerId = "windows.integrity",
            Category = FindingCategory.Windows,
            Severity = Severity.Critical,
            Confidence = Confidence.FromPercent(95),
            Title = $"Erros detectados no sistema de arquivos do volume {drive}",
            Description = message,
            ProbableCause = "Setores defeituosos no disco, queda de energia enquanto arquivos eram gravados ou falha do controlador de armazenamento.",
            Impact = "Risco grave de perda ou corrupção de arquivos e falha de inicialização do sistema operacional.",
            Recommendation = $"Realizar backup imediato dos dados do volume {drive} e agendar reparo com o comando chkdsk {drive} /f.",
            Evidence = evidence,
            RequiresElevation = true,
            RequiresRestart = true,
            SupportsRollback = false,
            DetectedAt = DateTimeOffset.UtcNow,
        };
    }
}
