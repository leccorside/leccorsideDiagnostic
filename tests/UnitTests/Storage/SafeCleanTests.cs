using LeccorWindowsHealthCenter.Application.Storage;
using LeccorWindowsHealthCenter.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.Storage;

public class SafeCleanTests
{
    [Fact]
    public async Task SafeCleanProvider_ProducesAccurateCategorySummaries()
    {
        var provider = new SafeCleanProvider(NullLogger<SafeCleanProvider>.Instance);

        var result = await provider.AnalyzeCleanableLocationsAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3, result.Categories.Count);

        var safe = result.Categories.Single(c => c.Category == CleanCategory.Safe);
        var review = result.Categories.Single(c => c.Category == CleanCategory.Review);
        var advanced = result.Categories.Single(c => c.Category == CleanCategory.Advanced);

        Assert.Equal("Seguro (Safe)", safe.Title);
        Assert.Equal("Revisar (Review)", review.Title);
        Assert.Equal("Avançado (Advanced)", advanced.Title);

        Assert.NotEmpty(safe.Items);
        Assert.NotEmpty(review.Items);
        Assert.NotEmpty(advanced.Items);

        Assert.Equal(result.SafeCleanableBytes + result.ReviewCleanableBytes + result.AdvancedCleanableBytes, result.TotalCleanableBytes);
    }
}

