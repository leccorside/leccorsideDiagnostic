using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Services;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta serviços configurados com inicialização automática cujo executável não existe no disco (seção 20 do PROMPT).
/// </summary>
public sealed class ServicesMissingExecutableRule : IRule
{
    public string Id => "services.missing-executable";

    public string Name => "Serviço automático órfão (executável ausente)";

    public IReadOnlyList<string> RequiredScanners { get; } = ["services"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("Enabled", 1, "booleano", "Identifica serviços que tentam iniciar automaticamente mas o arquivo binário foi apagado."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var observations = context.Observations.Where(o => o.Kind == WindowsServiceNames.ObservationService);

        foreach (var obs in observations)
        {
            var existsStr = obs.Attributes.GetValueOrDefault(WindowsServiceNames.AttrExists);
            if (!bool.TryParse(existsStr, out var exists) || exists)
            {
                continue;
            }

            var startupType = obs.Attributes.GetValueOrDefault(WindowsServiceNames.AttrStartupType);
            var isAutomatic = string.Equals(startupType, "Automatic", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(startupType, "AutomaticDelayed", StringComparison.OrdinalIgnoreCase);

            if (!isAutomatic)
            {
                continue;
            }

            var serviceName = obs.Subject;
            var displayName = obs.Attributes.GetValueOrDefault(WindowsServiceNames.AttrDisplayName) ?? serviceName;
            var exePath = obs.Attributes.GetValueOrDefault(WindowsServiceNames.AttrExecutablePath) ?? string.Empty;

            var evidence = new List<Evidence>
            {
                new()
                {
                    Source = EvidenceSource.Registry,
                    Description = $"O serviço '{displayName}' ({serviceName}) está configurado como Automático, mas seu executável '{exePath}' não existe",
                    Reference = serviceName,
                    CollectedAt = DateTimeOffset.UtcNow,
                },
            };

            yield return new Finding
            {
                Id = Guid.NewGuid(),
                RuleId = Id,
                ScannerId = "services",
                Category = FindingCategory.Services,
                Severity = Severity.Medium,
                Confidence = Confidence.FromPercent(95),
                Title = $"Serviço automático com executável ausente: {displayName}",
                Description = $"O serviço '{displayName}' ({serviceName}) tenta ser iniciado a cada boot do Windows, mas o arquivo '{exePath}' não foi encontrado no computador.",
                ProbableCause = "Desinstalação incompleta de aplicativo ou arquivo apagado manualmente.",
                Impact = "Gera erros repetidos no Visualizador de Eventos (Event ID 7000) e consome tempo no processo de inicialização do Windows tentando iniciar um binário inexistente.",
                Recommendation = "Desativar o serviço ou desinstalá-lo se a aplicação não for mais utilizada.",
                Evidence = evidence,
                RequiresElevation = true,
                RequiresRestart = false,
                SupportsRollback = true,
                DetectedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}
