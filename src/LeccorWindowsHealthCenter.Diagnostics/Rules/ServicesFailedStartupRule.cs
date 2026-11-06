using LeccorWindowsHealthCenter.Application.Events;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta serviços do Windows que falharam ao iniciar ou sofreram crash nas últimas 24h (seção 20 do PROMPT).
/// </summary>
public sealed class ServicesFailedStartupRule : IRule
{
    public string Id => "services.failed-startup";

    public string Name => "Serviço com falha de inicialização ou crash recente";

    public IReadOnlyList<string> RequiredScanners { get; } = ["eventviewer"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("FailureThreshold", 1, "ocorrências", "Qualquer falha de inicialização ou encerramento inesperado de serviço deve ser investigada."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var observations = context.Observations.Where(o => o.Kind == EventViewerNames.ObservationKind);

        foreach (var obs in observations)
        {
            var provider = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrProviderName) ?? string.Empty;
            if (!string.Equals(provider, "Service Control Manager", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var eventIdStr = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrEventId) ?? "0";
            if (!int.TryParse(eventIdStr, out var eventId))
            {
                continue;
            }

            // 7000: O serviço não pôde ser iniciado
            // 7009: Tempo limite aguardando conexão do serviço
            // 7024: O serviço foi finalizado com erro específico
            // 7031: O serviço foi encerrado inesperadamente (crash)
            // 7034: O serviço foi finalizado inesperadamente
            var isServiceError = eventId is 7000 or 7009 or 7024 or 7031 or 7034;
            if (!isServiceError)
            {
                continue;
            }

            var countStr = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrCount24h) ?? "1";
            _ = int.TryParse(countStr, out var count);
            if (count < 1)
            {
                count = 1;
            }

            var message = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrMessage) ?? string.Empty;
            var target = obs.Attributes.GetValueOrDefault(EventViewerNames.AttrTargetResource);
            var serviceName = !string.IsNullOrWhiteSpace(target) ? target : "Serviço do Windows";

            var evidence = new List<Evidence>
            {
                new()
                {
                    Source = EvidenceSource.EventLog,
                    Description = $"{count} falha(s) de serviço registrada(s) pelo Service Control Manager ({provider} / Evento {eventId})",
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
                Category = FindingCategory.Services,
                Severity = Severity.High,
                Confidence = Confidence.FromPercent(90),
                Title = $"Falha na execução de serviço do Windows ({serviceName})",
                Description = $"O Windows registrou falhas de inicialização ou terminação inesperada no serviço ({serviceName}) nas últimas 24 horas: {message}",
                ProbableCause = "Falha em dependências de rede/disco, permissões de conta de logon ou binário corrompido.",
                Impact = "Recursos dependentes deste serviço podem não funcionar, afetando aplicativos ou funcionalidades do sistema.",
                Recommendation = "Verificar os logs de eventos do serviço, verificar se as dependências estão ativas e reiniciar o serviço se necessário.",
                Evidence = evidence,
                RequiresElevation = true,
                RequiresRestart = false,
                SupportsRollback = false,
                DetectedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}

