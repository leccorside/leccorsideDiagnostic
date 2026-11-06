using LeccorWindowsHealthCenter.Infrastructure.Network;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.IntegrationTests.Network;

public class NetworkIntegrationTests
{
    [Fact]
    public async Task NetworkProvider_ReadsAdapters_WithoutErrors()
    {
        var provider = new NetworkProvider(NullLogger<NetworkProvider>.Instance);

        var adapters = await provider.GetAdaptersAsync(CancellationToken.None);

        Assert.NotNull(adapters);
        Assert.NotEmpty(adapters);

        foreach (var adapter in adapters)
        {
            Assert.False(string.IsNullOrWhiteSpace(adapter.Id));
            Assert.False(string.IsNullOrWhiteSpace(adapter.Name));
        }
    }

    [Fact]
    public async Task NetworkDiagnosticsService_RunsQuickDiagnostics_WithoutCrashing()
    {
        var provider = new NetworkProvider(NullLogger<NetworkProvider>.Instance);
        var diagService = new NetworkDiagnosticsService(provider, NullLogger<NetworkDiagnosticsService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await diagService.RunDiagnosticsAsync(cts.Token);

        Assert.NotNull(result);
        Assert.NotNull(result.DiagnosticWarnings);
    }
}

