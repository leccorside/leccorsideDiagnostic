using LeccorWindowsHealthCenter.Application.Scoring;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.UnitTests.Scoring;

public class ScoringEngineTests
{
    private readonly ScoringEngine _engine = new();

    private static DiagnosticResult Result(ScanModule module, ScannerRunStatus status = ScannerRunStatus.Completed, params Finding[] findings) => new()
    {
        ScannerId = module.ToString(),
        ScannerName = module.ToString(),
        Module = module,
        Status = status,
        Findings = findings,
    };

    [Fact]
    public void No_completed_scanner_means_no_score()
    {
        var score = _engine.Calculate([Result(ScanModule.Performance, ScannerRunStatus.Failed)]);

        Assert.Null(score.Overall);
        Assert.Equal(HealthRating.Unknown, score.Rating);
    }

    [Fact]
    public void Empty_scan_means_no_score()
    {
        var score = _engine.Calculate([]);

        Assert.Null(score.Overall);
    }

    [Fact]
    public void Healthy_measured_categories_score_100()
    {
        var score = _engine.Calculate([Result(ScanModule.Performance), Result(ScanModule.Disks)]);

        Assert.Equal(100, score.Overall);
        Assert.Equal(HealthRating.Excellent, score.Rating);
        Assert.Contains(ScoreCategory.Performance, score.Categories.Keys);
        Assert.Contains(ScoreCategory.Storage, score.Categories.Keys);
        Assert.DoesNotContain(ScoreCategory.Security, score.Categories.Keys);
    }

    [Fact]
    public void Recommendations_never_reduce_the_score()
    {
        var info = Findings.Make(severity: Severity.Info);

        var score = _engine.Calculate([Result(ScanModule.Performance, ScannerRunStatus.Completed, info, info, info)]);

        Assert.Equal(100, score.Overall);
    }

    [Fact]
    public void Possible_problem_weighs_less_than_confirmed()
    {
        var confirmed = _engine.Calculate([Result(ScanModule.Performance, ScannerRunStatus.Completed, Findings.Make(severity: Severity.High, confidence: 100))]);
        var possible = _engine.Calculate([Result(ScanModule.Performance, ScannerRunStatus.Completed, Findings.Make(severity: Severity.High, confidence: 40))]);

        Assert.True(possible.Overall > confirmed.Overall);
    }

    [Fact]
    public void Repeated_findings_of_the_same_rule_have_diminishing_returns()
    {
        var one = Findings.Make(severity: Severity.Medium, ruleId: "events.disk");
        var many = Enumerable.Range(0, 50).Select(_ => Findings.Make(severity: Severity.Medium, ruleId: "events.disk")).ToArray();

        var single = _engine.Calculate([Result(ScanModule.Performance, ScannerRunStatus.Completed, one)]);
        var repeated = _engine.Calculate([Result(ScanModule.Performance, ScannerRunStatus.Completed, many)]);

        // 50 ocorrências idênticas no máximo dobram a dedução de uma ocorrência.
        Assert.Equal(100 - (2 * (100 - single.Categories[ScoreCategory.Performance])), repeated.Categories[ScoreCategory.Performance]);
    }

    [Fact]
    public void Critical_problem_lowers_score_meaningfully()
    {
        var critical = Findings.Make(severity: Severity.Critical, category: FindingCategory.Disk);

        var score = _engine.Calculate([Result(ScanModule.Disks, ScannerRunStatus.Completed, critical)]);

        Assert.Equal(55, score.Categories[ScoreCategory.Hardware]);
        Assert.True(score.Overall < 90);
    }
}
