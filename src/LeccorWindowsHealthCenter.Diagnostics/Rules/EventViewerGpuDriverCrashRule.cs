using LeccorWindowsHealthCenter.Application.Events;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta recuperações de travamento do driver de vídeo (TDR / Display Event 4101) nas últimas 24h.
/// </summary>
public sealed class EventViewerGpuDriverCrashRule : IRule
{
    public string Id => "eventviewer.gpu-driver-crash";

    public string Name => "Travamento e recuperação do driver de vídeo (TDR)";

    public IReadOnlyList<string> RequiredScanners { get; } = ["eventviewer"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("WarningThreshold", 1, "ocorrências", "Mesmo uma ocorrência isolada indica instabilidade no subsistema gráfico do Windows."),
        new("HighThreshold", 3, "ocorrências", "Três ou mais travamentos em 24h indicam incompatibilidade de driver, superaquecimento ou falha de hardware."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var warnThreshold = (int)context.Parameter("WarningThreshold");
        var highThreshold = (int)context.Parameter("HighThreshold");

        var eventObs = context.Observations.Where(o => o.Kind == EventViewerNames.ObservationKind);

        foreach (var obs in eventObs)
        {
            var provider = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrProviderName) ?? string.Empty;
            var eventIdStr = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrEventId) ?? "0";
            if (!int.TryParse(eventIdStr, out var eventId))
            {
                continue;
            }

            var isGpuCrash = (string.Equals(provider, "Display", StringComparison.OrdinalIgnoreCase) && eventId == 4101) ||
                             (string.Equals(provider, "Microsoft-Windows-DxgKrnl", StringComparison.OrdinalIgnoreCase) && eventId is 141 or 142 or 144);

            if (!isGpuCrash)
            {
                continue;
            }

            var countStr = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrCount24h) ?? "0";
            if (!int.TryParse(countStr, out var count) || count < warnThreshold)
            {
                continue;
            }

            var severity = count >= highThreshold ? Severity.High : Severity.Medium;
            var target = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrTargetResource);
            var driverName = string.IsNullOrWhiteSpace(target) ? "Driver de vídeo" : target;

            var evidence = new List<Evidence>(obs.Evidence)
            {
                new()
                {
                    Source = EvidenceSource.EventLog,
                    Description = $"{count} evento(s) de recuperação de driver gráfico (Display 4101 / TDR) nas últimas 24 horas",
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
                Severity = severity,
                Confidence = Confidence.FromPercent(90),
                Title = $"O driver de vídeo parou de responder e foi recuperado ({driverName})",
                Description = $"O Windows detectou {count} evento(s) de Timeout Detection and Recovery (TDR) no driver gráfico nas últimas 24 horas.",
                ProbableCause = "Versão instável do driver, superaquecimento da placa de vídeo, overclock agressivo ou conflito de aceleração de hardware.",
                Impact = "Telas pretas momentâneas, fechamento inesperado de jogos ou programas de aceleração gráfica.",
                Recommendation = "Reinstalar o driver de vídeo de forma limpa e verificar a temperatura e estabilidade da GPU.",
                Evidence = evidence,
                RequiresElevation = false,
                RequiresRestart = false,
                SupportsRollback = false,
                DetectedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}
