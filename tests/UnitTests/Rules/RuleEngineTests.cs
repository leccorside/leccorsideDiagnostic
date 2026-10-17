using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.Rules;

public class RuleEngineTests
{
    private static RuleEngine Engine(IRuleSettings? settings = null, params IRule[] rules) =>
        new(rules, settings ?? new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance);

    private static DiagnosticResult Result(string id, ScannerRunStatus status = ScannerRunStatus.Completed, params Metric[] metrics) => new()
    {
        ScannerId = id,
        ScannerName = id,
        Module = ScanModule.Performance,
        Status = status,
        Metrics = metrics,
    };

    [Fact]
    public void Attaches_findings_to_the_scanner_that_owns_them()
    {
        var rule = new DelegateRule("r1", ["cpu"], _ => [Findings.Make("cpu")]);

        var results = Engine(null, rule).Evaluate([Result("cpu"), Result("ram")]);

        Assert.Single(results.Single(r => r.ScannerId == "cpu").Findings);
        Assert.Empty(results.Single(r => r.ScannerId == "ram").Findings);
        Assert.Equal("r1", results[0].Findings[0].RuleId);
    }

    [Theory]
    [InlineData(ScannerRunStatus.Failed)]
    [InlineData(ScannerRunStatus.Cancelled)]
    public void Does_not_run_rule_when_required_scanner_did_not_complete(ScannerRunStatus status)
    {
        var executed = false;
        var rule = new DelegateRule("r1", ["cpu"], _ =>
        {
            executed = true;
            return [];
        });

        Engine(null, rule).Evaluate([Result("cpu", status)]);

        Assert.False(executed);
    }

    [Fact]
    public void Requires_all_declared_scanners()
    {
        var executed = false;
        var rule = new DelegateRule("r1", ["cpu", "eventlog"], _ =>
        {
            executed = true;
            return [];
        });

        Engine(null, rule).Evaluate([Result("cpu")]);

        Assert.False(executed);
    }

    [Fact]
    public void Discards_findings_without_evidence()
    {
        var rule = new DelegateRule("r1", ["cpu"], _ => [Findings.Make(withEvidence: false)]);

        var results = Engine(null, rule).Evaluate([Result("cpu")]);

        Assert.Empty(results[0].Findings);
    }

    [Fact]
    public void Discards_findings_with_repair_but_without_declared_risk()
    {
        var rule = new DelegateRule("r1", ["cpu"], _ => [Findings.Make() with { RepairActionId = "x" }]);

        var results = Engine(null, rule).Evaluate([Result("cpu")]);

        Assert.Empty(results[0].Findings);
    }

    [Fact]
    public void A_failing_rule_does_not_stop_the_others()
    {
        var broken = new DelegateRule("broken", ["cpu"], _ => throw new InvalidOperationException("bug"));
        var healthy = new DelegateRule("healthy", ["cpu"], _ => [Findings.Make()]);

        var results = Engine(null, broken, healthy).Evaluate([Result("cpu")]);

        Assert.Single(results[0].Findings);
    }

    [Fact]
    public void Disabled_rule_is_skipped()
    {
        var settings = new FakeRuleSettings();
        settings.Disabled.Add("r1");
        var rule = new DelegateRule("r1", ["cpu"], _ => [Findings.Make()]);

        var results = Engine(settings, rule).Evaluate([Result("cpu")]);

        Assert.Empty(results[0].Findings);
    }

    [Fact]
    public void Parameter_uses_configuration_override_and_falls_back_to_default()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rules:r1:Threshold"] = "75.5",
                ["Rules:r2:Enabled"] = "false",
            })
            .Build();
        var settings = new ConfigurationRuleSettings(configuration);
        double threshold = 0, minimum = 0;
        var rule = new DelegateRule("r1", ["cpu"], ctx =>
            {
                threshold = ctx.Parameter("Threshold");
                minimum = ctx.Parameter("MinimumSamples");
                return [];
            },
            new RuleParameter("Threshold", 90, "%", "justificativa"),
            new RuleParameter("MinimumSamples", 30, "amostras", "justificativa"));

        Engine(settings, rule).Evaluate([Result("cpu")]);

        Assert.Equal(75.5, threshold);
        Assert.Equal(30, minimum);
        Assert.False(settings.IsEnabled("r2"));
        Assert.True(settings.IsEnabled("r1"));
    }

    [Fact]
    public void Undeclared_parameter_is_a_programming_error()
    {
        var rule = new DelegateRule("r1", ["cpu"], ctx => [Findings.Make() with { Title = ctx.Parameter("Nope").ToString(System.Globalization.CultureInfo.InvariantCulture) }]);

        // O erro é isolado pelo engine (regra falha, varredura continua).
        var results = Engine(null, rule).Evaluate([Result("cpu")]);

        Assert.Empty(results[0].Findings);
    }

    /// <summary>
    /// Seção 49: CPU 100% por 2 segundos NÃO gera problema; CPU &gt; 90% sustentada e recorrente gera.
    /// A regra de exemplo mostra o padrão que as regras reais devem seguir.
    /// </summary>
    [Fact]
    public void False_positive_guard_short_spike_vs_sustained_load()
    {
        var rule = new DelegateRule("cpu.sustained-high", ["cpu"], ctx =>
            {
                var series = ctx.Series("cpu.total");
                var threshold = ctx.Parameter("Threshold");
                if (series.Count < ctx.Parameter("MinimumSamples") ||
                    series.LongestRunAtOrAbove(threshold) < TimeSpan.FromSeconds(ctx.Parameter("MinimumSustainedSeconds")))
                {
                    return [];
                }

                return [Findings.Make() with { Title = "CPU alta sustentada" }];
            },
            new RuleParameter("Threshold", 90, "%", "Acima de 90% sobra pouca margem para tarefas interativas."),
            new RuleParameter("MinimumSamples", 10, "amostras", "Poucas amostras não representam o comportamento."),
            new RuleParameter("MinimumSustainedSeconds", 60, "s", "Picos curtos são normais (compilação, abertura de apps)."));

        var start = DateTimeOffset.UtcNow;
        var spike = Enumerable.Range(0, 30)
            .Select(i => Findings.Sample("cpu.total", i is 10 or 11 ? 100 : 15, start.AddSeconds(i * 5)))
            .ToArray();
        var sustained = Enumerable.Range(0, 30)
            .Select(i => Findings.Sample("cpu.total", 95, start.AddSeconds(i * 5)))
            .ToArray();

        var spikeResult = Engine(null, rule).Evaluate([Result("cpu", ScannerRunStatus.Completed, spike)]);
        var sustainedResult = Engine(null, rule).Evaluate([Result("cpu", ScannerRunStatus.Completed, sustained)]);

        Assert.Empty(spikeResult[0].Findings);
        Assert.Single(sustainedResult[0].Findings);
    }
}
