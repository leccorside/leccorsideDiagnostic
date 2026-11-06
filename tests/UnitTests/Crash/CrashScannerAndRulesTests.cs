using LeccorWindowsHealthCenter.Application.Crash;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Crash;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.Crash;

public class CrashScannerAndRulesTests
{
    private static List<Finding> Run(CrashKernelMinidumpRule rule, IEnumerable<Observation> observations)
    {
        var engine = new RuleEngine([rule], new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance);
        var result = new DiagnosticResult
        {
            ScannerId = rule.RequiredScanners[0],
            ScannerName = rule.RequiredScanners[0],
            Module = ScanModule.Events,
            Status = ScannerRunStatus.Completed,
            Metrics = [],
            Observations = observations.ToList(),
        };
        return engine.Evaluate([result]).SelectMany(r => r.Findings).ToList();
    }

    [Fact]
    public async Task CrashScanner_collects_dumps_top_apps_and_bsods()
    {
        var summary = new CrashSummary
        {
            DumpFiles =
            [
                new DumpFileInfo { FileName = "010126-1234-01.dmp", FilePath = "C:\\Windows\\Minidump\\010126-1234-01.dmp", DumpType = "KernelMinidump", SizeBytes = 1024 * 1024, CreatedAt = DateTimeOffset.UtcNow }
            ],
            TopCrashingApps =
            [
                new AppCrashCount { AppName = "explorer.exe", CrashCount = 4, HangCount = 1, LastOccurredAt = DateTimeOffset.UtcNow }
            ],
            TotalKernelDumpsCount = 1,
            TotalAppDumpsCount = 0,
            TotalBsodEventsPast30Days = 1,
            LatestBsodBugCheck = "0x0000003B (SYSTEM_SERVICE_EXCEPTION)",
            CapturedAt = DateTimeOffset.UtcNow
        };

        var provider = new FakeCrashProvider(summary);
        var scanner = new CrashScanner(provider, NullLogger<CrashScanner>.Instance);

        var result = await scanner.ScanAsync(CancellationToken.None);

        Assert.Equal("crash", scanner.Id);
        Assert.NotEmpty(result.Observations);
        Assert.Contains(result.Observations, o => o.Kind == CrashNames.ObservationDumpFile);
        Assert.Contains(result.Observations, o => o.Kind == CrashNames.ObservationAppCrash && o.Subject == "explorer.exe");
        Assert.Contains(result.Observations, o => o.Kind == CrashNames.ObservationBsod);
    }

    [Fact]
    public void CrashKernelMinidumpRule_triggers_when_bsod_or_dumps_exist()
    {
        var rule = new CrashKernelMinidumpRule();
        var bsodObs = new Observation
        {
            Kind = CrashNames.ObservationBsod,
            Subject = "RecentBSOD",
            ScannerId = "crash",
            Attributes = new Dictionary<string, string>
            {
                [CrashNames.AttrBugCheck] = "0x000000D1 (DRIVER_IRQL_NOT_LESS_OR_EQUAL)"
            }
        };

        var findings = Run(rule, [bsodObs]);
        var finding = Assert.Single(findings);

        Assert.Equal("crash.kernel-minidump-detected", finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
        Assert.Contains("0x000000D1", finding.Description);
    }

    private sealed class FakeCrashProvider : ICrashAnalyzerProvider
    {
        private readonly CrashSummary _summary;

        public FakeCrashProvider(CrashSummary summary)
        {
            _summary = summary;
        }

        public Task<CrashSummary> GetCrashSummaryAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_summary);
    }
}
