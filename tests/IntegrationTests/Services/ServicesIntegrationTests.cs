using LeccorWindowsHealthCenter.Infrastructure.Services;
using LeccorWindowsHealthCenter.Infrastructure.SystemInfo;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.IntegrationTests.Services;

public class ServicesIntegrationTests
{
    [Fact]
    public async Task WindowsServicesProvider_ReadsRealWindowsServices_WithoutErrors()
    {
        using var fileId = new FileIdentityProvider(NullLogger<FileIdentityProvider>.Instance);
        var provider = new WindowsServicesProvider(fileId, NullLogger<WindowsServicesProvider>.Instance);

        var services = await provider.GetServicesAsync(CancellationToken.None);

        Assert.NotNull(services);
        Assert.NotEmpty(services);

        // Todo Windows possui serviços essenciais ativos como RpcSs ou EventLog
        var rpcOrCore = services.FirstOrDefault(s => string.Equals(s.ServiceName, "RpcSs", StringComparison.OrdinalIgnoreCase) ||
                                                    string.Equals(s.ServiceName, "EventLog", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(rpcOrCore);
        Assert.False(string.IsNullOrWhiteSpace(rpcOrCore.DisplayName));
        Assert.True(rpcOrCore.FileExists);

        foreach (var svc in services.Take(20))
        {
            Assert.False(string.IsNullOrWhiteSpace(svc.ServiceName));
            Assert.False(string.IsNullOrWhiteSpace(svc.DisplayName));
        }
    }
}
