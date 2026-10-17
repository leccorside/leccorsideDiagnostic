using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.Scoring;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.UnitTests.Scanning;

public class ScanOrchestratorTests
{
    private readonly InMemoryScanRepository _repository = new();

    private ScanOrchestrator Orchestrator(IEnumerable<IDiagnosticScanner> scanners, ScanOptions? options = null, params IRule[] rules) => new(
        scanners,
        new RuleEngine(rules, new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance),
        new ScoringEngine(),
        _repository,
        Options.Create(options ?? new ScanOptions()),
        TimeProvider.System,
        NullLogger<ScanOrchestrator>.Instance);

    /// <summary>IProgress síncrono (Progress&lt;T&gt; posta no thread pool e tornaria o teste não determinístico).</summary>
    private sealed class SyncProgress : IProgress<ScanProgress>
    {
        public List<ScanProgress> Reports { get; } = [];

        public void Report(ScanProgress value)
        {
            lock (Reports)
            {
                Reports.Add(value);
            }
        }
    }

    [Fact]
    public async Task Runs_scanners_applies_rules_scores_and_persists()
    {
        var cpu = new FakeScanner("cpu");
        var rule = new DelegateRule("r1", ["cpu"], _ => [Findings.Make("cpu", Severity.Medium)]);

        var report = await Orchestrator([cpu], null, rule).RunAsync(ScanPlan.Full, null, CancellationToken.None);

        Assert.Equal(1, cpu.Calls);
        Assert.Single(report.Problems);
        Assert.NotNull(report.Score.Overall);
        Assert.False(report.WasCancelled);
        Assert.Same(report, Assert.Single(_repository.Saved));
    }

    [Fact]
    public async Task Failing_scanner_does_not_break_the_scan()
    {
        var broken = new FakeScanner("broken", behavior: _ => throw new InvalidOperationException("WMI indisponível"));
        var healthy = new FakeScanner("healthy");

        var report = await Orchestrator([broken, healthy]).RunAsync(ScanPlan.Full, null, CancellationToken.None);

        Assert.Equal(ScannerRunStatus.Failed, report.Results.Single(r => r.ScannerId == "broken").Status);
        Assert.Contains("WMI indisponível", report.Results.Single(r => r.ScannerId == "broken").Error, StringComparison.Ordinal);
        Assert.Equal(ScannerRunStatus.Completed, report.Results.Single(r => r.ScannerId == "healthy").Status);
    }

    [Fact]
    public async Task Scanner_exceeding_timeout_is_marked_failed()
    {
        var slow = new FakeScanner("slow", behavior: async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("inalcançável");
        });

        var report = await Orchestrator([slow], new ScanOptions { ScannerTimeout = TimeSpan.FromMilliseconds(100) })
            .RunAsync(ScanPlan.Full, null, CancellationToken.None);

        var result = Assert.Single(report.Results);
        Assert.Equal(ScannerRunStatus.Failed, result.Status);
        Assert.Contains("Tempo limite", result.Error, StringComparison.Ordinal);
        Assert.False(report.WasCancelled);
    }

    [Fact]
    public async Task Cancellation_keeps_completed_results_and_marks_the_rest_cancelled()
    {
        using var cts = new CancellationTokenSource();
        var first = new FakeScanner("first", order: 1);
        var blocking = new FakeScanner("blocking", order: 2, behavior: async ct =>
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("inalcançável");
        });
        var never = new FakeScanner("never", order: 3);

        var report = await Orchestrator([first, blocking, never], new ScanOptions { MaxConcurrency = 1 })
            .RunAsync(ScanPlan.Full, null, cts.Token);

        Assert.True(report.WasCancelled);
        Assert.Equal(ScannerRunStatus.Completed, report.Results.Single(r => r.ScannerId == "first").Status);
        Assert.Equal(ScannerRunStatus.Cancelled, report.Results.Single(r => r.ScannerId == "blocking").Status);
        Assert.Equal(ScannerRunStatus.Cancelled, report.Results.Single(r => r.ScannerId == "never").Status);
        Assert.Equal(0, never.Calls);
        Assert.Single(_repository.Saved);
    }

    [Fact]
    public async Task Respects_max_concurrency()
    {
        var running = 0;
        var peak = 0;
        var scanners = Enumerable.Range(0, 6).Select(i => new FakeScanner($"s{i}", behavior: async ct =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref peak, now);
            await Task.Delay(40, ct);
            Interlocked.Decrement(ref running);
            return new DiagnosticResult { ScannerId = $"s{i}", ScannerName = $"s{i}", Module = ScanModule.Performance, Status = ScannerRunStatus.Completed };
        })).ToList();

        await Orchestrator(scanners, new ScanOptions { MaxConcurrency = 2 }).RunAsync(ScanPlan.Full, null, CancellationToken.None);

        Assert.InRange(peak, 1, 2);
    }

    [Fact]
    public async Task Quick_and_custom_plans_select_scanners()
    {
        var quick = new FakeScanner("quick", ScanModule.Performance, quick: true);
        var slow = new FakeScanner("slow", ScanModule.Development, quick: false);
        var orchestrator = Orchestrator([quick, slow]);

        var quickReport = await orchestrator.RunAsync(ScanPlan.Quick, null, CancellationToken.None);
        var customReport = await orchestrator.RunAsync(ScanPlan.Custom([ScanModule.Development]), null, CancellationToken.None);

        Assert.Equal(["quick"], quickReport.Results.Select(r => r.ScannerId));
        Assert.Equal(["slow"], customReport.Results.Select(r => r.ScannerId));
    }

    [Fact]
    public async Task Reports_progress_until_100_percent()
    {
        var progress = new SyncProgress();

        await Orchestrator([new FakeScanner("a"), new FakeScanner("b")]).RunAsync(ScanPlan.Full, progress, CancellationToken.None);

        Assert.Equal(0, progress.Reports[0].Percent);
        Assert.Equal(100, progress.Reports[^1].Percent);
        Assert.Contains(progress.Reports, p => p.Scanners.Any(s => s.Status == ScannerRunStatus.Running));
    }

    [Fact]
    public async Task Repository_failure_does_not_lose_the_report()
    {
        var orchestrator = new ScanOrchestrator(
            [new FakeScanner("a")],
            new RuleEngine([], new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance),
            new ScoringEngine(),
            new ThrowingRepository(),
            Options.Create(new ScanOptions()),
            TimeProvider.System,
            NullLogger<ScanOrchestrator>.Instance);

        var report = await orchestrator.RunAsync(ScanPlan.Full, null, CancellationToken.None);

        Assert.Single(report.Results);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }

    private sealed class ThrowingRepository : LeccorWindowsHealthCenter.Application.Persistence.IScanRepository
    {
        public Task SaveAsync(ScanReport report, CancellationToken cancellationToken) => throw new IOException("banco bloqueado");

        public Task<IReadOnlyList<LeccorWindowsHealthCenter.Application.Persistence.ScanSummary>> GetRecentAsync(int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Finding>> GetFindingsAsync(Guid scanId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
