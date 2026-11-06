using LeccorWindowsHealthCenter.Application.Commands;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.WindowsHealth;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Diagnostics.WindowsHealth;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Infrastructure.WindowsHealth;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.WindowsHealth;

public class WindowsIntegrityRulesTests
{
    private static List<Finding> Run(IRule rule, string scannerId, IEnumerable<Observation> observations, IEnumerable<Metric>? metrics = null)
    {
        var engine = new RuleEngine([rule], new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance);
        var result = new DiagnosticResult
        {
            ScannerId = scannerId,
            ScannerName = scannerId,
            Module = ScanModule.Windows,
            Status = ScannerRunStatus.Completed,
            Metrics = metrics?.ToList() ?? [],
            Observations = observations.ToList(),
        };
        return engine.Evaluate([result]).SelectMany(r => r.Findings).ToList();
    }

    private static CommandResult MakeCommandResult(int exitCode, string stdout, string stderr = "") => new()
    {
        CommandLine = "test",
        ExitCode = exitCode,
        StandardOutput = stdout,
        StandardError = stderr,
        Duration = TimeSpan.FromSeconds(1),
    };

    [Fact]
    public void ParseDismOutput_ReturnsHealthy_ForPortugueseSuccess()
    {
        var result = MakeCommandResult(0, "Nenhuma corrupção no repositório de componentes foi detectada. A operação foi concluída com êxito.");
        var (status, _) = WindowsIntegrityProvider.ParseDismOutput(result);
        Assert.Equal(ComponentStoreStatus.Healthy, status);
    }

    [Fact]
    public void ParseDismOutput_ReturnsHealthy_ForEnglishSuccess()
    {
        var result = MakeCommandResult(0, "No component store corruption detected. The operation completed successfully.");
        var (status, _) = WindowsIntegrityProvider.ParseDismOutput(result);
        Assert.Equal(ComponentStoreStatus.Healthy, status);
    }

    [Fact]
    public void ParseDismOutput_ReturnsRepairable_WhenRepairable()
    {
        var result = MakeCommandResult(0, "O repositório de componentes é reparável. A operação foi concluída com êxito.");
        var (status, _) = WindowsIntegrityProvider.ParseDismOutput(result);
        Assert.Equal(ComponentStoreStatus.Repairable, status);
    }

    [Fact]
    public void ParseDismOutput_ReturnsCorrupted_WhenCorruptedOrExitCode14098()
    {
        var result = MakeCommandResult(14098, "O repositório de componentes está corrompido.", "Erro: 14098");
        var (status, _) = WindowsIntegrityProvider.ParseDismOutput(result);
        Assert.Equal(ComponentStoreStatus.Corrupted, status);
    }

    [Fact]
    public void ParseSfcOutput_ReturnsIntegrityOk_ForPortugueseSuccess()
    {
        var result = MakeCommandResult(0, "A Proteção de Recursos do Windows não encontrou nenhuma violação de integridade.");
        var (status, _) = WindowsIntegrityProvider.ParseSfcOutput(result);
        Assert.Equal(SystemFilesStatus.IntegrityOk, status);
    }

    [Fact]
    public void ParseSfcOutput_ReturnsIntegrityOk_ForEnglishSuccess()
    {
        var result = MakeCommandResult(0, "Windows Resource Protection did not find any integrity violations.");
        var (status, _) = WindowsIntegrityProvider.ParseSfcOutput(result);
        Assert.Equal(SystemFilesStatus.IntegrityOk, status);
    }

    [Fact]
    public void ParseSfcOutput_ReturnsCorruptedCannotRepair_WhenViolationsFound()
    {
        var result = MakeCommandResult(0, "A Proteção de Recursos do Windows encontrou arquivos corrompidos, mas não pôde reparar alguns deles.");
        var (status, _) = WindowsIntegrityProvider.ParseSfcOutput(result);
        Assert.Equal(SystemFilesStatus.CorruptedCannotRepair, status);
    }

    [Fact]
    public void ParseChkdskOutput_ReturnsVolumeOk_ForCleanVolume()
    {
        var result = MakeCommandResult(0, "O Windows examinou o sistema de arquivos e não encontrou problemas. Nenhuma ação adicional é necessária.");
        var (status, _) = WindowsIntegrityProvider.ParseChkdskOutput(result, "C:");
        Assert.Equal(FileSystemStatus.VolumeOk, status);
    }

    [Fact]
    public void ParseChkdskOutput_ReturnsErrorsFound_WhenErrorsDetected()
    {
        var result = MakeCommandResult(1, "O Windows examinou o sistema de arquivos e encontrou erros.");
        var (status, _) = WindowsIntegrityProvider.ParseChkdskOutput(result, "C:");
        Assert.Equal(FileSystemStatus.ErrorsFound, status);
    }

    [Fact]
    public void WindowsComponentStoreRule_TriggersFinding_WhenComponentStoreCorruptedOrRepairable()
    {
        var rule = new WindowsComponentStoreRule();
        var obs = new Observation
        {
            Kind = WindowsIntegrityNames.ObservationComponentStore,
            Subject = "ComponentStore",
            ScannerId = WindowsIntegrityScanner.ScannerId,
            Attributes = new Dictionary<string, string>
            {
                [WindowsIntegrityNames.AttrStatus] = ComponentStoreStatus.Repairable.ToString(),
                [WindowsIntegrityNames.AttrMessage] = "O repositório de componentes é reparável.",
            },
        };

        var findings = Run(rule, WindowsIntegrityScanner.ScannerId, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
    }

    [Fact]
    public void WindowsComponentStoreRule_DoesNotTrigger_WhenHealthy()
    {
        var rule = new WindowsComponentStoreRule();
        var obs = new Observation
        {
            Kind = WindowsIntegrityNames.ObservationComponentStore,
            Subject = "ComponentStore",
            ScannerId = WindowsIntegrityScanner.ScannerId,
            Attributes = new Dictionary<string, string>
            {
                [WindowsIntegrityNames.AttrStatus] = ComponentStoreStatus.Healthy.ToString(),
                [WindowsIntegrityNames.AttrMessage] = "Repositório íntegro.",
            },
        };

        var findings = Run(rule, WindowsIntegrityScanner.ScannerId, [obs]);
        Assert.Empty(findings);
    }

    [Fact]
    public void WindowsSystemFilesRule_TriggersFinding_WhenViolationsFound()
    {
        var rule = new WindowsSystemFilesRule();
        var obs = new Observation
        {
            Kind = WindowsIntegrityNames.ObservationSystemFiles,
            Subject = "SystemFiles",
            ScannerId = WindowsIntegrityScanner.ScannerId,
            Attributes = new Dictionary<string, string>
            {
                [WindowsIntegrityNames.AttrStatus] = SystemFilesStatus.CorruptedCannotRepair.ToString(),
                [WindowsIntegrityNames.AttrMessage] = "Arquivos corrompidos detectados.",
            },
        };

        var findings = Run(rule, WindowsIntegrityScanner.ScannerId, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
    }

    [Fact]
    public void WindowsSystemFilesRule_DoesNotTrigger_WhenIntegrityOk()
    {
        var rule = new WindowsSystemFilesRule();
        var obs = new Observation
        {
            Kind = WindowsIntegrityNames.ObservationSystemFiles,
            Subject = "SystemFiles",
            ScannerId = WindowsIntegrityScanner.ScannerId,
            Attributes = new Dictionary<string, string>
            {
                [WindowsIntegrityNames.AttrStatus] = SystemFilesStatus.IntegrityOk.ToString(),
                [WindowsIntegrityNames.AttrMessage] = "Nenhuma violação.",
            },
        };

        var findings = Run(rule, WindowsIntegrityScanner.ScannerId, [obs]);
        Assert.Empty(findings);
    }

    [Fact]
    public void WindowsFileSystemRule_TriggersFinding_WhenErrorsFound()
    {
        var rule = new WindowsFileSystemRule();
        var obs = new Observation
        {
            Kind = WindowsIntegrityNames.ObservationFileSystem,
            Subject = "C:",
            ScannerId = WindowsIntegrityScanner.ScannerId,
            Attributes = new Dictionary<string, string>
            {
                [WindowsIntegrityNames.AttrDrive] = "C:",
                [WindowsIntegrityNames.AttrStatus] = FileSystemStatus.ErrorsFound.ToString(),
                [WindowsIntegrityNames.AttrMessage] = "Erros encontrados no volume C:.",
            },
        };

        var findings = Run(rule, WindowsIntegrityScanner.ScannerId, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.Critical, finding.Severity);
    }

    [Fact]
    public void WindowsFileSystemRule_DoesNotTrigger_WhenVolumeOk()
    {
        var rule = new WindowsFileSystemRule();
        var obs = new Observation
        {
            Kind = WindowsIntegrityNames.ObservationFileSystem,
            Subject = "C:",
            ScannerId = WindowsIntegrityScanner.ScannerId,
            Attributes = new Dictionary<string, string>
            {
                [WindowsIntegrityNames.AttrDrive] = "C:",
                [WindowsIntegrityNames.AttrStatus] = FileSystemStatus.VolumeOk.ToString(),
                [WindowsIntegrityNames.AttrMessage] = "Volume C: sem erros.",
            },
        };

        var findings = Run(rule, WindowsIntegrityScanner.ScannerId, [obs]);
        Assert.Empty(findings);
    }
}
