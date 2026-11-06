using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.WindowsHealth;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Corrupção detectada no repositório de componentes do Windows (DISM) (seção 22 do PROMPT).
/// </summary>
public sealed class WindowsComponentStoreRule : IRule
{
    public string Id => "windows.component-store-corrupted";

    public string Name => "Repositório de componentes corrompido";

    public IReadOnlyList<string> RequiredScanners { get; } = ["windows.integrity"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("RequireCheckSuccess", 1, "booleano", "A regra só avalia quando a ferramenta DISM executou com sucesso (código 0 ou detecção de corrupção comprovada)."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var obs = context.Observations.FirstOrDefault(o => o.Kind == WindowsIntegrityNames.ObservationComponentStore);
        if (obs is null)
        {
            yield break;
        }

        var statusStr = obs.Attributes.GetValueOrDefault(WindowsIntegrityNames.AttrStatus);
        if (!Enum.TryParse<ComponentStoreStatus>(statusStr, out var status))
        {
            yield break;
        }

        if (status is not (ComponentStoreStatus.Repairable or ComponentStoreStatus.Corrupted))
        {
            yield break;
        }

        var isRepairable = status == ComponentStoreStatus.Repairable;
        var severity = isRepairable ? Severity.High : Severity.Critical;
        var message = obs.Attributes.GetValueOrDefault(WindowsIntegrityNames.AttrMessage) ?? "Corrupção detectada no Component Store.";
        var cmd = obs.Attributes.GetValueOrDefault(WindowsIntegrityNames.AttrCommandLine) ?? "dism.exe /Online /Cleanup-Image /CheckHealth";

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
            Severity = severity,
            Confidence = Confidence.Confirmed,
            Title = isRepairable ? "Repositório de componentes do Windows danificado (reparável)" : "Repositório de componentes do Windows corrompido",
            Description = message,
            ProbableCause = "Atualização do Windows interrompida, desligamento abrupto durante operação de manutenção ou falha de disco.",
            Impact = "Falhas em atualizações futuras do Windows Update, impossibilidade de instalar novos recursos ou reparar arquivos do sistema.",
            Recommendation = "Executar o reparo do repositório de componentes utilizando DISM /Online /Cleanup-Image /RestoreHealth.",
            Evidence = evidence,
            RequiresElevation = true,
            RequiresRestart = false,
            SupportsRollback = false,
            DetectedAt = DateTimeOffset.UtcNow,
        };
    }
}
