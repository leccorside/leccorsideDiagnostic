using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Security;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta persistências suspeitas via Image File Execution Options (IFEO Debugger Hijack) (seção 30 do PROMPT).
/// </summary>
public sealed class SecurityIfeoHijackRule : IRule
{
    public string Id => "security.ifeo-hijack";

    public string Name => "Redirecionamento de depuração suspeito (IFEO Hijack)";

    public IReadOnlyList<string> RequiredScanners { get; } = ["security"];

    public IReadOnlyList<RuleParameter> Parameters { get; } = [];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var persistences = context.Observations.Where(o => o.Kind == SecurityNames.ObservationPersistence);

        foreach (var p in persistences)
        {
            var isSuspicious = string.Equals(p.Attributes.GetValueOrDefault(SecurityNames.AttrSuspicious), "true", StringComparison.OrdinalIgnoreCase);
            if (!isSuspicious)
            {
                continue;
            }

            var type = p.Attributes.GetValueOrDefault(SecurityNames.AttrPersistenceType) ?? "Persistência";
            var target = p.Subject;

            var evidence = new List<Evidence>
            {
                new()
                {
                    Source = EvidenceSource.Registry,
                    Description = $"Chave de inicialização suspeita do tipo {type} detectada no alvo '{target}'.",
                    Reference = target,
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
                Title = $"Persistência suspeita detectada: {target}",
                Description = $"Foi detectada uma configuração de redirecionamento de execução ({type}) no executável do sistema '{target}'.",
                ProbableCause = "Técnica comum de persistência e bypass de segurança utilizada por malwares ou ferramentas de acesso não autorizado.",
                Impact = "Permite que um binário alternativo seja disparado no lugar de ferramentas críticas de acessibilidade ou do sistema.",
                Recommendation = "Examinar a chave no Editor do Registro e remover o valor Debugger associado caso não tenha sido intencional.",
                Evidence = evidence,
                RequiresElevation = true,
                RequiresRestart = false,
                SupportsRollback = false,
                DetectedAt = DateTimeOffset.UtcNow
            };
        }
    }
}

