using System.Globalization;
using LeccorWindowsHealthCenter.Application.Events;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Eventos recorrentes de I/O de disco (Disk 153/7/11, storahci 129, NTFS 55/137) nas últimas 24h (seções 21 e 44 do PROMPT).
/// </summary>
public sealed class EventViewerDiskIoErrorsRule : IRule
{
    public string Id => "eventviewer.disk-io-errors";

    public string Name => "Erros de I/O ou comunicação com armazenamento";

    public IReadOnlyList<string> RequiredScanners { get; } = ["eventviewer"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("WarningThreshold", 1, "ocorrências", "Qualquer erro de setor ou estrutura corrompida de disco já requer atenção preventiva."),
        new("CriticalThreshold", 5, "ocorrências", "Erros de repetição de I/O em grande quantidade indicam falha iminente de unidade ou cabo defeituoso."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var warnThreshold = (int)context.Parameter("WarningThreshold");
        var critThreshold = (int)context.Parameter("CriticalThreshold");

        var eventObs = context.Observations.Where(o => o.Kind == EventViewerNames.ObservationKind);

        foreach (var obs in eventObs)
        {
            var provider = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrProviderName) ?? string.Empty;
            var eventIdStr = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrEventId) ?? "0";
            if (!int.TryParse(eventIdStr, out var eventId))
            {
                continue;
            }

            var isDiskEvent = (string.Equals(provider, "disk", StringComparison.OrdinalIgnoreCase) && eventId is 7 or 11 or 15 or 153) ||
                              (string.Equals(provider, "storahci", StringComparison.OrdinalIgnoreCase) && eventId is 129) ||
                              (string.Equals(provider, "Ntfs", StringComparison.OrdinalIgnoreCase) && eventId is 55 or 137 or 140);

            if (!isDiskEvent)
            {
                continue;
            }

            var countStr = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrCount24h) ?? "0";
            if (!int.TryParse(countStr, out var count) || count < warnThreshold)
            {
                continue;
            }

            var target = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrTargetResource);
            var resourceName = string.IsNullOrWhiteSpace(target) ? "Disco" : target;
            var message = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrMessage) ?? string.Empty;

            var isCritical = eventId is 7 or 11 or 55 || count >= critThreshold;
            var severity = isCritical ? Severity.Critical : Severity.High;
            var confidence = isCritical ? Confidence.FromPercent(95) : Confidence.FromPercent(85);

            var title = $"Foram detectados eventos recorrentes de I/O ({resourceName})";

            var evidence = new List<Evidence>(obs.Evidence)
            {
                new()
                {
                    Source = EvidenceSource.EventLog,
                    Description = $"{count} eventos {provider} {eventId} registrados nas últimas 24 horas",
                    Value = count,
                    Unit = "eventos",
                    Reference = $"{provider} / {eventId}",
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
                Severity = severity,
                Confidence = confidence,
                Title = title,
                Description = $"{count} erro(s) de comunicação ou integridade de armazenamento registrados pelo provedor {provider} (Evento {eventId}) nas últimas 24h.",
                ProbableCause = "Falha física na unidade, desgaste de mídia, cabo SATA/NVMe com mal contato ou corrupção de estrutura NTFS.",
                Impact = "Degradação extrema de desempenho de disco, travamentos do sistema ou risco iminente de perda de dados.",
                Recommendation = "Verificar integridade física e conexões do armazenamento e realizar backup imediatamente.",
                Evidence = evidence,
                RequiresElevation = false,
                RequiresRestart = false,
                SupportsRollback = false,
                DetectedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}
