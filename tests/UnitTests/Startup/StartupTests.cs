using System.Globalization;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Startup;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Startup;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.Startup;

/// <summary>Linhas de comando reais desta máquina (sonda de 19/10/2026) e as regras do Passo 4.</summary>
public class StartupTests
{
    private const string System32 = @"C:\Windows\System32";

    private static string? Parse(string command) =>
        StartupCommand.ExecutablePath(command, s => s.Replace("%ProgramFiles%", @"C:\Program Files", StringComparison.OrdinalIgnoreCase), System32);

    [Theory]
    [InlineData(@"C:\Windows\System32\ctfmon.exe", @"C:\Windows\System32\ctfmon.exe")]
    [InlineData(@"C:\Program Files\OpenVPN Connect\OpenVPNConnect.exe --opened-at-login --minimize", @"C:\Program Files\OpenVPN Connect\OpenVPNConnect.exe")]
    [InlineData("\"C:\\Users\\Jo\\AppData\\Local\\AnthropicClaude\\claude.exe\" --startup", @"C:\Users\Jo\AppData\Local\AnthropicClaude\claude.exe")]
    [InlineData("\"C:\\Program Files\\Google\\Drive File Stream\\132.0.0.0\\GoogleDriveFS.exe\" --startup_mode", @"C:\Program Files\Google\Drive File Stream\132.0.0.0\GoogleDriveFS.exe")]
    [InlineData("%ProgramFiles%\\Tool\\tool.exe -q", @"C:\Program Files\Tool\tool.exe")]
    [InlineData("ctfmon.exe", @"C:\Windows\System32\ctfmon.exe")]
    [InlineData(@"C:\WINDOWS\system32\svchost.exe -k netsvcs -p", @"C:\WINDOWS\system32\svchost.exe")]
    [InlineData(@"\SystemRoot\System32\drivers\foo.sys", @"C:\Windows\System32\drivers\foo.sys")]
    [InlineData(@"rundll32.exe C:\Program Files\Vendor\helper.dll,Start", @"C:\Program Files\Vendor\helper.dll")]
    public void Real_command_lines_resolve_to_the_file_that_must_exist(string command, string expected) =>
        Assert.Equal(expected, Parse(command), ignoreCase: true);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"sem fechamento")]
    public void Unparseable_commands_are_null(string command) => Assert.Null(Parse(command));

    [Theory]
    [InlineData(@"C:\Users\Jo\AppData\Local\Temp\updater\run.exe", true)]
    [InlineData(@"C:\Windows\Temp\x.exe", true)]
    [InlineData(@"C:\Users\Jo\AppData\Local\Programs\App\app.exe", false)]
    [InlineData(@"C:\Program Files\Temperature Monitor\tm.exe", false)] // "Temp" no nome não é pasta temporária
    [InlineData(null, false)]
    public void Temporary_locations(string? path, bool expected) =>
        Assert.Equal(expected, StartupCommand.IsTemporaryLocation(path, @"C:\Users\Jo\AppData\Local\Temp\"));

    private static StartupEntry Entry(bool? enabled = true, string? exe = @"C:\App\app.exe") => new()
    {
        Id = "x", Name = "App", Source = StartupSourceKind.RegistryRun, Location = "HKCU", Enabled = enabled, ExecutablePath = exe,
    };

    private static ProcessDetails Running(long privateMb, double cpuSeconds, string exe = @"C:\App\app.exe") => new()
    {
        Pid = 10, Name = "app", ExecutablePath = exe, PrivateBytes = privateMb * 1024 * 1024, TotalProcessorTime = TimeSpan.FromSeconds(cpuSeconds),
    };

    [Fact]
    public void Impact_is_measured_only_for_what_is_running()
    {
        Assert.Equal(StartupImpactLevel.NotMeasured, StartupImpact.Estimate(Entry(), []));
        Assert.Equal(StartupImpactLevel.Disabled, StartupImpact.Estimate(Entry(enabled: false), [Running(500, 100)]));
        Assert.Equal(StartupImpactLevel.Low, StartupImpact.Estimate(Entry(), [Running(40, 1)]));
        Assert.Equal(StartupImpactLevel.Medium, StartupImpact.Estimate(Entry(), [Running(150, 1)]));
        Assert.Equal(StartupImpactLevel.High, StartupImpact.Estimate(Entry(), [Running(200, 10), Running(150, 25)])); // soma das instâncias
    }

    [Theory]
    [InlineData(4, "System", true)]
    [InlineData(812, "csrss", true)]
    [InlineData(900, "lsass.exe", true)]
    [InlineData(1200, "svchost", true)]
    [InlineData(5000, "explorer", false)] // reiniciar o Explorer é seguro (e útil)
    [InlineData(6000, "chrome", false)]
    public void Critical_processes_are_protected(int pid, string name, bool critical) =>
        Assert.Equal(critical, CriticalProcesses.IsCritical(pid, name));

    // ---------- Regras ----------

    private static List<Finding> Run(IRule rule, params Observation[] observations)
    {
        var engine = new RuleEngine([rule], new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance);
        var result = new DiagnosticResult
        {
            ScannerId = StartupScanner.ScannerId,
            ScannerName = "Inicialização",
            Module = ScanModule.Startup,
            Status = ScannerRunStatus.Completed,
            Observations = observations,
        };
        return engine.Evaluate([result]).SelectMany(r => r.Findings).ToList();
    }

    private static Observation Obs(string exe, bool? exists, bool? enabled, string source = "RegistryRun", bool windows = false) => new()
    {
        Kind = StartupScanner.ObservationEntry,
        Subject = "App",
        ScannerId = StartupScanner.ScannerId,
        Attributes = new Dictionary<string, string>
        {
            ["id"] = "HKCU\\Run\\App",
            ["name"] = "App",
            ["source"] = source,
            ["location"] = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run",
            ["command"] = $"\"{exe}\"",
            ["executable"] = exe,
            ["exists"] = exists?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ["enabled"] = enabled?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ["windows"] = windows.ToString(CultureInfo.InvariantCulture),
        },
    };

    [Fact]
    public void Missing_executable_of_an_enabled_entry_is_reported_with_both_evidences()
    {
        var finding = Assert.Single(Run(new StartupMissingExecutableRule(), Obs(@"C:\Program Files\Gone\gone.exe", exists: false, enabled: true)));

        Assert.Equal(Severity.Low, finding.Severity);
        Assert.Equal(2, finding.Evidence.Count);
        Assert.True(finding.Confidence.IsConfirmed);
    }

    [Fact]
    public void Missing_executable_of_a_service_is_medium() =>
        Assert.Equal(Severity.Medium, Assert.Single(Run(new StartupMissingExecutableRule(), Obs(@"C:\Gone\svc.exe", false, true, "Service"))).Severity);

    [Fact]
    public void Missing_executable_of_a_windows_task_is_informational()
    {
        var finding = Assert.Single(Run(new StartupMissingExecutableRule(), Obs(@"C:\Windows\System32\acproxy.dll", false, true, "ScheduledTask", windows: true)));

        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Contains("Não é preciso fazer nada", finding.Recommendation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, true)]   // existe: nada a dizer
    [InlineData(false, false)] // desativada: não roda
    public void Missing_executable_needs_proof_and_an_enabled_entry(bool exists, bool enabled) =>
        Assert.Empty(Run(new StartupMissingExecutableRule(), Obs(@"C:\App\app.exe", exists, enabled)));

    [Fact]
    public void Unknown_existence_concludes_nothing() =>
        Assert.Empty(Run(new StartupMissingExecutableRule(), Obs(@"C:\App\app.exe", exists: null, enabled: true)));

    [Fact]
    public void Temp_location_is_a_possible_problem_not_a_confirmed_one()
    {
        var temp = Path.Combine(Path.GetTempPath(), "upd", "run.exe");
        var finding = Assert.Single(Run(new StartupTempLocationRule(), Obs(temp, exists: true, enabled: true)));

        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.False(finding.Confidence.IsConfirmed);
        Assert.Empty(Run(new StartupTempLocationRule(), Obs(temp, exists: true, enabled: false)));
        Assert.Empty(Run(new StartupTempLocationRule(), Obs(@"C:\Program Files\App\app.exe", exists: true, enabled: true)));
    }
}
