using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Xml.Linq;
using LeccorWindowsHealthCenter.Application.Startup;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.Startup;

/// <summary>
/// Coleta eventos de degradação do boot do canal Microsoft-Windows-Diagnostics-Performance/Operational (seção 19 e Pacote A).
/// Extrai os Event IDs 101 a 110 (aplicativos, serviços, drivers e tarefas agendadas que atrasaram a inicialização).
/// Se o canal não estiver disponível ou faltar privilégio administrativo, retorna um mapa vazio sem falhar.
/// </summary>
public sealed class BootPerformanceProvider : IBootPerformanceProvider
{
    private const string ChannelName = "Microsoft-Windows-Diagnostics-Performance/Operational";
    private readonly ILogger<BootPerformanceProvider> _logger;

    public BootPerformanceProvider(ILogger<BootPerformanceProvider> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyDictionary<string, BootDegradationRecord>> GetRecentBootDegradationsAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, BootDegradationRecord>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Consulta eventos de degradação (EventID de 101 a 110)
            const string xpath = "*[System[(EventID >= 101 and EventID <= 110)]]";
            var query = new EventLogQuery(ChannelName, PathType.LogName, xpath)
            {
                ReverseDirection = true,
            };

            using var reader = new EventLogReader(query);
            EventRecord? record;
            var count = 0;
            const int maxEvents = 200;

            while ((record = reader.ReadEvent()) is not null && count++ < maxEvents)
            {
                using (record)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var degradation = ParseRecord(record);
                    if (degradation is not null)
                    {
                        // Guarda tanto pelo alvo exato (caminho completo) quanto pelo nome do binário
                        if (!result.ContainsKey(degradation.Target))
                        {
                            result[degradation.Target] = degradation;
                        }

                        var fileName = Path.GetFileName(degradation.Target);
                        if (!string.IsNullOrWhiteSpace(fileName) && !result.ContainsKey(fileName))
                        {
                            result[fileName] = degradation;
                        }
                    }
                }
            }

            _logger.LogInformation("Degradações de boot lidas com sucesso: {Count} registros mapeados.", result.Count);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogDebug(ex, "Leitura de '{ChannelName}' requer privilégios elevados. Métricas de boot ignoradas graciosamente.", ChannelName);
        }
        catch (EventLogNotFoundException ex)
        {
            _logger.LogDebug(ex, "Canal de eventos '{ChannelName}' não encontrado no sistema operacional.", ChannelName);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Falha ao ler telemetria de degradação de boot de '{ChannelName}'", ChannelName);
        }

        return Task.FromResult<IReadOnlyDictionary<string, BootDegradationRecord>>(result);
    }

    private static BootDegradationRecord? ParseRecord(EventRecord record)
    {
        try
        {
            var eventId = record.Id;
            var timestamp = record.TimeCreated ?? DateTimeOffset.UtcNow;
            var xml = record.ToXml();
            if (string.IsNullOrWhiteSpace(xml))
            {
                return null;
            }

            var doc = XDocument.Parse(xml);
            var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
            var eventData = doc.Descendants().FirstOrDefault(e => e.Name.LocalName is "EventData" or "UserData");
            if (eventData is null)
            {
                return null;
            }

            string? name = null;
            string? path = null;
            long totalTimeMs = 0;
            long degradationTimeMs = 0;

            foreach (var elem in eventData.Elements())
            {
                var localName = elem.Name.LocalName;
                var attrName = elem.Attribute("Name")?.Value ?? localName;
                var val = elem.Value?.Trim();

                if (string.Equals(attrName, "Name", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(attrName, "ApplicationName", StringComparison.OrdinalIgnoreCase))
                {
                    name = val;
                }
                else if (string.Equals(attrName, "Path", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(attrName, "ApplicationPath", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(attrName, "ImagePath", StringComparison.OrdinalIgnoreCase))
                {
                    path = val;
                }
                else if (string.Equals(attrName, "TotalTime", StringComparison.OrdinalIgnoreCase) && long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tt))
                {
                    totalTimeMs = tt;
                }
                else if (string.Equals(attrName, "DegradationTime", StringComparison.OrdinalIgnoreCase) && long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dt))
                {
                    degradationTimeMs = dt;
                }
            }

            var target = !string.IsNullOrWhiteSpace(path) ? path : name;
            if (string.IsNullOrWhiteSpace(target))
            {
                return null;
            }

            return new BootDegradationRecord
            {
                Target = target,
                DegradationTimeMs = degradationTimeMs,
                TotalTimeMs = totalTimeMs,
                EventId = eventId,
                Timestamp = timestamp,
            };
        }
        catch
        {
            return null;
        }
    }
}

