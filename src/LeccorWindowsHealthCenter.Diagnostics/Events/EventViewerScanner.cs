using System.Globalization;
using LeccorWindowsHealthCenter.Application.Events;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Diagnostics.Events;

/// <summary>
/// Analisa os logs do Visualizador de Eventos (System, Application, Setup, WHEA, Windows Update)
/// das últimas 24h, agrupando erros repetidos (seção 21 do PROMPT).
/// </summary>
public sealed class EventViewerScanner : DiagnosticScannerBase
{
    public const string ScannerId = "eventviewer";

    private readonly IEventViewerProvider _provider;

    public EventViewerScanner(IEventViewerProvider provider, ILogger<EventViewerScanner> logger)
        : base(logger)
    {
        _provider = provider;
    }

    public override string Id => "eventviewer";

    public override string Name => "Event Viewer";

    public override ScanModule Module => ScanModule.Events;

    public override bool IncludedInQuickScan => true;

    public override int Order => 75;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var groups = await _provider.GetRecentEventGroupsAsync(TimeSpan.FromHours(24), cancellationToken).ConfigureAwait(false);

        var criticalTotal = 0;
        var errorTotal = 0;
        var warningTotal = 0;

        foreach (var group in groups)
        {
            if (string.Equals(group.Level, "Crítico", StringComparison.OrdinalIgnoreCase))
            {
                criticalTotal += group.Count24h;
            }
            else if (string.Equals(group.Level, "Erro", StringComparison.OrdinalIgnoreCase))
            {
                errorTotal += group.Count24h;
            }
            else if (string.Equals(group.Level, "Aviso", StringComparison.OrdinalIgnoreCase))
            {
                warningTotal += group.Count24h;
            }

            var attributes = new Dictionary<string, string>
            {
                [EventViewerNames.AttrLogName] = group.LogName,
                [EventViewerNames.AttrProviderName] = group.ProviderName,
                [EventViewerNames.AttrEventId] = group.EventId.ToString(CultureInfo.InvariantCulture),
                [EventViewerNames.AttrLevel] = group.Level,
                [EventViewerNames.AttrCount24h] = group.Count24h.ToString(CultureInfo.InvariantCulture),
                [EventViewerNames.AttrFirstSeen] = group.FirstSeen.ToString("o"),
                [EventViewerNames.AttrLastSeen] = group.LastSeen.ToString("o"),
                [EventViewerNames.AttrMessage] = group.Message,
                [EventViewerNames.AttrTargetResource] = group.TargetResource ?? string.Empty,
                [EventViewerNames.AttrTechnicalDetails] = group.TechnicalDetails ?? string.Empty,
            };

            var evidence = new List<Evidence>
            {
                new()
                {
                    Source = EvidenceSource.EventLog,
                    Description = $"{group.LogName} / {group.ProviderName} (Evento {group.EventId})",
                    Value = group.Count24h,
                    Unit = "ocorrências em 24h",
                    Reference = $"{group.LogName}/{group.ProviderName}/{group.EventId}",
                    SampleCount = group.Count24h,
                    CollectedAt = group.LastSeen,
                },
            };

            collector.AddObservation(
                EventViewerNames.ObservationKind,
                $"{group.ProviderName} / Evento {group.EventId}: {group.Count24h} ocorrência(s) nas últimas 24h ({group.Level})",
                attributes,
                evidence);
        }

        collector.AddMetric("events.critical.count", criticalTotal, "eventos");
        collector.AddMetric("events.error.count", errorTotal, "eventos");
        collector.AddMetric("events.warning.count", warningTotal, "eventos");
    }
}
