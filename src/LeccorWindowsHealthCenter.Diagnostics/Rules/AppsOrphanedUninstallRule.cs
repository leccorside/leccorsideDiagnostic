using LeccorWindowsHealthCenter.Application.Apps;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta programas instalados cujo executável de desinstalação foi apagado do disco (seção 28 do PROMPT).
/// </summary>
public sealed class AppsOrphanedUninstallRule : IRule
{
    public string Id => "apps.orphaned-uninstall";

    public string Name => "Registro de aplicativo órfão (desinstalador ausente)";

    public IReadOnlyList<string> RequiredScanners { get; } = ["apps"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("Enabled", 1, "booleano", "Identifica entradas em 'Programas e Recursos' cujo comando de desinstalação não existe mais."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var observations = context.Observations.Where(o => o.Kind == AppsNames.ObservationApp);

        foreach (var obs in observations)
        {
            var isOrphanedStr = obs.Attributes.GetValueOrDefault(AppsNames.AttrIsOrphaned);
            if (!bool.TryParse(isOrphanedStr, out var isOrphaned) || !isOrphaned)
            {
                continue;
            }

            var appName = obs.Subject;
            var version = obs.Attributes.GetValueOrDefault(AppsNames.AttrVersion);
            var publisher = obs.Attributes.GetValueOrDefault(AppsNames.AttrPublisher);
            var uninstallString = obs.Attributes.GetValueOrDefault(AppsNames.AttrUninstallString) ?? string.Empty;

            var evidence = new List<Evidence>
            {
                new()
                {
                    Source = EvidenceSource.Registry,
                    Description = $"O aplicativo '{appName}' está registrado no Windows, mas seu comando de desinstalação '{uninstallString}' aponta para um arquivo inexistente",
                    Reference = appName,
                    CollectedAt = DateTimeOffset.UtcNow,
                },
            };

            yield return new Finding
            {
                Id = Guid.NewGuid(),
                RuleId = Id,
                ScannerId = "apps",
                Category = FindingCategory.Applications,
                Severity = Severity.Low,
                Confidence = Confidence.FromPercent(95),
                Title = $"Desinstalador ausente para o aplicativo: {appName}",
                Description = $"O aplicativo '{appName}' {(string.IsNullOrWhiteSpace(version) ? "" : $"versão {version}")} permanece na lista de programas do Windows, mas seu arquivo de desinstalação foi removido do computador.",
                ProbableCause = "Exclusão manual da pasta do programa sem executar o assistente de desinstalação ou falha em atualizador de terceiros.",
                Impact = "Impede que o usuário desinstale o programa normalmente pelo Painel de Controle ou Configurações do Windows.",
                Recommendation = "Reinstalar o aplicativo para reparar o desinstalador ou remover a entrada residual do registro.",
                Evidence = evidence,
                RequiresElevation = true,
                RequiresRestart = false,
                SupportsRollback = true,
                DetectedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}

