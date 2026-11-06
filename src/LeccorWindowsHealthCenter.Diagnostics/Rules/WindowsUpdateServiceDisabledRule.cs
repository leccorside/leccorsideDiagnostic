using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.WindowsUpdate;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta quando o serviço do Windows Update foi desativado no sistema (seção 26 e 57 do PROMPT).
/// </summary>
public sealed class WindowsUpdateServiceDisabledRule : IRule
{
    public string Id => "windowsupdate.service-disabled";

    public string Name => "Serviço essencial do Windows Update desativado";

    public IReadOnlyList<string> RequiredScanners { get; } = ["windowsupdate"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("Enabled", 1, "booleano", "Identifica desativação prejudicial do serviço wuauserv (regra estrita da seção 57)."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var statusObs = context.Observations.FirstOrDefault(o => o.Kind == WindowsUpdateNames.ObservationStatus);
        if (statusObs == null)
        {
            yield break;
        }

        var wuauservState = statusObs.Attributes.GetValueOrDefault(WindowsUpdateNames.AttrWuauservState);
        if (!string.Equals(wuauservState, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var evidence = new List<Evidence>
        {
            new()
            {
                Source = EvidenceSource.Registry,
                Description = "O serviço 'wuauserv' (Windows Update) está com tipo de inicialização 'Desativado' (Disabled)",
                Reference = "wuauserv",
                CollectedAt = DateTimeOffset.UtcNow,
            },
        };

        yield return new Finding
        {
            Id = Guid.NewGuid(),
            RuleId = Id,
            ScannerId = "windowsupdate",
            Category = FindingCategory.WindowsUpdate,
            Severity = Severity.High,
            Confidence = Confidence.FromPercent(95),
            Title = "Serviço do Windows Update está desativado",
            Description = "O serviço principal do Windows Update (wuauserv) foi desativado. O computador não receberá atualizações de segurança críticas nem correções cumulativas.",
            ProbableCause = "Script de 'otimização' agressivo de terceiros ou alteração manual no registro.",
            Impact = "Deixa o sistema vulnerável a novas ameaças de segurança e impede o funcionamento correto da Microsoft Store.",
            Recommendation = "Reativar o serviço do Windows Update para o modo Automático ou Manual.",
            RepairActionId = "repair.windowsupdate.enable-service",
            RepairRisk = RiskLevel.Low,
            Evidence = evidence,
            RequiresElevation = true,
            RequiresRestart = false,
            SupportsRollback = true,
            DetectedAt = DateTimeOffset.UtcNow,
        };
    }
}

