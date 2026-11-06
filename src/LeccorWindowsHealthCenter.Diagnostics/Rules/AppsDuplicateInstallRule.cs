using LeccorWindowsHealthCenter.Application.Apps;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta programas com múltiplas versões ou arquiteturas diferentes instaladas simultaneamente (seção 28 do PROMPT).
/// </summary>
public sealed class AppsDuplicateInstallRule : IRule
{
    public string Id => "apps.duplicate-install";

    public string Name => "Versões duplicadas do mesmo aplicativo";

    public IReadOnlyList<string> RequiredScanners { get; } = ["apps"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("Enabled", 1, "booleano", "Identifica programas instalados mais de uma vez com versões distintas."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var observations = context.Observations
            .Where(o => o.Kind == AppsNames.ObservationApp)
            .ToList();

        var duplicates = observations
            .Where(o =>
            {
                var flag = o.Attributes.GetValueOrDefault(AppsNames.AttrHasDuplicate);
                return bool.TryParse(flag, out var hasDup) && hasDup;
            })
            .GroupBy(o => o.Subject, StringComparer.OrdinalIgnoreCase);

        foreach (var group in duplicates)
        {
            var appName = group.Key;
            var versions = group.Select(g => g.Attributes.GetValueOrDefault(AppsNames.AttrVersion) ?? "desconhecida").Distinct().ToList();

            var evidence = new List<Evidence>
            {
                new()
                {
                    Source = EvidenceSource.Registry,
                    Description = $"Foram encontradas {group.Count()} entradas registradas para '{appName}': versões {string.Join(", ", versions)}",
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
                Severity = Severity.Info,
                Confidence = Confidence.FromPercent(90),
                Title = $"Múltiplas versões instaladas do aplicativo: {appName}",
                Description = $"O computador possui mais de uma instalação registrada do aplicativo '{appName}' ({string.Join(" e ", versions)}).",
                ProbableCause = "Instalação de atualização que não removeu a versão legada ou instalação concomitante de arquiteturas 32 e 64 bits.",
                Impact = "Consumo redundante de espaço em disco e possível conflito de associações de arquivos.",
                Recommendation = "Desinstalar a versão legada do aplicativo através do Painel de Controle se ela não for necessária.",
                Evidence = evidence,
                RequiresElevation = false,
                RequiresRestart = false,
                SupportsRollback = false,
                DetectedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}

