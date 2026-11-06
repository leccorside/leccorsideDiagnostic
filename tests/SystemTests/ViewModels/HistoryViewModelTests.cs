using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.App.ViewModels;
using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Application.Settings;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.SystemTests.ViewModels;

public class HistoryViewModelTests
{
    private static UiPreferences CreatePreferences() =>
        new(Options.Create(new HealthCenterOptions()));

    [Fact]
    public async Task Refresh_PopulatesScansList_FromRepository()
    {
        var scanId = Guid.NewGuid();
        var summary = new ScanSummary(
            Id: scanId,
            Mode: ScanMode.Quick,
            StartedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedAt: DateTimeOffset.UtcNow,
            Score: 85,
            Rating: HealthRating.Good,
            ProblemCount: 1,
            RecommendationCount: 2,
            WasCancelled: false);

        var repo = new FakeScanRepository
        {
            Summaries = [summary],
        };

        var vm = new HistoryViewModel(repo, CreatePreferences(), NullLogger<HistoryViewModel>.Instance);

        await vm.RefreshCommand.ExecuteAsync(null);

        var item = Assert.Single(vm.Scans);
        Assert.Equal(scanId, item.Id);
        Assert.Equal("85", item.ScoreText);
        Assert.Equal("Varredura rápida", item.ModeText);
        Assert.Equal(1, item.ProblemCount);
        Assert.Equal(2, item.RecommendationCount);
    }

    [Fact]
    public async Task ViewReport_LoadsFindings_ForSelectedScan()
    {
        var scanId = Guid.NewGuid();
        var summary = new ScanSummary(
            Id: scanId,
            Mode: ScanMode.Full,
            StartedAt: DateTimeOffset.UtcNow.AddHours(-1),
            CompletedAt: DateTimeOffset.UtcNow.AddMinutes(-55),
            Score: 60,
            Rating: HealthRating.Attention,
            ProblemCount: 1,
            RecommendationCount: 1,
            WasCancelled: false);

        var problemFinding = new Finding
        {
            RuleId = "windows.component-store-corrupted",
            ScannerId = "windows.integrity",
            Category = FindingCategory.Windows,
            Title = "Repositório de componentes corrompido",
            Description = "O repositório DISM contém arquivos corrompidos.",
            Severity = Severity.High,
            Confidence = Confidence.Confirmed,
            Evidence = [new Evidence { Source = EvidenceSource.Command, Description = "Corrupção detectada" }],
            Impact = "Atualizações podem falhar.",
            Recommendation = "Reparar repositório.",
        };

        var recommendationFinding = new Finding
        {
            RuleId = "eventviewer.app-crash",
            ScannerId = "eventviewer",
            Category = FindingCategory.Processes,
            Title = "Falhas frequentes em aplicativo",
            Description = "App caiu várias vezes.",
            Severity = Severity.Info,
            Confidence = Confidence.Confirmed,
            Evidence = [new Evidence { Source = EvidenceSource.EventLog, Description = "Event 1000" }],
            Impact = "Interrupção do trabalho.",
            Recommendation = "Atualizar aplicativo.",
        };

        var repo = new FakeScanRepository
        {
            Summaries = [summary],
            Findings = [problemFinding, recommendationFinding],
        };

        var vm = new HistoryViewModel(repo, CreatePreferences(), NullLogger<HistoryViewModel>.Instance);
        await vm.RefreshCommand.ExecuteAsync(null);

        var scanItem = Assert.Single(vm.Scans);
        await vm.ViewReportCommand.ExecuteAsync(scanItem);

        Assert.True(vm.ViewingScanReport);
        Assert.Single(vm.Problems);
        Assert.Equal("Repositório de componentes corrompido", vm.Problems[0].Title);
        Assert.Single(vm.Recommendations);
        Assert.Equal("Falhas frequentes em aplicativo", vm.Recommendations[0].Title);
    }

    private sealed class FakeScanRepository : IScanRepository
    {
        public List<ScanSummary> Summaries { get; init; } = [];
        public List<Finding> Findings { get; init; } = [];

        public Task SaveAsync(ScanReport report, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<ScanSummary>> GetRecentAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ScanSummary>>(Summaries);

        public Task<IReadOnlyList<Finding>> GetFindingsAsync(Guid scanId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Finding>>(Findings);
    }
}
