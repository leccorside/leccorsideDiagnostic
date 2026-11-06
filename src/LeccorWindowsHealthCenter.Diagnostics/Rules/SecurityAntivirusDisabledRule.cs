using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Security;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta quando a proteção antivírus ativa está desabilitada no Windows (seção 30 do PROMPT).
/// </summary>
public sealed class SecurityAntivirusDisabledRule : IRule
{
    public string Id => "security.antivirus-disabled";

    public string Name => "Proteção antivírus desativada";

    public IReadOnlyList<string> RequiredScanners { get; } = ["security"];

    public IReadOnlyList<RuleParameter> Parameters { get; } = [];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var avList = context.Observations.Where(o => o.Kind == SecurityNames.ObservationAntivirus).ToList();
        if (avList.Count == 0)
        {
            yield break;
        }

        var anyActive = avList.Any(a => string.Equals(a.Attributes.GetValueOrDefault(SecurityNames.AttrEnabled), "true", StringComparison.OrdinalIgnoreCase));
        if (anyActive)
        {
            yield break;
        }

        var evidence = new List<Evidence>
        {
            new()
            {
                Source = EvidenceSource.Wmi,
                Description = "Nenhum produto antivírus registrado no Windows Security Center está com a proteção ativa ativada.",
                Reference = "SecurityCenter2",
                CollectedAt = DateTimeOffset.UtcNow
            }
        };

        yield return new Finding
        {
            Id = Guid.NewGuid(),
            RuleId = Id,
            ScannerId = "security",
            Category = FindingCategory.Windows,
            Severity = Severity.Critical,
            Confidence = Confidence.FromPercent(95),
            Title = "Proteção antivírus em tempo real desativada",
            Description = "O computador está operando sem nenhuma solução de antivírus ativa em tempo real (Windows Defender ou software de terceiros).",
            ProbableCause = "A proteção em tempo real foi pausada manualmente ou desativada por política de grupo ou script de otimização.",
            Impact = "Deixa o computador vulnerável à execução imediata de códigos maliciosos, ransomware e ameaças de rede.",
            Recommendation = "Abra o aplicativo Segurança do Windows e reative a Proteção contra vírus e ameaças.",
            Evidence = evidence,
            RequiresElevation = true,
            RequiresRestart = false,
            SupportsRollback = false,
            DetectedAt = DateTimeOffset.UtcNow
        };
    }
}

