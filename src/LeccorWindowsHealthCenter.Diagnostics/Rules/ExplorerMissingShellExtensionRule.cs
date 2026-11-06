using LeccorWindowsHealthCenter.Application.Explorer;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta extensões do Windows Explorer ativas registradas cujo arquivo DLL não existe no disco (seção 16 do PROMPT).
/// </summary>
public sealed class ExplorerMissingShellExtensionRule : IRule
{
    public string Id => "explorer.missing-shell-extension";

    public string Name => "Extensão do Windows Explorer órfã (DLL inexistente)";

    public IReadOnlyList<string> RequiredScanners { get; } = ["explorer"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("Enabled", 1, "booleano", "Ativa ou desativa a verificação de extensões de Shell órfãs."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var observations = context.Observations.Where(o => o.Kind == ExplorerNames.ObservationShellExtension);

        foreach (var obs in observations)
        {
            var existsStr = obs.Attributes.GetValueOrDefault(ExplorerNames.AttrExists);
            if (!bool.TryParse(existsStr, out var exists) || exists)
            {
                continue;
            }

            var blockedStr = obs.Attributes.GetValueOrDefault(ExplorerNames.AttrBlocked);
            if (bool.TryParse(blockedStr, out var blocked) && blocked)
            {
                continue; // Se já está bloqueada pelo sistema, não causa lentidão
            }

            var name = obs.Subject;
            var clsid = obs.Attributes.GetValueOrDefault(ExplorerNames.AttrClsid) ?? string.Empty;
            var dllPath = obs.Attributes.GetValueOrDefault(ExplorerNames.AttrDllPath) ?? string.Empty;
            var type = obs.Attributes.GetValueOrDefault(ExplorerNames.AttrType) ?? "ContextMenu";

            var evidence = new List<Evidence>
            {
                new()
                {
                    Source = EvidenceSource.Registry,
                    Description = $"A extensão de Shell '{name}' aponta para uma biblioteca que não existe: '{dllPath}'",
                    Reference = clsid,
                    CollectedAt = DateTimeOffset.UtcNow,
                },
            };

            yield return new Finding
            {
                Id = Guid.NewGuid(),
                RuleId = Id,
                ScannerId = "explorer",
                Category = FindingCategory.Explorer,
                Severity = Severity.Low,
                Confidence = Confidence.FromPercent(95),
                Title = $"Extensão do Windows Explorer órfã: {name}",
                Description = $"A extensão de Shell do tipo {type} ({name}) está registrada no Windows, mas seu arquivo DLL '{dllPath}' foi removido do disco.",
                ProbableCause = "Desinstalação incompleta de aplicativo de terceiros que deixou registros residuais de integração com o Explorer.",
                Impact = "Pode causar atrasos de vários segundos ou travamento ao clicar com botão direito ou abrir pastas no Windows Explorer enquanto o sistema tenta carregar a biblioteca inexistente.",
                Recommendation = "Desativar ou remover o registro órfão do menu de contexto/Shell Extension.",
                RepairActionId = "repair.explorer.disable-shell-extension",
                RepairRisk = RiskLevel.Low,
                Evidence = evidence,
                RequiresElevation = false,
                RequiresRestart = false,
                SupportsRollback = true,
                DetectedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}

