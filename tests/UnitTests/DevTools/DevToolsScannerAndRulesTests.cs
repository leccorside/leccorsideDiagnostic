using System.Globalization;
using LeccorWindowsHealthCenter.Application.DevTools;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.DevTools;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.DevTools;

public class DevToolsScannerAndRulesTests
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
            Module = ScanModule.Applications,
            Status = ScannerRunStatus.Completed,
            Metrics = [],
            Observations = observations.ToList(),
        };
        return engine.Evaluate([result]).SelectMany(r => r.Findings).ToList();
    }

    [Fact]
    public async Task DevToolsScanner_collects_tools_wsl_docker_and_path()
    {
        var fakeSummary = new DevEnvironmentSummary
        {
            Tools =
            [
                new DevToolItem { Id = "node", Name = "Node.js", Category = "Runtime", IsInstalled = true, Version = "v20.10.0" },
                new DevToolItem { Id = "git", Name = "Git", Category = "VCS", IsInstalled = true, Version = "2.43.0" }
            ],
            WslDistributions =
            [
                new WslDistributionInfo { Name = "Ubuntu-22.04", WslVersion = 2, State = "Running", IsDefault = true, VhdxSizeBytes = 10L * 1024 * 1024 * 1024 }
            ],
            Docker = new DockerStatusInfo { IsInstalled = true, IsRunning = true, Backend = "WSL2", TotalDiskUsageBytes = 25L * 1024 * 1024 * 1024, RunningContainers = 2, TotalContainers = 5, TotalImages = 10, TotalVolumes = 3 },
            PathAnalysis = new PathEnvironmentAnalysis { UserEntries = ["C:\\Tools"], SystemEntries = ["C:\\Windows"], InvalidEntries = ["D:\\DeletedTool"], TotalLength = 500 },
            HasHyperV = true,
            CapturedAt = DateTimeOffset.UtcNow
        };

        var provider = new FakeDevToolsProvider(fakeSummary);
        var scanner = new DevToolsScanner(provider, NullLogger<DevToolsScanner>.Instance);

        var result = await scanner.ScanAsync(CancellationToken.None);

        Assert.Equal("devtools", scanner.Id);
        Assert.NotEmpty(result.Observations);
        Assert.Contains(result.Observations, o => o.Kind == DevToolsNames.ObservationDevTool && o.Subject == "Node.js");
        Assert.Contains(result.Observations, o => o.Kind == DevToolsNames.ObservationWslDistro && o.Subject == "Ubuntu-22.04");
        Assert.Contains(result.Observations, o => o.Kind == DevToolsNames.ObservationDocker);
        Assert.Contains(result.Observations, o => o.Kind == DevToolsNames.ObservationPath);
    }

    [Fact]
    public void DevWslVhdxLargeRule_triggers_when_vhdx_exceeds_50gb()
    {
        var rule = new DevWslVhdxLargeRule();
        var obsLarge = new Observation
        {
            Kind = DevToolsNames.ObservationWslDistro,
            Subject = "Ubuntu-Heavy",
            ScannerId = "devtools",
            Attributes = new Dictionary<string, string>
            {
                [DevToolsNames.AttrWslName] = "Ubuntu-Heavy",
                [DevToolsNames.AttrWslVhdxSize] = (60L * 1024 * 1024 * 1024).ToString(CultureInfo.InvariantCulture)
            }
        };

        var findings = Run(rule, [obsLarge]);
        var finding = Assert.Single(findings);

        Assert.Equal("dev.wsl-vhdx-large", finding.RuleId);
        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Contains("60,0 GB", finding.Title);
    }

    [Fact]
    public void DevPathInvalidEntriesRule_triggers_when_invalid_paths_exist()
    {
        var rule = new DevPathInvalidEntriesRule();
        var pathObs = new Observation
        {
            Kind = DevToolsNames.ObservationPath,
            Subject = "EnvironmentPath",
            ScannerId = "devtools",
            Attributes = new Dictionary<string, string>
            {
                [DevToolsNames.AttrPathInvalidCount] = "3"
            }
        };

        var findings = Run(rule, [pathObs]);
        var finding = Assert.Single(findings);

        Assert.Equal("dev.path-invalid-entries", finding.RuleId);
        Assert.Equal(Severity.Low, finding.Severity);
        Assert.Contains("3 diretório(s) inexistente(s)", finding.Title);
    }

    private sealed class FakeDevToolsProvider : IDevToolsProvider
    {
        private readonly DevEnvironmentSummary _summary;

        public FakeDevToolsProvider(DevEnvironmentSummary summary)
        {
            _summary = summary;
        }

        public Task<DevEnvironmentSummary> GetDevEnvironmentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_summary);
    }
}
