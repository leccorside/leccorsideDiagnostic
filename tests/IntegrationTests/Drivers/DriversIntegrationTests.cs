using LeccorWindowsHealthCenter.Infrastructure.Drivers;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.IntegrationTests.Drivers;

public class DriversIntegrationTests
{
    [Fact]
    public async Task PnpDeviceDriverProvider_ReadsRealPnpDevices_WithoutErrors()
    {
        var provider = new PnpDeviceDriverProvider(NullLogger<PnpDeviceDriverProvider>.Instance);

        var devices = await provider.GetDevicesAsync(CancellationToken.None);

        Assert.NotNull(devices);
        Assert.NotEmpty(devices);

        foreach (var dev in devices.Take(20))
        {
            Assert.False(string.IsNullOrWhiteSpace(dev.DeviceId));
            Assert.False(string.IsNullOrWhiteSpace(dev.Name));
        }
    }
}

