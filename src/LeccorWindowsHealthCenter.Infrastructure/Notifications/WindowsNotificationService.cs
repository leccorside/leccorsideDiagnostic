using LeccorWindowsHealthCenter.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.Notifications;

/// <summary>
/// Serviço de notificações nativas para alertas e conclusões de varreduras agendadas (seção 60).
/// </summary>
public sealed class WindowsNotificationService : INotificationService
{
    private readonly ILogger<WindowsNotificationService> _logger;

    public WindowsNotificationService(ILogger<WindowsNotificationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task ShowNotificationAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogInformation("Notificação do Sistema: [{Title}] {Message}", title, message);
        return Task.CompletedTask;
    }
}
