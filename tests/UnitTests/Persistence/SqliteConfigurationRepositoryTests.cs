using LeccorWindowsHealthCenter.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LeccorWindowsHealthCenter.UnitTests.Persistence;

public sealed class SqliteConfigurationRepositoryTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqliteDatabase _database;
    private readonly SqliteConfigurationRepository _repository;

    public SqliteConfigurationRepositoryTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"healthcenter_cfg_test_{Guid.NewGuid():N}.db");
        _database = new SqliteDatabase(_tempDbPath);
        var runner = new MigrationRunner(_database, NullLogger<MigrationRunner>.Instance);
        runner.MigrateAsync(CancellationToken.None).GetAwaiter().GetResult();
        _repository = new SqliteConfigurationRepository(_database);
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
    public async Task GetValueAsync_WhenKeyDoesNotExist_ReturnsNull()
    {
        var val = await _repository.GetValueAsync("non_existent_key");
        Assert.Null(val);
    }

    [Fact]
    public async Task SetValueAsync_And_GetValueAsync_PersistsValue()
    {
        await _repository.SetValueAsync("ui.theme", "Dark");
        var val = await _repository.GetValueAsync("ui.theme");

        Assert.Equal("Dark", val);

        // Atualiza a mesma chave
        await _repository.SetValueAsync("ui.theme", "Light");
        var updated = await _repository.GetValueAsync("ui.theme");
        Assert.Equal("Light", updated);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllStoredConfigurations()
    {
        await _repository.SetValueAsync("key1", "val1");
        await _repository.SetValueAsync("key2", "val2");

        var all = await _repository.GetAllAsync();

        Assert.True(all.Count >= 2);
        Assert.Equal("val1", all["key1"]);
        Assert.Equal("val2", all["key2"]);
    }
}
