using LeccorWindowsHealthCenter.Application.Events;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta erros críticos de hardware reportados pela arquitetura WHEA (Windows Hardware Error Architecture).
/// </summary>
public sealed class EventViewerHardwareWheaRule : IRule
{
    public string Id => "eventviewer.hardware-whea";

    public string Name => "Erros de hardware WHEA";

    public IReadOnlyList<string> RequiredScanners { get; } = ["eventviewer"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("Threshold", 1, "ocorrências", "Erros de hardware WHEA sinalizam instabilidade física de processador, memória ou barramento."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var threshold = (int)context.Parameter("Threshold");
        var eventObs = context.Observations.Where(o => o.Kind == EventViewerNames.ObservationKind);

        foreach (var obs in eventObs)
        {
            var provider = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrProviderName) ?? string.Empty;
            if (!provider.Contains("WHEA", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var countStr = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrCount24h) ?? "0";
            if (!int.TryParse(countStr, out var count) || count < threshold)
            {
                continue;
            }

            var eventId = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrEventId) ?? "Desconhecido";

            var evidence = new List<Evidence>(obs.Evidence)
            {
                new()
                {
                    Source = EvidenceSource.EventLog,
                    Description = $"{count} erro(s) WHEA (Evento {eventId}) registrados nas últimas 24h",
                    Value = count,
                    Unit = "eventos",
                    Reference = $"{provider}/{eventId}",
                    SampleCount = count,
                    CollectedAt = DateTimeOffset.UtcNow,
                },
            };

            yield return new Finding
            {
                Id = Guid.NewGuid(),
                RuleId = Id,
                ScannerId = "eventviewer",
                Category = FindingCategory.Events,
                Severity = Severity.Critical,
                Confidence = Confidence.FromPercent(95),
                Title = "Erros de hardware do computador detectados (WHEA)",
                Description = $"O Windows registrou {count} erro(s) de arquitetura de hardware WHEA (Evento {eventId}) nas últimas 24 horas.",
                ProbableCause = "Falha em pentes de memória RAM, degradação ou voltagem instável do processador, barramento PCIe com erro ou superaquecimento.",
                Impact = "Telas azuis repentinas (BSOD), corrupção silenciosa de dados ou congelamento total do sistema.",
                Recommendation = "Verificar integridade da memória RAM com diagnóstico de memória, checar temperaturas e redefinir overclock.",
                Evidence = evidence,
                RequiresElevation = false,
                RequiresRestart = false,
                SupportsRollback = false,
                DetectedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}
