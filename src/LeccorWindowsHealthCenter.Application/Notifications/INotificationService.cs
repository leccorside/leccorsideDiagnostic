namespace LeccorWindowsHealthCenter.Application.Notifications;

/// <summary>
/// Serviço de notificações do sistema operacional para varreduras agendadas e alertas críticos (seção 60).
/// </summary>
public interface INotificationService
{
    Task ShowNotificationAsync(string title, string message, CancellationToken cancellationToken = default);
}

