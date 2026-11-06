using LeccorWindowsHealthCenter.Application.Commands;
using LeccorWindowsHealthCenter.Application.Scheduling;
using LeccorWindowsHealthCenter.Application.Settings;
using LeccorWindowsHealthCenter.Infrastructure.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LeccorWindowsHealthCenter.UnitTests.Scheduling;

public sealed class WindowsTaskSchedulerServiceTests
{
    private sealed class FakeCommandExecutor : ICommandExecutor
    {
        public CommandRequest? LastRequest { get; private set; }
        public int ExitCodeToReturn { get; set; }
        public string OutputToReturn { get; set; } = "Próxima Hora de Execução: 12/11/2026 12:00:00\r\n";

        public Task<CommandResult> ExecuteAsync(CommandRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new CommandResult
            {
                CommandLine = $"{request.ExecutablePath} {string.Join(" ", request.Arguments)}",
                ExitCode = ExitCodeToReturn,
                StandardOutput = OutputToReturn,
                StandardError = string.Empty,
                Duration = TimeSpan.FromMilliseconds(50),
                TimedOut = false
            });
        }
    }

    private sealed class FakeConfigRepo : IConfigurationRepository
    {
        private readonly Dictionary<string, string> _store = new();

        public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(key, out var val);
            return Task.FromResult<string?>(val);
        }

        public Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(_store);
        }
    }

    [Fact]
    public async Task ConfigureScheduleAsync_Daily_CallsSchtasksWithDailyArgumentAndStoresConfig()
    {
        var executor = new FakeCommandExecutor();
        var config = new FakeConfigRepo();
        var service = new WindowsTaskSchedulerService(executor, config, NullLogger<WindowsTaskSchedulerService>.Instance);

        await service.ConfigureScheduleAsync(ScheduledScanFrequency.Daily, CancellationToken.None);

        Assert.NotNull(executor.LastRequest);
        Assert.Contains("/create", executor.LastRequest.Arguments);
        Assert.Contains(WindowsTaskSchedulerService.TaskName, executor.LastRequest.Arguments);
        Assert.Contains("DAILY", executor.LastRequest.Arguments);

        var saved = await config.GetValueAsync("schedule.frequency");
        Assert.Equal("Daily", saved);
    }

    [Fact]
    public async Task DisableScheduleAsync_CallsSchtasksDeleteAndClearsConfig()
    {
        var executor = new FakeCommandExecutor();
        var config = new FakeConfigRepo();
        await config.SetValueAsync("schedule.frequency", "Daily");
        var service = new WindowsTaskSchedulerService(executor, config, NullLogger<WindowsTaskSchedulerService>.Instance);

        await service.DisableScheduleAsync(CancellationToken.None);

        Assert.NotNull(executor.LastRequest);
        Assert.Contains("/delete", executor.LastRequest.Arguments);
        Assert.Contains(WindowsTaskSchedulerService.TaskName, executor.LastRequest.Arguments);

        var saved = await config.GetValueAsync("schedule.frequency");
        Assert.Equal("Disabled", saved);
    }

    [Fact]
    public async Task GetStatusAsync_WhenTaskExists_ReturnsConfiguredStatus()
    {
        var executor = new FakeCommandExecutor();
        var config = new FakeConfigRepo();
        await config.SetValueAsync("schedule.frequency", "Daily");
        var service = new WindowsTaskSchedulerService(executor, config, NullLogger<WindowsTaskSchedulerService>.Instance);

        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.True(status.IsConfigured);
        Assert.Equal(ScheduledScanFrequency.Daily, status.Frequency);
        Assert.Equal(WindowsTaskSchedulerService.TaskName, status.TaskName);
    }
}
