namespace LeccorWindowsHealthCenter.Application.Events;

public static class EventViewerNames
{
    public const string ObservationKind = "eventviewer.group";

    public const string AttrLogName = "log";
    public const string AttrProviderName = "provider";
    public const string AttrEventId = "eventId";
    public const string AttrLevel = "level";
    public const string AttrCount24h = "count24h";
    public const string AttrFirstSeen = "firstSeen";
    public const string AttrLastSeen = "lastSeen";
    public const string AttrMessage = "message";
    public const string AttrTargetResource = "targetResource";
    public const string AttrTechnicalDetails = "technicalDetails";
}

/// <summary>Resumo agrupado de eventos por origem e ID nas últimas 24 horas (seção 21 do PROMPT).</summary>
public sealed record EventGroupSummary
{
    public required string LogName { get; init; }

    public required string ProviderName { get; init; }

    public required int EventId { get; init; }

    public required string Level { get; init; }

    public required int Count24h { get; init; }

    public required DateTimeOffset FirstSeen { get; init; }

    public required DateTimeOffset LastSeen { get; init; }

    public required string Message { get; init; }

    public string? TargetResource { get; init; }

    public string? TechnicalDetails { get; init; }
}

public sealed class EventViewerOptions
{
    public const string SectionName = "EventViewer";

    /// <summary>Janela de análise em horas (padrão 24h, seção 21).</summary>
    public int WindowHours { get; set; } = 24;

    /// <summary>Máximo de eventos lidos por log para não causar sobrecarga.</summary>
    public int MaxEventsPerLog { get; set; } = 2000;
}

