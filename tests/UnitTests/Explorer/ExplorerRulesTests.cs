using LeccorWindowsHealthCenter.Application.Explorer;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Explorer;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.Explorer;

public class ExplorerRulesTests
{
    private static List<Finding> Run(IRule rule, IEnumerable<Observation> observations)
    {
        var engine = new RuleEngine([rule], new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance);
        var result = new DiagnosticResult
        {
            ScannerId = ExplorerScanner.ScannerId,
            ScannerName = ExplorerScanner.ScannerId,
            Module = ScanModule.Explorer,
            Status = ScannerRunStatus.Completed,
            Metrics = [],
            Observations = observations.ToList(),
        };
        return engine.Evaluate([result]).SelectMany(r => r.Findings).ToList();
    }

    private static Observation CreateShellExtensionObservation(
        string name,
        string clsid,
        string type,
        string dllPath,
        bool isSigned,
        bool fileExists,
        bool isBlocked = false,
        bool isWindows = false) => new()
    {
        Kind = ExplorerNames.ObservationShellExtension,
        Subject = name,
        ScannerId = ExplorerScanner.ScannerId,
        Attributes = new Dictionary<string, string>
        {
            [ExplorerNames.AttrName] = name,
            [ExplorerNames.AttrClsid] = clsid,
            [ExplorerNames.AttrType] = type,
            [ExplorerNames.AttrDllPath] = dllPath,
            [ExplorerNames.AttrSigned] = isSigned ? "true" : "false",
            [ExplorerNames.AttrExists] = fileExists ? "true" : "false",
            [ExplorerNames.AttrBlocked] = isBlocked ? "true" : "false",
            [ExplorerNames.AttrWindows] = isWindows ? "true" : "false",
        },
    };

    [Fact]
    public void ExplorerMissingShellExtensionRule_Triggers_WhenDllFileDoesNotExist()
    {
        var rule = new ExplorerMissingShellExtensionRule();
        var obs = CreateShellExtensionObservation("OrphanExtension", "{11111111-2222-3333-4444-555555555555}", "ContextMenuHandler", @"C:\NonExistent\orphan.dll", isSigned: false, fileExists: false);

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.Low, finding.Severity);
        Assert.Equal(FindingCategory.Explorer, finding.Category);
        Assert.Contains("OrphanExtension", finding.Title);
    }

    [Fact]
    public void ExplorerMissingShellExtensionRule_DoesNotTrigger_WhenDllFileExists()
    {
        var rule = new ExplorerMissingShellExtensionRule();
        var obs = CreateShellExtensionObservation("ValidExtension", "{11111111-2222-3333-4444-555555555555}", "ContextMenuHandler", @"C:\Windows\System32\shell32.dll", isSigned: true, fileExists: true);

        var findings = Run(rule, [obs]);

        Assert.Empty(findings);
    }

    [Fact]
    public void ExplorerUnsignedShellExtensionRule_Triggers_WhenExtensionIsUnsignedAndExists()
    {
        var rule = new ExplorerUnsignedShellExtensionRule();
        var obs = CreateShellExtensionObservation("CustomTool", "{22222222-3333-4444-5555-666666666666}", "ContextMenuHandler", @"C:\Tools\tool.dll", isSigned: false, fileExists: true);

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Equal(FindingCategory.Explorer, finding.Category);
        Assert.Contains("CustomTool", finding.Title);
    }

    [Fact]
    public void ExplorerUnsignedShellExtensionRule_DoesNotTrigger_WhenExtensionIsSigned()
    {
        var rule = new ExplorerUnsignedShellExtensionRule();
        var obs = CreateShellExtensionObservation("SignedTool", "{22222222-3333-4444-5555-666666666666}", "ContextMenuHandler", @"C:\Tools\signed.dll", isSigned: true, fileExists: true);

        var findings = Run(rule, [obs]);

        Assert.Empty(findings);
    }

    [Fact]
    public void ExplorerUnsignedShellExtensionRule_TriggersHighSeverity_WhenCertificateIsRevoked()
    {
        var rule = new ExplorerUnsignedShellExtensionRule();
        var obs = new Observation
        {
            Kind = ExplorerNames.ObservationShellExtension,
            Subject = "RevokedTool",
            ScannerId = ExplorerScanner.ScannerId,
            Attributes = new Dictionary<string, string>
            {
                [ExplorerNames.AttrName] = "RevokedTool",
                [ExplorerNames.AttrClsid] = "{99999999-8888-7777-6666-555555555555}",
                [ExplorerNames.AttrType] = "ContextMenuHandler",
                [ExplorerNames.AttrDllPath] = @"C:\Malware\compromised.dll",
                [ExplorerNames.AttrSigned] = "true",
                [ExplorerNames.AttrRevoked] = "true",
                [ExplorerNames.AttrExists] = "true",
                [ExplorerNames.AttrWindows] = "false",
            },
        };

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
        Assert.Contains("REVOGADO", finding.Title);
        Assert.Equal(100, finding.Confidence.Percent);
    }
}
