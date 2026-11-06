using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.WindowsUpdate;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta atualizações do Windows pendentes de reinicialização para conclusão (seção 26 do PROMPT).
/// </summary>
public sealed class WindowsUpdateRebootPendingRule : IRule
{
    public string Id => "windowsupdate.reboot-pending";

    public string Name => "Reinicialização do sistema pendente por atualizações";

    public IReadOnlyList<string> RequiredScanners { get; } = ["windowsupdate"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("Enabled", 1, "booleano", "Alerta sobre atualizações instaladas que dependem de reboot do sistema para ter efeito."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var statusObs = context.Observations.FirstOrDefault(o => o.Kind == WindowsUpdateNames.ObservationStatus);
        if (statusObs == null)
        {
            yield break;
        }

        var rebootPendingStr = statusObs.Attributes.GetValueOrDefault(WindowsUpdateNames.AttrRebootPending);
        if (!bool.TryParse(rebootPendingStr, out var isPending) || !isPending)
        {
            yield break;
        }

        var reason = statusObs.Attributes.GetValueOrDefault(WindowsUpdateNames.AttrRebootReason) ??
                     "Componentes do sistema operacional aguardam reinicialização.";

        var evidence = new List<Evidence>
        {
            new()
            {
                Source = EvidenceSource.Registry,
                Description = $"Registro do Windows indica reinicialização pendente: {reason}",
                Reference = "RebootPending",
                CollectedAt = DateTimeOffset.UtcNow,
            },
        };

        yield return new Finding
        {
            Id = Guid.NewGuid(),
            RuleId = Id,
            ScannerId = "windowsupdate",
            Category = FindingCategory.WindowsUpdate,
            Severity = Severity.Medium,
            Confidence = Confidence.FromPercent(95),
            Title = "Reinicialização necessária para concluir atualizações",
            Description = $"O Windows instalou pacotes ou atualizações que exigem reinicialização do computador para entrar em vigor: {reason}",
            ProbableCause = "Atualização cumulativa recente ou atualização de driver instalada em segundo plano.",
            Impact = "Arquivos temporários de atualização ficam retidos em disco e novas atualizações podem ser bloqueadas até a conclusão do boot.",
            Recommendation = "Reiniciar o computador assim que possível para finalizar a instalação dos pacotes.",
            Evidence = evidence,
            RequiresElevation = false,
            RequiresRestart = true,
            SupportsRollback = false,
            DetectedAt = DateTimeOffset.UtcNow,
        };
    }
}

