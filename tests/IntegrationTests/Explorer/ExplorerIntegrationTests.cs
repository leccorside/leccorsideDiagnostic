using LeccorWindowsHealthCenter.Infrastructure.Explorer;
using LeccorWindowsHealthCenter.Infrastructure.SystemInfo;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.IntegrationTests.Explorer;

public class ExplorerIntegrationTests
{
    [Fact]
    public async Task ExplorerProvider_ReadsShellExtensions_AndCacheStatus_WithoutErrors()
    {
        using var fileId = new FileIdentityProvider(NullLogger<FileIdentityProvider>.Instance);
        var provider = new ExplorerProvider(fileId, NullLogger<ExplorerProvider>.Instance);

        var extensions = await provider.GetShellExtensionsAsync(CancellationToken.None);
        Assert.NotNull(extensions);
        // Em um sistema Windows padrão sempre há dezenas de extensões de shell instaladas
        Assert.NotEmpty(extensions);

        foreach (var ext in extensions.Take(10))
        {
            Assert.False(string.IsNullOrWhiteSpace(ext.Clsid));
            Assert.False(string.IsNullOrWhiteSpace(ext.Name));
        }

        var cache = await provider.GetCacheStatusAsync(CancellationToken.None);
        Assert.NotNull(cache);
        Assert.True(cache.IconCacheSizeBytes >= 0);
        Assert.True(cache.ThumbnailCacheSizeBytes >= 0);
        Assert.True(cache.CacheFilesCount >= 0);
    }

    [Fact]
    public async Task FolderBenchmarkService_BenchmarksDirectory_Accurately()
    {
        var benchmark = new FolderBenchmarkService(NullLogger<FolderBenchmarkService>.Instance);
        var tempDir = Path.Combine(Path.GetTempPath(), "lwhc_bench_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var subDir = Path.Combine(tempDir, "sub");
            Directory.CreateDirectory(subDir);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "file1.txt"), "hello world");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "file2.txt"), "antigravity test");

            var result = await benchmark.BenchmarkFolderAsync(tempDir, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(tempDir, result.FolderPath);
            Assert.Equal(2, result.FileCount);
            Assert.Equal(1, result.DirectoryCount);
            Assert.True(result.TotalSizeBytes > 0);
            Assert.True(result.EnumerationDuration >= TimeSpan.Zero);
            Assert.True(result.MetadataReadDuration >= TimeSpan.Zero);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
