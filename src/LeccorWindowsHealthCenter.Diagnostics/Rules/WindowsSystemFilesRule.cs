using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.WindowsHealth;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Arquivos protegidos do Windows com integridade violada ou corrompidos (SFC) (seção 22 do PROMPT).
/// </summary>
public sealed class WindowsSystemFilesRule : IRule
{
    public string Id => "windows.system-files-corrupted";

    public string Name => "Arquivos protegidos do Windows corrompidos";

    public IReadOnlyList<string> RequiredScanners { get; } = ["windows.integrity"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("RequireCheckSuccess", 1, "booleano", "A regra só avalia quando a ferramenta SFC concluiu a verificação de integridade."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var obs = context.Observations.FirstOrDefault(o => o.Kind == WindowsIntegrityNames.ObservationSystemFiles);
        if (obs is null)
        {
            yield break;
        }

        var statusStr = obs.Attributes.GetValueOrDefault(WindowsIntegrityNames.AttrStatus);
        if (!Enum.TryParse<SystemFilesStatus>(statusStr, out var status))
        {
            yield break;
        }

        if (status is not (SystemFilesStatus.CorruptedRepaired or SystemFilesStatus.CorruptedCannotRepair))
        {
            yield break;
        }

        var message = obs.Attributes.GetValueOrDefault(WindowsIntegrityNames.AttrMessage) ?? "Arquivos corrompidos detectados.";
        var cmd = obs.Attributes.GetValueOrDefault(WindowsIntegrityNames.AttrCommandLine) ?? "sfc.exe /VERIFYONLY";

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
            Severity = Severity.High,
            Confidence = Confidence.FromPercent(95),
            Title = "Arquivos protegidos do Windows corrompidos",
            Description = message,
            ProbableCause = "Falha em disco, atualização incompleta, infecção corrigida por antivírus ou encerramento abrupto do sistema.",
            Impact = "Instabilidade do sistema, falhas em serviços essenciais do Windows ou erros de tela azul (BSOD).",
            Recommendation = "Executar a restauração de arquivos protegidos do Windows utilizando o comando sfc /scannow.",
            Evidence = evidence,
            RequiresElevation = true,
            RequiresRestart = false,
            SupportsRollback = false,
            DetectedAt = DateTimeOffset.UtcNow,
        };
    }
}
