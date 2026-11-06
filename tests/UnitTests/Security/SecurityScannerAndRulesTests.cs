using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Security;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Security;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.Security;

public class SecurityScannerAndRulesTests
{
#pragma warning disable CA1859
    private static List<Finding> Run(IRule rule, IEnumerable<Observation> observations)
#pragma warning restore CA1859
    {
        var engine = new RuleEngine([rule], new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance);
        var result = new DiagnosticResult
        {
            ScannerId = rule.RequiredScanners[0],
            ScannerName = rule.RequiredScanners[0],
            Module = ScanModule.Windows,
            Status = ScannerRunStatus.Completed,
            Metrics = [],
            Observations = observations.ToList(),
        };
        return engine.Evaluate([result]).SelectMany(r => r.Findings).ToList();
    }

    [Fact]
    public async Task SecurityScanner_collects_antivirus_uac_and_persistences()
    {
        var summary = new SecurityStatusSummary
        {
            AntivirusProducts =
            [
                new AntivirusProductInfo { Name = "Windows Defender", InstanceGuid = "{123}", PathToSignedProductExe = "C:\\Windows\\Defender.exe", IsEnabled = true, IsUpToDate = true, IsWindowsDefender = true }
            ],
            FirewallProducts =
            [
                new FirewallProductInfo { Name = "Windows Firewall", InstanceGuid = "{456}", IsEnabled = true }
            ],
            IsUacEnabled = true,
            IsRealTimeProtectionActive = true,
            IsTamperProtectionActive = true,
            SuspiciousPersistences =
            [
                new SuspiciousPersistenceItem { Target = "IFEO\\sethc.exe", Type = "IFEO Debugger Hijack", Description = "Debugger ativo", Details = "cmd.exe", IsSuspicious = true }
            ],
            CapturedAt = DateTimeOffset.UtcNow
        };

        var provider = new FakeSecurityProvider(summary);
        var scanner = new SecurityScanner(provider, NullLogger<SecurityScanner>.Instance);

        var result = await scanner.ScanAsync(CancellationToken.None);

        Assert.Equal("security", scanner.Id);
        Assert.NotEmpty(result.Observations);
        Assert.Contains(result.Observations, o => o.Kind == SecurityNames.ObservationAntivirus);
        Assert.Contains(result.Observations, o => o.Kind == SecurityNames.ObservationUac);
        Assert.Contains(result.Observations, o => o.Kind == SecurityNames.ObservationPersistence);
    }

    [Fact]
    public void SecurityAntivirusDisabledRule_triggers_when_all_av_are_disabled()
    {
        var rule = new SecurityAntivirusDisabledRule();
        var avObs = new Observation
        {
            Kind = SecurityNames.ObservationAntivirus,
            Subject = "Windows Defender",
            ScannerId = "security",
            Attributes = new Dictionary<string, string>
            {
                [SecurityNames.AttrName] = "Windows Defender",
                [SecurityNames.AttrEnabled] = "false"
            }
        };

        var findings = Run(rule, [avObs]);
        var finding = Assert.Single(findings);

        Assert.Equal("security.antivirus-disabled", finding.RuleId);
        Assert.Equal(Severity.Critical, finding.Severity);
    }

    [Fact]
    public void SecurityUacDisabledRule_triggers_when_uac_is_false()
    {
        var rule = new SecurityUacDisabledRule();
        var uacObs = new Observation
        {
            Kind = SecurityNames.ObservationUac,
            Subject = "UAC",
            ScannerId = "security",
            Attributes = new Dictionary<string, string>
            {
                [SecurityNames.AttrUacEnabled] = "false"
            }
        };

        var findings = Run(rule, [uacObs]);
        var finding = Assert.Single(findings);

        Assert.Equal("security.uac-disabled", finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
    }

    [Fact]
    public void SecurityIfeoHijackRule_triggers_when_suspicious_persistence_found()
    {
        var rule = new SecurityIfeoHijackRule();
        var pObs = new Observation
        {
            Kind = SecurityNames.ObservationPersistence,
            Subject = "IFEO\\sethc.exe",
            ScannerId = "security",
            Attributes = new Dictionary<string, string>
            {
                [SecurityNames.AttrPersistenceType] = "IFEO Debugger Hijack",
                [SecurityNames.AttrSuspicious] = "true"
            }
        };

        var findings = Run(rule, [pObs]);
        var finding = Assert.Single(findings);

        Assert.Equal("security.ifeo-hijack", finding.RuleId);
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Contains("IFEO\\sethc.exe", finding.Title);
    }

    private sealed class FakeSecurityProvider : ISecurityProvider
    {
        private readonly SecurityStatusSummary _summary;

        public FakeSecurityProvider(SecurityStatusSummary summary)
        {
            _summary = summary;
        }

        public Task<SecurityStatusSummary> GetSecurityStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_summary);
    }
}
