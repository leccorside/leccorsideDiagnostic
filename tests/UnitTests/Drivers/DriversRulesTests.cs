using System.Globalization;
using LeccorWindowsHealthCenter.Application.Drivers;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Drivers;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.Drivers;

public class DriversRulesTests
{
    private static List<Finding> Run(IRule rule, IEnumerable<Observation> observations)
    {
        var engine = new RuleEngine([rule], new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance);
        var result = new DiagnosticResult
        {
            ScannerId = DriversScanner.ScannerId,
            ScannerName = DriversScanner.ScannerId,
            Module = ScanModule.Hardware,
            Status = ScannerRunStatus.Completed,
            Metrics = [],
            Observations = observations.ToList(),
        };
        return engine.Evaluate([result]).SelectMany(r => r.Findings).ToList();
    }

    private static Observation CreateDeviceObservation(
        string deviceId,
        string deviceName,
        uint errorCode,
        string statusMessage,
        string? manufacturer = null)
    {
        var isMissing = errorCode == 28;
        var isProblem = errorCode != 0;

        return new Observation
        {
            Kind = DriverNames.ObservationDevice,
            Subject = deviceName,
            ScannerId = DriversScanner.ScannerId,
            Attributes = new Dictionary<string, string>
            {
                [DriverNames.AttrDeviceId] = deviceId,
                [DriverNames.AttrName] = deviceName,
                [DriverNames.AttrManufacturer] = manufacturer ?? "Unknown",
                [DriverNames.AttrErrorCode] = errorCode.ToString(CultureInfo.InvariantCulture),
                [DriverNames.AttrStatusMessage] = statusMessage,
                [DriverNames.AttrIsProblem] = isProblem ? "true" : "false",
                [DriverNames.AttrIsMissing] = isMissing ? "true" : "false",
            },
        };
    }

    [Fact]
    public void DriversDeviceErrorRule_Triggers_WhenDeviceHasErrorCodeOtherThan28()
    {
        var rule = new DriversDeviceErrorRule();
        var obs = CreateDeviceObservation("PCI\\VEN_10DE&DEV_1C03", "NVIDIA GeForce GTX 1060", 43, "O Windows interrompeu este dispositivo porque encontrou problemas (Código 43).");

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
        Assert.Equal(FindingCategory.Drivers, finding.Category);
        Assert.Contains("NVIDIA GeForce GTX 1060", finding.Title);
    }

    [Fact]
    public void DriversDeviceErrorRule_DoesNotTrigger_WhenDeviceHasCodeZero()
    {
        var rule = new DriversDeviceErrorRule();
        var obs = CreateDeviceObservation("PCI\\VEN_8086&DEV_15D8", "Intel Ethernet Controller", 0, "Este dispositivo está funcionando corretamente.");

        var findings = Run(rule, [obs]);

        Assert.Empty(findings);
    }

    [Fact]
    public void DriversDeviceErrorRule_DoesNotTrigger_WhenDeviceIsCode28()
    {
        // Código 28 (driver ausente) é tratado pela regra DriversMissingDriverRule
        var rule = new DriversDeviceErrorRule();
        var obs = CreateDeviceObservation("PCI\\VEN_104C&DEV_8033", "Controlador de Comunicação PCI Simples", 28, "Os drivers para este dispositivo não estão instalados (Código 28).");

        var findings = Run(rule, [obs]);

        Assert.Empty(findings);
    }

    [Fact]
    public void DriversMissingDriverRule_Triggers_WhenDeviceHasMissingDriverCode28()
    {
        var rule = new DriversMissingDriverRule();
        var obs = CreateDeviceObservation("PCI\\VEN_104C&DEV_8033", "Controlador de Comunicação PCI Simples", 28, "Os drivers para este dispositivo não estão instalados (Código 28).");

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Equal(FindingCategory.Drivers, finding.Category);
        Assert.Contains("Controlador de Comunicação PCI Simples", finding.Title);
    }

    [Fact]
    public void DriversMissingDriverRule_DoesNotTrigger_WhenDeviceHasNoError()
    {
        var rule = new DriversMissingDriverRule();
        var obs = CreateDeviceObservation("PCI\\VEN_8086&DEV_15D8", "Intel Ethernet Controller", 0, "Este dispositivo está funcionando corretamente.");

        var findings = Run(rule, [obs]);

        Assert.Empty(findings);
    }

    [Fact]
    public void DriversMissingDriverRule_DoesNotTrigger_WhenDeviceHasOtherErrorCode()
    {
        var rule = new DriversMissingDriverRule();
        var obs = CreateDeviceObservation("PCI\\VEN_10DE&DEV_1C03", "NVIDIA GeForce GTX 1060", 43, "O Windows interrompeu este dispositivo porque encontrou problemas (Código 43).");

        var findings = Run(rule, [obs]);

        Assert.Empty(findings);
    }
}

