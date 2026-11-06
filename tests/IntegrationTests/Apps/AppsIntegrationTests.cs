using LeccorWindowsHealthCenter.Infrastructure.Apps;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.IntegrationTests.Apps;

public class AppsIntegrationTests
{
    [Fact]
    public async Task InstalledAppsProvider_ReadsInstalledApps_WithoutErrors()
    {
        var provider = new InstalledAppsProvider(NullLogger<InstalledAppsProvider>.Instance);

        var apps = await provider.GetInstalledAppsAsync(CancellationToken.None);

        Assert.NotNull(apps);
        Assert.NotEmpty(apps);

        foreach (var app in apps.Take(20))
        {
            Assert.False(string.IsNullOrWhiteSpace(app.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(app.RegistryKeyPath));
        }
    }
}

