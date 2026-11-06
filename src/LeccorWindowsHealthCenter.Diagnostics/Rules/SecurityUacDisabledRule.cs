using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Security;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta quando o Controle de Conta de Usuário (UAC) foi totalmente desativado (seção 30 do PROMPT).
/// </summary>
public sealed class SecurityUacDisabledRule : IRule
{
    public string Id => "security.uac-disabled";

    public string Name => "Controle de Conta de Usuário (UAC) desativado";

    public IReadOnlyList<string> RequiredScanners { get; } = ["security"];

    public IReadOnlyList<RuleParameter> Parameters { get; } = [];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var uacObs = context.Observations.FirstOrDefault(o => o.Kind == SecurityNames.ObservationUac);
        if (uacObs == null)
        {
            yield break;
        }

        var enabledStr = uacObs.Attributes.GetValueOrDefault(SecurityNames.AttrUacEnabled);
        if (string.Equals(enabledStr, "true", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var evidence = new List<Evidence>
        {
            new()
            {
                Source = EvidenceSource.Registry,
                Description = "A política de sistema 'EnableLUA' está configurada como 0 em HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System.",
                Reference = "EnableLUA",
                CollectedAt = DateTimeOffset.UtcNow
            }
        };

        yield return new Finding
        {
            Id = Guid.NewGuid(),
            RuleId = Id,
            ScannerId = "security",
            Category = FindingCategory.Windows,
            Severity = Severity.High,
            Confidence = Confidence.FromPercent(99),
            Title = "Controle de Conta de Usuário (UAC) está desativado",
            Description = "O Controle de Conta de Usuário (UAC) foi desativado. Todos os processos iniciados por administradores rodam com privilégios de alto nível sem aviso.",
            ProbableCause = "Alteração manual nas configurações do UAC ou modificação no registro por script de tweaks.",
            Impact = "Aplicativos e malwares podem executar comandos administrativos, instalar drivers e alterar arquivos do sistema sem confirmação na tela.",
            Recommendation = "Reativar o Controle de Conta de Usuário (UAC) pelo Painel de Controle para o nível padrão recomendado.",
            Evidence = evidence,
            RequiresElevation = true,
            RequiresRestart = true,
            SupportsRollback = false,
            DetectedAt = DateTimeOffset.UtcNow
        };
    }
}

