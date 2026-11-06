using LeccorWindowsHealthCenter.Application.Storage;
using LeccorWindowsHealthCenter.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LeccorWindowsHealthCenter.UnitTests.Persistence;

public sealed class SqliteSpaceAnalysisSnapshotRepositoryTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqliteDatabase _database;
    private readonly SqliteSpaceAnalysisSnapshotRepository _repository;

    public SqliteSpaceAnalysisSnapshotRepositoryTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"healthcenter_space_test_{Guid.NewGuid():N}.db");
        _database = new SqliteDatabase(_tempDbPath);
        var runner = new MigrationRunner(_database, NullLogger<MigrationRunner>.Instance);
        runner.MigrateAsync(CancellationToken.None).GetAwaiter().GetResult();
        _repository = new SqliteSpaceAnalysisSnapshotRepository(_database);
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

    private static SpaceAnalysis CreateSampleAnalysis(string rootPath, DateTimeOffset capturedAt)
    {
        var subSubFolder = new FolderNode
        {
            Path = Path.Combine(rootPath, "Sub", "Deep"),
            Name = "Deep",
            SizeBytes = 100L * 1024 * 1024,
            FileCount = 50,
            FilesBytes = 50L * 1024 * 1024,
            Children = [],
        };

        var subFolder = new FolderNode
        {
            Path = Path.Combine(rootPath, "Sub"),
            Name = "Sub",
            SizeBytes = 500L * 1024 * 1024,
            FileCount = 200,
            FilesBytes = 150L * 1024 * 1024,
            HardLinkDeduplicatedBytes = 10L * 1024 * 1024,
            Children = [subSubFolder],
        };

        var rootNode = new FolderNode
        {
            Path = rootPath,
            Name = rootPath,
            SizeBytes = 1024L * 1024 * 1024,
            FileCount = 500,
            FilesBytes = 200L * 1024 * 1024,
            HardLinkDeduplicatedBytes = 15L * 1024 * 1024,
            Children = [subFolder],
        };

        var largestFiles = new List<FileEntry>
        {
            new(Path.Combine(rootPath, "bigfile.iso"), 400L * 1024 * 1024, capturedAt.AddDays(-2)),
            new(Path.Combine(rootPath, "data.vhd"), 300L * 1024 * 1024, capturedAt.AddDays(-1)),
        };

        var oldFiles = new List<FileEntry>
        {
            new(Path.Combine(rootPath, "old_backup.zip"), 250L * 1024 * 1024, capturedAt.AddYears(-2)),
        };

        return new SpaceAnalysis
        {
            Root = rootNode,
            LargestFiles = largestFiles,
            OldFiles = oldFiles,
            Duration = TimeSpan.FromSeconds(3.5),
            CapturedAt = capturedAt,
        };
    }

    [Fact]
    public async Task GetLatestSnapshotAsync_WhenEmpty_ReturnsNull()
    {
        var result = await _repository.GetLatestSnapshotAsync(@"C:\", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveSnapshotAsync_And_GetLatestSnapshotAsync_RestoresCompleteAnalysis()
    {
        var root = @"C:\";
        var capturedAt = DateTimeOffset.UtcNow;
        var sample = CreateSampleAnalysis(root, capturedAt);

        await _repository.SaveSnapshotAsync(root, sample, CancellationToken.None);

        var loaded = await _repository.GetLatestSnapshotAsync(root, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(sample.Root.FileCount, loaded.Root.FileCount);
        Assert.Equal(sample.Root.SizeBytes, loaded.Root.SizeBytes);
        Assert.Equal(sample.Duration, loaded.Duration);
        Assert.Equal(sample.Root.Name, loaded.Root.Name);
        Assert.Equal(sample.Root.HardLinkDeduplicatedBytes, loaded.Root.HardLinkDeduplicatedBytes);
        Assert.Single(loaded.Root.Children);
        Assert.Equal("Sub", loaded.Root.Children[0].Name);
        Assert.Equal(10L * 1024 * 1024, loaded.Root.Children[0].HardLinkDeduplicatedBytes);
        Assert.Equal(2, loaded.LargestFiles.Count);
        Assert.Single(loaded.OldFiles);
        Assert.Equal(Path.Combine(root, "old_backup.zip"), loaded.OldFiles[0].Path);
    }

    [Fact]
    public async Task SaveSnapshotAsync_EnforcesRetentionOfLatest10Snapshots()
    {
        var root = @"D:\";
        var baseTime = DateTimeOffset.UtcNow.AddDays(-20);

        for (var i = 1; i <= 13; i++)
        {
            var analysis = CreateSampleAnalysis(root, baseTime.AddHours(i));
            await _repository.SaveSnapshotAsync(root, analysis, CancellationToken.None);
        }

        var history = await _repository.GetSnapshotsHistoryAsync(root, CancellationToken.None);
        Assert.Equal(10, history.Count);

        // O mais recente deve ser a 13ª análise
        var latest = await _repository.GetLatestSnapshotAsync(root, CancellationToken.None);
        Assert.NotNull(latest);
        Assert.Equal(baseTime.AddHours(13).ToUnixTimeSeconds(), latest.CapturedAt.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task DeleteSnapshotAsync_RemovesSpecificSnapshot()
    {
        var root = @"E:\";
        var now = DateTimeOffset.UtcNow;
        var a1 = CreateSampleAnalysis(root, now.AddMinutes(-10));
        var a2 = CreateSampleAnalysis(root, now);

        await _repository.SaveSnapshotAsync(root, a1, CancellationToken.None);
        await _repository.SaveSnapshotAsync(root, a2, CancellationToken.None);

        var historyBefore = await _repository.GetSnapshotsHistoryAsync(root, CancellationToken.None);
        Assert.Equal(2, historyBefore.Count);

        var idToDelete = historyBefore[0].Id;
        await _repository.DeleteSnapshotAsync(idToDelete, CancellationToken.None);

        var historyAfter = await _repository.GetSnapshotsHistoryAsync(root, CancellationToken.None);
        Assert.Single(historyAfter);
        Assert.NotEqual(idToDelete, historyAfter[0].Id);
    }
}
