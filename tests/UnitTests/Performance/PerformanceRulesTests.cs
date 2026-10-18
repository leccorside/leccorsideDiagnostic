using System.Globalization;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Performance;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.Performance;

/// <summary>Cada regra é testada com uma série que NÃO deve disparar e uma que deve (seção 49).</summary>
public class PerformanceRulesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static List<Finding> Run(IRule rule, string scannerId, IEnumerable<Metric> metrics, IEnumerable<Observation>? observations = null)
    {
        var engine = new RuleEngine([rule], new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance);
        var result = new DiagnosticResult
        {
            ScannerId = scannerId,
            ScannerName = scannerId,
            Module = ScanModule.Performance,
            Status = ScannerRunStatus.Completed,
            Metrics = metrics.ToList(),
            Observations = observations?.ToList() ?? [],
        };
        return engine.Evaluate([result]).SelectMany(r => r.Findings).ToList();
    }

    private static Metric M(string name, double value, DateTimeOffset at, string scannerId, string source = PerfNames.SourceHistory) => new()
    {
        Name = name,
        Value = value,
        Unit = "%",
        ScannerId = scannerId,
        Timestamp = at,
        Tags = new Dictionary<string, string> { [PerfNames.SourceTag] = source },
    };

    private static IEnumerable<Metric> Series(string name, string scannerId, int count, TimeSpan step, Func<int, double> value, string source = PerfNames.SourceHistory) =>
        Enumerable.Range(0, count).Select(i => M(name, value(i), T0 + (step * i), scannerId, source));

    private static Observation TopCpu(string name, double percent) => new()
    {
        Kind = PerfNames.ObservationProcessCpu,
        Subject = name,
        ScannerId = CpuScanner.ScannerId,
        Attributes = new Dictionary<string, string> { ["name"] = name, ["pid"] = "42", ["cpuPercent"] = percent.ToString(CultureInfo.InvariantCulture), ["path"] = @"C:\x\" + name + ".exe" },
    };

    // ---------- CPU sustentada ----------

    [Fact]
    public void Cpu_short_spikes_do_not_create_a_finding()
    {
        // 10 min de histórico a cada 5 s, com picos de 100% de até 30 s.
        var metrics = Series(PerfNames.CpuTotal, CpuScanner.ScannerId, 120, TimeSpan.FromSeconds(5), i => i % 20 < 6 ? 100 : 20);

        Assert.Empty(Run(new CpuSustainedHighRule(), CpuScanner.ScannerId, metrics));
    }

    [Fact]
    public void Cpu_sustained_load_creates_a_finding_with_evidence_and_probable_cause()
    {
        var metrics = Series(PerfNames.CpuTotal, CpuScanner.ScannerId, 120, TimeSpan.FromSeconds(5), _ => 97);

        var finding = Assert.Single(Run(new CpuSustainedHighRule(), CpuScanner.ScannerId, metrics, [TopCpu("node", 60), TopCpu("chrome", 12)]));

        Assert.Equal(Severity.Medium, finding.Severity); // ~10 min (< 15 min)
        Assert.True(finding.Confidence.IsConfirmed);
        Assert.Contains("node", finding.ProbableCause, StringComparison.Ordinal);
        Assert.Contains(finding.Evidence, e => e.Source == EvidenceSource.Process && e.Description.Contains("node", StringComparison.Ordinal));
    }

    [Fact]
    public void Cpu_sustained_beyond_15_minutes_is_high_severity()
    {
        var metrics = Series(PerfNames.CpuTotal, CpuScanner.ScannerId, 240, TimeSpan.FromSeconds(5), _ => 99);

        Assert.Equal(Severity.High, Assert.Single(Run(new CpuSustainedHighRule(), CpuScanner.ScannerId, metrics)).Severity);
    }

    [Fact]
    public void Cpu_scan_window_alone_is_not_enough_to_conclude()
    {
        // Só os 15 s da varredura a 100%: não há como afirmar sustentação.
        var metrics = Series(PerfNames.CpuTotal, CpuScanner.ScannerId, 15, TimeSpan.FromSeconds(1), _ => 100, PerfNames.SourceScan);

        Assert.Empty(Run(new CpuSustainedHighRule(), CpuScanner.ScannerId, metrics));
    }

    // ---------- Throttling ----------

    [Fact]
    public void Low_frequency_at_idle_is_power_saving_not_throttling()
    {
        var step = TimeSpan.FromSeconds(1);
        var metrics = Series(PerfNames.CpuTotal, CpuScanner.ScannerId, 10, step, _ => 5, PerfNames.SourceScan)
            .Concat(Series(PerfNames.CpuPerformance, CpuScanner.ScannerId, 10, step, _ => 30, PerfNames.SourceScan));

        Assert.Empty(Run(new CpuThrottlingRule(), CpuScanner.ScannerId, metrics));
    }

    [Fact]
    public void Low_frequency_under_load_is_reported_as_possible_throttling()
    {
        var step = TimeSpan.FromSeconds(1);
        var metrics = Series(PerfNames.CpuTotal, CpuScanner.ScannerId, 10, step, _ => 85, PerfNames.SourceScan)
            .Concat(Series(PerfNames.CpuPerformance, CpuScanner.ScannerId, 10, step, _ => 40, PerfNames.SourceScan))
            .Concat(Series(PerfNames.CpuBaseFrequency, CpuScanner.ScannerId, 10, step, _ => 2900, PerfNames.SourceScan))
            .Concat(Series(PerfNames.CpuEffectiveFrequency, CpuScanner.ScannerId, 10, step, _ => 1160, PerfNames.SourceScan));

        var finding = Assert.Single(Run(new CpuThrottlingRule(), CpuScanner.ScannerId, metrics));

        Assert.Equal(ConfidenceLevel.Possible, finding.Confidence.Level);
        Assert.Equal(Severity.Low, finding.Severity);
        Assert.Equal(2, finding.Evidence.Count);
    }

    [Fact]
    public void Turbo_under_load_is_healthy()
    {
        var step = TimeSpan.FromSeconds(1);
        var metrics = Series(PerfNames.CpuTotal, CpuScanner.ScannerId, 10, step, _ => 90, PerfNames.SourceScan)
            .Concat(Series(PerfNames.CpuPerformance, CpuScanner.ScannerId, 10, step, _ => 135, PerfNames.SourceScan));

        Assert.Empty(Run(new CpuThrottlingRule(), CpuScanner.ScannerId, metrics));
    }

    // ---------- Pressão de memória ----------

    [Fact]
    public void Memory_occasional_peaks_do_not_create_a_finding()
    {
        var metrics = Series(PerfNames.MemoryUsed, MemoryScanner.ScannerId, 60, TimeSpan.FromSeconds(5), i => i % 3 == 0 ? 95 : 70);

        Assert.Empty(Run(new MemoryPressureRule(), MemoryScanner.ScannerId, metrics));
    }

    [Fact]
    public void Memory_pressure_is_medium_and_becomes_high_with_heavy_paging()
    {
        var used = Series(PerfNames.MemoryUsed, MemoryScanner.ScannerId, 60, TimeSpan.FromSeconds(5), _ => 94).ToList();
        var lightPaging = Series(PerfNames.MemoryPagesInput, MemoryScanner.ScannerId, 15, TimeSpan.FromSeconds(1), _ => 10, PerfNames.SourceScan);
        var heavyPaging = Series(PerfNames.MemoryPagesInput, MemoryScanner.ScannerId, 15, TimeSpan.FromSeconds(1), _ => 450, PerfNames.SourceScan);

        var medium = Assert.Single(Run(new MemoryPressureRule(), MemoryScanner.ScannerId, used.Concat(lightPaging)));
        var high = Assert.Single(Run(new MemoryPressureRule(), MemoryScanner.ScannerId, used.Concat(heavyPaging)));

        Assert.Equal(Severity.Medium, medium.Severity);
        Assert.Equal(Severity.High, high.Severity);
        Assert.Contains(high.Evidence, e => e.Reference == @"\Memory\Pages Input/sec");
    }

    // ---------- Commit ----------

    [Fact]
    public void Commit_near_limit_is_high_severity()
    {
        var commit = Series(PerfNames.MemoryCommit, MemoryScanner.ScannerId, 30, TimeSpan.FromSeconds(5), _ => 96);
        var healthy = Series(PerfNames.MemoryCommit, MemoryScanner.ScannerId, 30, TimeSpan.FromSeconds(5), _ => 60);

        Assert.Equal(Severity.High, Assert.Single(Run(new CommitChargeHighRule(), MemoryScanner.ScannerId, commit)).Severity);
        Assert.Empty(Run(new CommitChargeHighRule(), MemoryScanner.ScannerId, healthy));
    }

    // ---------- Possível vazamento ----------

    private static IEnumerable<Metric> ProcessSeries(string name, int pid, int count, Func<int, double> megabytes) =>
        Enumerable.Range(0, count).Select(i => new Metric
        {
            Name = PerfNames.ProcessPrivate,
            Value = megabytes(i) * 1024 * 1024,
            Unit = "bytes",
            ScannerId = MemoryScanner.ScannerId,
            Timestamp = T0.AddMinutes(i),
            Tags = new Dictionary<string, string> { ["pid"] = pid.ToString(CultureInfo.InvariantCulture), ["name"] = name, ["start"] = "2026-10-17T08:00:00Z" },
        });

    [Fact]
    public void Steady_growth_is_a_possible_leak_but_oscillation_is_not()
    {
        var leaking = ProcessSeries("servico.exe", 1234, 20, i => 300 + (i * 60));          // +1,1 GB em 19 min, sempre subindo
        var oscillating = ProcessSeries("chrome", 99, 20, i => 800 + (i % 2 == 0 ? i * 60 : -50)); // sobe e desce
        var smallGrowth = ProcessSeries("code", 77, 20, i => 400 + (i * 10));                // +190 MB

        var findings = Run(new PossibleMemoryLeakRule(), MemoryScanner.ScannerId, leaking.Concat(oscillating).Concat(smallGrowth));

        var leak = Assert.Single(findings);
        Assert.Contains("servico.exe", leak.Title, StringComparison.Ordinal);
        Assert.Equal(ConfidenceLevel.Likely, leak.Confidence.Level);
        Assert.False(leak.Confidence.IsConfirmed);
        Assert.Equal(Severity.Low, leak.Severity);
    }

    [Fact]
    public void Leak_rule_requires_enough_observation_time()
    {
        var shortWindow = ProcessSeries("servico.exe", 1234, 5, i => 300 + (i * 400));

        Assert.Empty(Run(new PossibleMemoryLeakRule(), MemoryScanner.ScannerId, shortWindow));
    }

    [Fact]
    public void Every_performance_rule_declares_justified_parameters()
    {
        IRule[] rules = [new CpuSustainedHighRule(), new CpuThrottlingRule(), new MemoryPressureRule(), new CommitChargeHighRule(), new PossibleMemoryLeakRule()];

        Assert.All(rules.SelectMany(r => r.Parameters), p => Assert.True(p.Justification.Length > 20, $"{p.Key} sem justificativa"));
        Assert.Equal(rules.Length, rules.Select(r => r.Id).Distinct().Count());
    }
}
