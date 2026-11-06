using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;
using LeccorWindowsHealthCenter.Application.Events;
using LeccorWindowsHealthCenter.Shared.Security;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.Events;

/// <summary>
/// Coleta e agrupa eventos dos logs do Windows (System, Application, Setup, WHEA, Windows Update)
/// das últimas 24 horas (seção 21 do PROMPT).
/// </summary>
public sealed class EventViewerProvider : IEventViewerProvider
{
    private readonly ILogger<EventViewerProvider> _logger;

    private static readonly string[] MonitoredLogs =
    [
        "System",
        "Application",
        "Setup",
        "Microsoft-Windows-WHEA-Logger/Operational",
        "Microsoft-Windows-WindowsUpdateClient/Operational",
    ];

    public EventViewerProvider(ILogger<EventViewerProvider> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<EventGroupSummary>> GetRecentEventGroupsAsync(TimeSpan window, CancellationToken cancellationToken)
    {
        var windowMs = (long)Math.Max(window.TotalMilliseconds, 60_000);
        var groups = new Dictionary<(string Log, string Provider, int EventId, string Level), EventAccumulator>();

        foreach (var logName in MonitoredLogs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadLog(logName, windowMs, groups, cancellationToken);
        }

        var result = groups.Values
            .Select(acc => new EventGroupSummary
            {
                LogName = acc.LogName,
                ProviderName = acc.ProviderName,
                EventId = acc.EventId,
                Level = acc.Level,
                Count24h = acc.Count,
                FirstSeen = acc.FirstSeen,
                LastSeen = acc.LastSeen,
                Message = SensitiveDataRedactor.Redact(acc.SampleMessage),
                TargetResource = acc.TargetResource,
                TechnicalDetails = SensitiveDataRedactor.Redact(acc.TechnicalDetails ?? string.Empty),
            })
            .OrderByDescending(g => GetLevelSeverity(g.Level))
            .ThenByDescending(g => g.Count24h)
            .ToList();

        return Task.FromResult<IReadOnlyList<EventGroupSummary>>(result);
    }

    private void ReadLog(
        string logName,
        long windowMs,
        Dictionary<(string Log, string Provider, int EventId, string Level), EventAccumulator> groups,
        CancellationToken cancellationToken)
    {
        // Consulta eventos com Nível <= 3 (1=Crítico, 2=Erro, 3=Aviso) no período solicitado
        var xpath = $"*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[timediff(@SystemTime) <= {windowMs}]]]";
        EventLogQuery query;

        try
        {
            query = new EventLogQuery(logName, PathType.LogName, xpath)
            {
                ReverseDirection = true,
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Log de eventos '{LogName}' não pôde ser consultado", logName);
            return;
        }

        try
        {
            using var reader = new EventLogReader(query);
            var readCount = 0;
            const int maxEvents = 2500;

            EventRecord? record;
            while ((record = reader.ReadEvent()) is not null && readCount++ < maxEvents)
            {
                using (record)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var provider = record.ProviderName ?? "Desconhecido";
                    var eventId = record.Id;
                    var level = FormatLevel(record.Level);
                    var time = record.TimeCreated ?? DateTimeOffset.UtcNow;
                    var key = (logName, provider, eventId, level);

                    if (!groups.TryGetValue(key, out var acc))
                    {
                        var (message, targetResource, details) = ExtractDetails(record);
                        acc = new EventAccumulator(logName, provider, eventId, level, time, message, targetResource, details);
                        groups[key] = acc;
                    }
                    else
                    {
                        acc.AddOccurrence(time);
                    }
                }
            }
        }
        catch (EventLogNotFoundException)
        {
            _logger.LogDebug("Canal de log '{LogName}' não existe nesta edição do Windows", logName);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogDebug("Acesso negado ao log '{LogName}'", logName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao ler eventos do canal '{LogName}'", logName);
        }
    }

    private static (string Message, string? TargetResource, string? Details) ExtractDetails(EventRecord record)
    {
        string? message = null;
        try
        {
            message = record.FormatDescription();
        }
        catch
        {
            // FormatDescription pode falhar se os recursos da mensagem não estiverem registrados
        }

        var targetResource = ExtractTargetResource(record, message);
        var details = ExtractXmlOrProperties(record);

        if (string.IsNullOrWhiteSpace(message))
        {
            message = $"Evento {record.Id} emitido por {record.ProviderName}.";
        }

        // Limita tamanho da mensagem principal
        if (message.Length > 500)
        {
            message = string.Concat(message.AsSpan(0, 497), "...");
        }

        return (message, targetResource, details);
    }

    private static string? ExtractTargetResource(EventRecord record, string? message)
    {
        var provider = record.ProviderName ?? string.Empty;

        // Disk e Storage (ex.: Disk 153, Disk 7, NTFS 55)
        if (string.Equals(provider, "disk", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(provider, "storahci", StringComparison.OrdinalIgnoreCase))
        {
            if (message is not null)
            {
                var match = Regex.Match(message, @"\\Device\\Harddisk\d+\\DR\d+|\\Device\\Harddisk\d+|Disk\s+\d+|Disco\s+\d+", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Value;
                }
            }
            return "Armazenamento";
        }

        if (string.Equals(provider, "Ntfs", StringComparison.OrdinalIgnoreCase))
        {
            if (message is not null)
            {
                var match = Regex.Match(message, @"volume\s+([A-Z]:|[^\s,]+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Value;
                }
            }
            return "Volume NTFS";
        }

        // Display / GPU (Event 4101)
        if (string.Equals(provider, "Display", StringComparison.OrdinalIgnoreCase))
        {
            if (message is not null)
            {
                var match = Regex.Match(message, @"(nvlddmkm|amdkmdag|igfxn|igfx)\b", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Value;
                }
            }
            return "Driver de Vídeo";
        }

        // Application Error (Event 1000)
        if (string.Equals(provider, "Application Error", StringComparison.OrdinalIgnoreCase))
        {
            if (message is not null)
            {
                var match = Regex.Match(message, @"(?:Nome do aplicativo com falha|Faulting application name):\s*([^\s,]+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            return "Aplicativo";
        }

        return null;
    }

    private static string? ExtractXmlOrProperties(EventRecord record)
    {
        try
        {
            var xml = record.ToXml();
            if (!string.IsNullOrWhiteSpace(xml) && xml.Length > 2000)
            {
                return string.Concat(xml.AsSpan(0, 1997), "...");
            }
            return xml;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatLevel(byte? level) => level switch
    {
        1 => "Crítico",
        2 => "Erro",
        3 => "Aviso",
        4 => "Informação",
        _ => "Informação",
    };

    private static int GetLevelSeverity(string level) => level switch
    {
        "Crítico" => 4,
        "Erro" => 3,
        "Aviso" => 2,
        _ => 1,
    };

    private sealed class EventAccumulator
    {
        public string LogName { get; }
        public string ProviderName { get; }
        public int EventId { get; }
        public string Level { get; }
        public int Count { get; private set; }
        public DateTimeOffset FirstSeen { get; private set; }
        public DateTimeOffset LastSeen { get; private set; }
        public string SampleMessage { get; }
        public string? TargetResource { get; }
        public string? TechnicalDetails { get; }

        public EventAccumulator(
            string logName,
            string providerName,
            int eventId,
            string level,
            DateTimeOffset time,
            string message,
            string? targetResource,
            string? details)
        {
            LogName = logName;
            ProviderName = providerName;
            EventId = eventId;
            Level = level;
            Count = 1;
            FirstSeen = time;
            LastSeen = time;
            SampleMessage = message;
            TargetResource = targetResource;
            TechnicalDetails = details;
        }

        public void AddOccurrence(DateTimeOffset time)
        {
            Count++;
            if (time < FirstSeen)
            {
                FirstSeen = time;
            }

            if (time > LastSeen)
            {
                LastSeen = time;
            }
        }
    }
}
