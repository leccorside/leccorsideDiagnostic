using LeccorWindowsHealthCenter.Application.Events;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta desligamentos inesperados, perda repentina de energia ou telas azuis (Kernel-Power 41 / EventLog 6008).
/// </summary>
public sealed class EventViewerUnexpectedShutdownRule : IRule
{
    public string Id => "eventviewer.unexpected-shutdown";

    public string Name => "Desligamento inesperado do sistema";

    public IReadOnlyList<string> RequiredScanners { get; } = ["eventviewer"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("Threshold", 1, "ocorrências", "Desligamentos anormais indicam falha na fonte, corte de energia ou tela azul sem encerramento limpo."),
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

            var isUnexpectedShutdown = (string.Equals(provider, "Microsoft-Windows-Kernel-Power", StringComparison.OrdinalIgnoreCase) && eventId == 41) ||
                                       (string.Equals(provider, "EventLog", StringComparison.OrdinalIgnoreCase) && eventId == 6008);

            if (!isUnexpectedShutdown)
            {
                continue;
            }

            var countStr = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrCount24h) ?? "0";
            if (!int.TryParse(countStr, out var count) || count < threshold)
            {
                continue;
            }

            var evidence = new List<Evidence>(obs.Evidence)
            {
                new()
                {
                    Source = EvidenceSource.EventLog,
                    Description = $"{count} desligamento(s) inesperado(s) registrado(s) nas últimas 24 horas ({provider} / {eventId})",
                    Value = count,
                    Unit = "ocorrências",
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
                Severity = Severity.High,
                Confidence = Confidence.FromPercent(90),
                Title = "Desligamento inesperado do computador nas últimas 24h",
                Description = $"O Windows registrou {count} reinicialização(ões) ou desligamento(s) sem encerramento limpo ({provider} / Evento {eventId}).",
                ProbableCause = "Queda súbita de energia, defeito na fonte de alimentação, superaquecimento ou falha crítica de driver (BugCheck).",
                Impact = "Possível corrupção de arquivos abertos no momento do desligamento e desgaste de discos.",
                Recommendation = "Verificar estabilidade da energia/alimentação, verificar se há arquivos de minidump (despejos) e monitorar temperaturas.",
                Evidence = evidence,
                RequiresElevation = false,
                RequiresRestart = false,
                SupportsRollback = false,
                DetectedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}
