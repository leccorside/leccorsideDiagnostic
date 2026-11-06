using LeccorWindowsHealthCenter.Infrastructure.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.IntegrationTests.Events;

public class EventViewerIntegrationTests
{
    [Fact]
    public async Task EventViewerProvider_ReadsRealWindowsEvents_WithoutErrors()
    {
        var provider = new EventViewerProvider(NullLogger<EventViewerProvider>.Instance);

        var events = await provider.GetRecentEventGroupsAsync(TimeSpan.FromHours(48), CancellationToken.None);

        Assert.NotNull(events);
        foreach (var group in events)
        {
            Assert.False(string.IsNullOrWhiteSpace(group.LogName));
            Assert.False(string.IsNullOrWhiteSpace(group.ProviderName));
            Assert.True(group.EventId >= 0);
            Assert.True(group.Count24h >= 1);
            Assert.True(group.LastSeen >= group.FirstSeen);
        }
    }
}
