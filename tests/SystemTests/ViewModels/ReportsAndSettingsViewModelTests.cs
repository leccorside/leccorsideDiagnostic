using System.IO;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.App.ViewModels;
using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Application.Reporting;
using LeccorWindowsHealthCenter.Application.Scheduling;
using LeccorWindowsHealthCenter.Application.Settings;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Reporting;
using LeccorWindowsHealthCenter.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LeccorWindowsHealthCenter.SystemTests.ViewModels;

public sealed class ReportsAndSettingsViewModelTests
{
    private sealed class FakePrivilegeService : IPrivilegeService
    {
        public bool IsElevated => false;
        public string CurrentUser => "TestUser";
    }

    private sealed class FakeScanRepository : IScanRepository
    {
        public Task SaveAsync(ScanReport report, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ScanSummary>> GetRecentAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ScanSummary>>([]);
        public Task<IReadOnlyList<Finding>> GetFindingsAsync(Guid scanId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Finding>>([]);
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
        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(_store);
    }

    private sealed class FakeSchedulerService : IScanSchedulerService
    {
        public ScheduledScanFrequency ConfiguredFrequency { get; private set; } = ScheduledScanFrequency.Disabled;
        public Task<ScheduleStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ScheduleStatus
            {
                IsConfigured = ConfiguredFrequency != ScheduledScanFrequency.Disabled,
                Frequency = ConfiguredFrequency,
                TaskName = "TestTask",
                Details = "Configurado para teste"
            });

        public Task ConfigureScheduleAsync(ScheduledScanFrequency frequency, CancellationToken cancellationToken = default)
        {
            ConfiguredFrequency = frequency;
            return Task.CompletedTask;
        }

        public Task DisableScheduleAsync(CancellationToken cancellationToken = default)
        {
            ConfiguredFrequency = ScheduledScanFrequency.Disabled;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ReportsViewModel_InitializesWithFormatsAndPublishesSessionReport()
    {
        var session = new ScanSession();
        var repo = new FakeScanRepository();
        var exporters = new IReportExporter[]
        {
            new HtmlReportExporter(),
            new PdfReportExporter(),
            new CsvReportExporter(),
            new JsonReportExporter()
        };

        var vm = new ReportsViewModel(session, repo, exporters);

        Assert.Equal(4, vm.Formats.Count);
        Assert.Equal("HTML", vm.SelectedFormat.Format);
        Assert.False(vm.HasReport);

        // Publica relatório na sessão
        var sampleReport = new ScanReport
        {
            Mode = ScanMode.Quick,
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-5),
            CompletedAt = DateTimeOffset.UtcNow,
            Results = [],
            Score = new HealthScore { Overall = 90, Rating = HealthRating.Good },
            MachineName = "DESKTOP-TEST"
        };

        session.Publish(sampleReport);
        await vm.RefreshReportsAsync();

        Assert.True(vm.HasReport);
        Assert.NotNull(vm.SelectedReport);
        Assert.Contains("Sessão Atual", vm.SelectedReport.DisplayTitle);
    }

    [Fact]
    public async Task SettingsViewModel_HandlesThemeAndSchedulingChanges()
    {
        var themeService = new ThemeService();
        var prefs = new UiPreferences(Options.Create(new HealthCenterOptions()));
        var paths = AppPaths.ResolveWritable(Path.GetTempPath());
        var privs = new FakePrivilegeService();
        var repairOptions = Options.Create(new RepairOptions());
        var configRepo = new FakeConfigRepo();
        var scheduler = new FakeSchedulerService();

        var vm = new SettingsViewModel(themeService, prefs, paths, privs, repairOptions, configRepo, scheduler);

        // Altera tema
        vm.SelectedTheme = AppTheme.Dark;
        Assert.Equal(AppTheme.Dark, themeService.Current);

        var savedTheme = await configRepo.GetValueAsync("ui.theme");
        Assert.Equal("Dark", savedTheme);

        // Altera frequência de agendamento e salva
        vm.SelectedFrequency = ScheduledScanFrequency.Weekly;
        await vm.SaveScheduleAsync();

        Assert.Equal(ScheduledScanFrequency.Weekly, scheduler.ConfiguredFrequency);
    }
}
