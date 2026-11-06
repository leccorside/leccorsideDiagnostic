using LeccorWindowsHealthCenter.Infrastructure.WindowsUpdate;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.IntegrationTests.WindowsUpdate;

public class WindowsUpdateIntegrationTests
{
    [Fact]
    public async Task WindowsUpdateProvider_ReadsStatusAndHistory_WithoutErrors()
    {
        var provider = new WindowsUpdateProvider(NullLogger<WindowsUpdateProvider>.Instance);

        var status = await provider.GetStatusAsync(CancellationToken.None);
        Assert.NotNull(status);
        Assert.NotNull(status.ServiceStatus);
        Assert.False(string.IsNullOrWhiteSpace(status.ServiceStatus.WuauservState));

        var history = await provider.GetHistoryAsync(CancellationToken.None);
        Assert.NotNull(history);
        // Em qualquer máquina Windows atualizada há hotfixes registrados
        Assert.NotEmpty(history);
        Assert.False(string.IsNullOrWhiteSpace(history[0].HotFixId));
    }
}

