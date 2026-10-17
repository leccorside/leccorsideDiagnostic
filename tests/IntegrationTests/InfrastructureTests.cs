using LeccorWindowsHealthCenter.Application.Settings;
using LeccorWindowsHealthCenter.Domain.Repairs;
using LeccorWindowsHealthCenter.Infrastructure.Backup;
using LeccorWindowsHealthCenter.Infrastructure.Logging;
using LeccorWindowsHealthCenter.Infrastructure.Security;
using LeccorWindowsHealthCenter.Monitoring;
using LeccorWindowsHealthCenter.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Extensions.Logging;

namespace LeccorWindowsHealthCenter.IntegrationTests;

public sealed class InfrastructureTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Backup_store_roundtrip()
    {
        var store = new FileBackupStore(_temp.File("Backups"), NullLogger<FileBackupStore>.Instance);
        var record = new BackupRecord
        {
            ActionId = "startup.disable",
            Timestamp = DateTimeOffset.UtcNow,
            User = @"PC\usuario",
            PreviousValues = new Dictionary<string, string?> { [@"HKCU\Run\App"] = "C:\\app.exe", ["vazio"] = null },
        };

        await store.SaveAsync(record, CancellationToken.None);
        var loaded = await store.GetAsync(record.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("C:\\app.exe", loaded.PreviousValues[@"HKCU\Run\App"]);
        Assert.Null(loaded.PreviousValues["vazio"]);
        Assert.Null(await store.GetAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Empty(Directory.GetFiles(_temp.File("Backups"), "*.tmp"));
    }

    [Fact]
    public void Logging_writes_structured_file_and_redacts_secrets()
    {
        var paths = new AppPaths(_temp.Path, isPortable: true);
        paths.EnsureCreated();

        using (var serilog = LoggingSetup.CreateLogger(paths, new LoggingFileOptions()))
        using (var factory = new SerilogLoggerFactory(serilog))
        {
            var logger = factory.CreateLogger("teste");
            logger.LogInformation("Comando {CommandLine} com {Password}", "tool --token abc123secret", "Sup3rS3cret");
        }

        var file = Assert.Single(Directory.GetFiles(paths.Logs, "lwhc-*.log"));
        var content = File.ReadAllText(file);
        Assert.Contains("\"@mt\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123secret", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Sup3rS3cret", content, StringComparison.Ordinal);
        Assert.Contains("REDACTED", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Live_metrics_are_in_valid_ranges()
    {
        var provider = new LiveMetricsProvider();

        var first = provider.Sample();
        await Task.Delay(250);
        var second = provider.Sample();

        Assert.Null(first.CpuPercent);
        Assert.NotNull(second.CpuPercent);
        Assert.InRange(second.CpuPercent!.Value, 0, 100);
        Assert.InRange(second.MemoryPercent!.Value, 0, 100);
        Assert.True(second.MemoryTotalBytes > 0);
        Assert.InRange(second.SystemDrivePercent!.Value, 0, 100);
        Assert.Null(second.GpuPercent);
    }

    [Fact]
    public void Privilege_service_reports_current_user()
    {
        var service = new PrivilegeService();

        Assert.Contains(Environment.UserName, service.CurrentUser, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Portable_flag_switches_data_directory()
    {
        File.WriteAllText(_temp.File(AppPaths.PortableFlagFileName), string.Empty);

        var paths = AppPaths.Resolve(_temp.Path);

        Assert.True(paths.IsPortable);
        Assert.Equal(_temp.File("Data"), paths.Root);
    }
}
