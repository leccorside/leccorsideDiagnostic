using LeccorWindowsHealthCenter.Application.Events;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta aplicativos com falhas repetidas nas últimas 24h (Application Error 1000).
/// </summary>
public sealed class EventViewerAppCrashRule : IRule
{
    public string Id => "eventviewer.app-crash-recurring";

    public string Name => "Falhas recorrentes em aplicativo";

    public IReadOnlyList<string> RequiredScanners { get; } = ["eventviewer"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("Threshold", 3, "falhas em 24h", "Uma única falha pode ser pontual; três ou mais em 24h caracterizam instabilidade real no programa."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var threshold = (int)context.Parameter("Threshold");
        var eventObs = context.Observations.Where(o => o.Kind == EventViewerNames.ObservationKind);

        foreach (var obs in eventObs)
        {
            var provider = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrProviderName) ?? string.Empty;
            var eventIdStr = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrEventId) ?? "0";
            if (!int.TryParse(eventIdStr, out var eventId))
            {
                continue;
            }

            if (!string.Equals(provider, "Application Error", StringComparison.OrdinalIgnoreCase) || eventId != 1000)
            {
                continue;
            }

            var countStr = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrCount24h) ?? "0";
            if (!int.TryParse(countStr, out var count) || count < threshold)
            {
                continue;
            }

            var target = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrTargetResource);
            var appName = string.IsNullOrWhiteSpace(target) ? "Aplicativo" : target;

            var evidence = new List<Evidence>(obs.Evidence)
            {
                new()
                {
                    Source = EvidenceSource.EventLog,
                    Description = $"{count} falha(s) do aplicativo {appName} registradas no log Application nas últimas 24h",
                    Value = count,
                    Unit = "falhas",
                    Reference = $"{appName} (Evento 1000)",
                    SampleCount = count,
                    CollectedAt = DateTimeOffset.UtcNow,
                },
            };

            yield return new Finding
            {
                Id = Guid.NewGuid(),
                RuleId = Id,
                ScannerId = "eventviewer",
                Category = FindingCategory.Applications,
                Severity = Severity.Medium,
                Confidence = Confidence.FromPercent(85),
                Title = $"Falhas recorrentes no aplicativo {appName}",
                Description = $"O aplicativo {appName} apresentou {count} travamentos ou encerramentos anormais nas últimas 24 horas.",
                ProbableCause = "Incompatibilidade com o Windows, biblioteca corrompida, conflito de extensões ou bug interno do aplicativo.",
                Impact = "Interrupção do trabalho, fechamento involuntário do programa e potencial perda de progresso.",
                Recommendation = "Verificar atualizações do software, reparar a instalação ou reinstalar o aplicativo.",
                Evidence = evidence,
                RequiresElevation = false,
                RequiresRestart = false,
                SupportsRollback = false,
                DetectedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}
