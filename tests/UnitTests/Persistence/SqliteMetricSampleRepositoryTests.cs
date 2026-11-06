using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LeccorWindowsHealthCenter.UnitTests.Persistence;

public sealed class SqliteMetricSampleRepositoryTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqliteDatabase _database;
    private readonly SqliteMetricSampleRepository _repository;

    public SqliteMetricSampleRepositoryTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"healthcenter_metrics_test_{Guid.NewGuid():N}.db");
        _database = new SqliteDatabase(_tempDbPath);
        var runner = new MigrationRunner(_database, NullLogger<MigrationRunner>.Instance);
        runner.MigrateAsync(CancellationToken.None).GetAwaiter().GetResult();
        _repository = new SqliteMetricSampleRepository(_database);
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbPath))
        {
            try
            {
                File.Delete(_tempDbPath);
            }
            catch
            {
                // Limpeza best-effort
            }
        }
    }

    [Fact]
    public async Task SaveSampleAsync_And_GetRecentSamplesAsync_ReturnsPersistedData()
    {
        var now = DateTimeOffset.UtcNow;
        var sample1 = new MetricSampleRecord
        {
            Timestamp = now.AddMinutes(-5),
            CpuPercent = 25.5,
            MemoryUsedPercent = 60.0,
            MemoryUsedBytes = 8L * 1024 * 1024 * 1024,
            CommitUsedPercent = 50.0,
            CommitUsedBytes = 10L * 1024 * 1024 * 1024,
        };

        var sample2 = new MetricSampleRecord
        {
            Timestamp = now,
            CpuPercent = 45.0,
            MemoryUsedPercent = 65.0,
            MemoryUsedBytes = 9L * 1024 * 1024 * 1024,
            CommitUsedPercent = 55.0,
            CommitUsedBytes = 11L * 1024 * 1024 * 1024,
        };

        await _repository.SaveSampleAsync(sample1, CancellationToken.None);
        await _repository.SaveSampleAsync(sample2, CancellationToken.None);

        var recent = await _repository.GetRecentSamplesAsync(TimeSpan.FromMinutes(10), CancellationToken.None);

        Assert.Equal(2, recent.Count);
        Assert.Equal(25.5, recent[0].CpuPercent);
        Assert.Equal(45.0, recent[1].CpuPercent);
    }

    [Fact]
    public async Task PruneOlderThanAsync_DeletesOldSamples()
    {
        var now = DateTimeOffset.UtcNow;
        var oldSample = new MetricSampleRecord
        {
            Timestamp = now.AddDays(-10),
            CpuPercent = 10.0,
            MemoryUsedPercent = 50.0,
            MemoryUsedBytes = 4L * 1024 * 1024 * 1024,
            CommitUsedPercent = 40.0,
            CommitUsedBytes = 5L * 1024 * 1024 * 1024,
        };

        var recentSample = new MetricSampleRecord
        {
            Timestamp = now,
            CpuPercent = 30.0,
            MemoryUsedPercent = 55.0,
            MemoryUsedBytes = 5L * 1024 * 1024 * 1024,
            CommitUsedPercent = 45.0,
            CommitUsedBytes = 6L * 1024 * 1024 * 1024,
        };

        await _repository.SaveSampleAsync(oldSample, CancellationToken.None);
        await _repository.SaveSampleAsync(recentSample, CancellationToken.None);

        await _repository.PruneOlderThanAsync(now.AddDays(-5), CancellationToken.None);

        var remaining = await _repository.GetRecentSamplesAsync(TimeSpan.FromDays(30), CancellationToken.None);

        Assert.Single(remaining);
        Assert.Equal(30.0, remaining[0].CpuPercent);
    }
}

