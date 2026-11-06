using LeccorWindowsHealthCenter.Application.Apps;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Apps;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.Apps;

public class AppsRulesTests
{
    private static List<Finding> Run(IRule rule, IEnumerable<Observation> observations)
    {
        var engine = new RuleEngine([rule], new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance);
        var result = new DiagnosticResult
        {
            ScannerId = AppsScanner.ScannerId,
            ScannerName = AppsScanner.ScannerId,
            Module = ScanModule.Applications,
            Status = ScannerRunStatus.Completed,
            Metrics = [],
            Observations = observations.ToList(),
        };
        return engine.Evaluate([result]).SelectMany(r => r.Findings).ToList();
    }

    private static Observation CreateAppObservation(
        string appName,
        string version,
        string publisher,
        string uninstallString,
        bool isOrphaned,
        bool hasDuplicate = false) => new()
    {
        Kind = AppsNames.ObservationApp,
        Subject = appName,
        ScannerId = AppsScanner.ScannerId,
        Attributes = new Dictionary<string, string>
        {
            [AppsNames.AttrName] = appName,
            [AppsNames.AttrVersion] = version,
            [AppsNames.AttrPublisher] = publisher,
            [AppsNames.AttrUninstallString] = uninstallString,
            [AppsNames.AttrIsOrphaned] = isOrphaned ? "true" : "false",
            [AppsNames.AttrHasDuplicate] = hasDuplicate ? "true" : "false",
        },
    };

    [Fact]
    public void AppsOrphanedUninstallRule_Triggers_WhenUninstallFileDoesNotExist()
    {
        var rule = new AppsOrphanedUninstallRule();
        var obs = CreateAppObservation("OldTool", "1.0", "Vendor A", @"C:\OldTool\unins000.exe", isOrphaned: true);

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.Low, finding.Severity);
        Assert.Equal(FindingCategory.Applications, finding.Category);
        Assert.Contains("OldTool", finding.Title);
    }

    [Fact]
    public void AppsOrphanedUninstallRule_DoesNotTrigger_WhenUninstallerExists()
    {
        var rule = new AppsOrphanedUninstallRule();
        var obs = CreateAppObservation("GoodTool", "2.0", "Vendor B", @"C:\Windows\System32\cmd.exe", isOrphaned: false);

        var findings = Run(rule, [obs]);

        Assert.Empty(findings);
    }

    [Fact]
    public void AppsDuplicateInstallRule_Triggers_WhenAppHasDuplicateVersions()
    {
        var rule = new AppsDuplicateInstallRule();
        var obs1 = CreateAppObservation("CodeEditor", "1.80.0", "Vendor", @"C:\Apps\uninstall.exe", isOrphaned: false, hasDuplicate: true);
        var obs2 = CreateAppObservation("CodeEditor", "1.85.0", "Vendor", @"C:\Apps\uninstall.exe", isOrphaned: false, hasDuplicate: true);

        var findings = Run(rule, [obs1, obs2]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Equal(FindingCategory.Applications, finding.Category);
        Assert.Contains("CodeEditor", finding.Title);
    }

    [Fact]
    public void AppsDuplicateInstallRule_DoesNotTrigger_WhenNoDuplicates()
    {
        var rule = new AppsDuplicateInstallRule();
        var obs = CreateAppObservation("CodeEditor", "1.85.0", "Vendor", @"C:\Apps\uninstall.exe", isOrphaned: false, hasDuplicate: false);

        var findings = Run(rule, [obs]);

        Assert.Empty(findings);
    }
}

