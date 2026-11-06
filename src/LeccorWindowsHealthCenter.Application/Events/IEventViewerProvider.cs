namespace LeccorWindowsHealthCenter.Application.Events;

/// <summary>Provedor de eventos do Windows Event Viewer agrupados e filtrados por relevância e janela de tempo.</summary>
public interface IEventViewerProvider
{
    Task<IReadOnlyList<EventGroupSummary>> GetRecentEventGroupsAsync(TimeSpan window, CancellationToken cancellationToken);
}

