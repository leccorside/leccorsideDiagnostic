using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.WindowsUpdate;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Diagnostics.WindowsUpdate;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.WindowsUpdate;

public class WindowsUpdateRulesTests
{
    private static List<Finding> Run(IRule rule, IEnumerable<Observation> observations)
    {
        var engine = new RuleEngine([rule], new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance);
        var result = new DiagnosticResult
        {
            ScannerId = WindowsUpdateScanner.ScannerId,
            ScannerName = WindowsUpdateScanner.ScannerId,
            Module = ScanModule.WindowsUpdate,
            Status = ScannerRunStatus.Completed,
            Metrics = [],
            Observations = observations.ToList(),
        };
        return engine.Evaluate([result]).SelectMany(r => r.Findings).ToList();
    }

    private static Observation CreateUpdateStatusObservation(
        string wuauservState,
        bool isRebootPending,
        string? rebootReason = null) => new()
    {
        Kind = WindowsUpdateNames.ObservationStatus,
        Subject = "WindowsUpdate",
        ScannerId = WindowsUpdateScanner.ScannerId,
        Attributes = new Dictionary<string, string>
        {
            [WindowsUpdateNames.AttrWuauservState] = wuauservState,
            [WindowsUpdateNames.AttrBitsState] = "Automatic",
            [WindowsUpdateNames.AttrDosvcState] = "Automatic",
            [WindowsUpdateNames.AttrRebootPending] = isRebootPending ? "true" : "false",
            [WindowsUpdateNames.AttrRebootReason] = rebootReason ?? string.Empty,
        },
    };

    [Fact]
    public void WindowsUpdateServiceDisabledRule_Triggers_WhenWuauservIsDisabled()
    {
        var rule = new WindowsUpdateServiceDisabledRule();
        var obs = CreateUpdateStatusObservation("Disabled", isRebootPending: false);

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
        Assert.Equal(FindingCategory.WindowsUpdate, finding.Category);
        Assert.Contains("desativado", finding.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Automatic")]
    [InlineData("Manual")]
    public void WindowsUpdateServiceDisabledRule_DoesNotTrigger_WhenServiceIsActiveOrManual(string state)
    {
        var rule = new WindowsUpdateServiceDisabledRule();
        var obs = CreateUpdateStatusObservation(state, isRebootPending: false);

        var findings = Run(rule, [obs]);

        Assert.Empty(findings);
    }

    [Fact]
    public void WindowsUpdateRebootPendingRule_Triggers_WhenRebootPendingIsTrue()
    {
        var rule = new WindowsUpdateRebootPendingRule();
        var obs = CreateUpdateStatusObservation("Automatic", isRebootPending: true, "Pacote cumulativo instalado.");

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Equal(FindingCategory.WindowsUpdate, finding.Category);
        Assert.Contains("Reinicialização", finding.Title);
    }

    [Fact]
    public void WindowsUpdateRebootPendingRule_DoesNotTrigger_WhenNoRebootPending()
    {
        var rule = new WindowsUpdateRebootPendingRule();
        var obs = CreateUpdateStatusObservation("Automatic", isRebootPending: false);

        var findings = Run(rule, [obs]);

        Assert.Empty(findings);
    }
}

