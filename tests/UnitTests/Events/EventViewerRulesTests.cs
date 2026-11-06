using System.Globalization;
using LeccorWindowsHealthCenter.Application.Events;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Events;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.Events;

public class EventViewerRulesTests
{
    private static List<Finding> Run(IRule rule, IEnumerable<Observation> observations)
    {
        var engine = new RuleEngine([rule], new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance);
        var result = new DiagnosticResult
        {
            ScannerId = EventViewerScanner.ScannerId,
            ScannerName = EventViewerScanner.ScannerId,
            Module = ScanModule.Events,
            Status = ScannerRunStatus.Completed,
            Metrics = [],
            Observations = observations.ToList(),
        };
        return engine.Evaluate([result]).SelectMany(r => r.Findings).ToList();
    }

    private static Observation CreateEventObservation(
        string log,
        string provider,
        int eventId,
        string level,
        int count24h,
        string message,
        string? targetResource = null) => new()
    {
        Kind = EventViewerNames.ObservationKind,
        Subject = $"{provider}/{eventId}",
        ScannerId = EventViewerScanner.ScannerId,
        Attributes = new Dictionary<string, string>
        {
            [EventViewerNames.AttrLogName] = log,
            [EventViewerNames.AttrProviderName] = provider,
            [EventViewerNames.AttrEventId] = eventId.ToString(CultureInfo.InvariantCulture),
            [EventViewerNames.AttrLevel] = level,
            [EventViewerNames.AttrCount24h] = count24h.ToString(CultureInfo.InvariantCulture),
            [EventViewerNames.AttrFirstSeen] = DateTimeOffset.UtcNow.AddHours(-12).ToString("O"),
            [EventViewerNames.AttrLastSeen] = DateTimeOffset.UtcNow.ToString("O"),
            [EventViewerNames.AttrMessage] = message,
            [EventViewerNames.AttrTargetResource] = targetResource ?? string.Empty,
        },
    };

    [Fact]
    public void EventViewerDiskIoErrorsRule_TriggersCritical_WhenBadBlockOrNtfsCorruptionDetected()
    {
        var rule = new EventViewerDiskIoErrorsRule();
        var obs = CreateEventObservation("System", "disk", 7, "Error", 2, "O dispositivo \\Device\\Harddisk0\\DR0 possui um bloco defeituoso.", "\\Device\\Harddisk0\\DR0");

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Equal(FindingCategory.Events, finding.Category);
    }

    [Fact]
    public void EventViewerDiskIoErrorsRule_TriggersHigh_WhenDiskTimeoutOccurs()
    {
        var rule = new EventViewerDiskIoErrorsRule();
        var obs = CreateEventObservation("System", "disk", 153, "Warning", 3, "A operação de E/S no endereço de bloco lógico foi repetida.", "\\Device\\Harddisk1\\DR1");

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
    }

    [Fact]
    public void EventViewerDiskIoErrorsRule_DoesNotTrigger_WhenNoDiskErrors()
    {
        var rule = new EventViewerDiskIoErrorsRule();
        var obs = CreateEventObservation("System", "Service Control Manager", 7036, "Information", 10, "O serviço foi iniciado com êxito.");

        var findings = Run(rule, [obs]);
        Assert.Empty(findings);
    }

    [Fact]
    public void EventViewerGpuDriverCrashRule_Triggers_WhenTdrCrashDetected()
    {
        var rule = new EventViewerGpuDriverCrashRule();
        var obs = CreateEventObservation("System", "Display", 4101, "Warning", 4, "O driver de vídeo nvlddmkm parou de responder e se recuperou com êxito.", "nvlddmkm");

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
        Assert.Equal(FindingCategory.Events, finding.Category);
    }

    [Fact]
    public void EventViewerGpuDriverCrashRule_DoesNotTrigger_WhenNoGpuCrash()
    {
        var rule = new EventViewerGpuDriverCrashRule();
        var obs = CreateEventObservation("System", "Kernel-General", 1, "Information", 1, "A hora do sistema foi alterada.");

        var findings = Run(rule, [obs]);
        Assert.Empty(findings);
    }

    [Fact]
    public void EventViewerHardwareWheaRule_TriggersCritical_WhenWheaHardwareErrorLogged()
    {
        var rule = new EventViewerHardwareWheaRule();
        var obs = CreateEventObservation("System", "Microsoft-Windows-WHEA-Logger", 18, "Error", 1, "Ocorreu um erro fatal de hardware. Componente: Núcleo do processador.", "Núcleo do processador");

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Equal(FindingCategory.Events, finding.Category);
    }

    [Fact]
    public void EventViewerHardwareWheaRule_DoesNotTrigger_WhenNoWheaEvents()
    {
        var rule = new EventViewerHardwareWheaRule();
        var obs = CreateEventObservation("System", "Power", 109, "Information", 2, "Transição de estado do kernel.");

        var findings = Run(rule, [obs]);
        Assert.Empty(findings);
    }

    [Fact]
    public void EventViewerUnexpectedShutdownRule_TriggersHigh_WhenKernelPower41OrEventLog6008Present()
    {
        var rule = new EventViewerUnexpectedShutdownRule();
        var obs = CreateEventObservation("System", "Microsoft-Windows-Kernel-Power", 41, "Critical", 2, "O sistema foi reiniciado sem um desligamento prévio.");

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
        Assert.Equal(FindingCategory.Events, finding.Category);
    }

    [Fact]
    public void EventViewerUnexpectedShutdownRule_DoesNotTrigger_WhenNormalReboot()
    {
        var rule = new EventViewerUnexpectedShutdownRule();
        var obs = CreateEventObservation("System", "USER32", 1074, "Information", 1, "O processo shutdown.exe iniciou a reinicialização.");

        var findings = Run(rule, [obs]);
        Assert.Empty(findings);
    }

    [Fact]
    public void EventViewerAppCrashRule_TriggersMedium_WhenFrequentApplicationCrashesDetected()
    {
        var rule = new EventViewerAppCrashRule();
        var obs = CreateEventObservation("Application", "Application Error", 1000, "Error", 5, "Nome do aplicativo com falha: Photoshop.exe", "Photoshop.exe");

        var findings = Run(rule, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Equal(FindingCategory.Applications, finding.Category);
    }

    [Fact]
    public void EventViewerAppCrashRule_DoesNotTrigger_WhenOccurrencesBelowThreshold()
    {
        var rule = new EventViewerAppCrashRule();
        var obs = CreateEventObservation("Application", "Application Error", 1000, "Error", 1, "Nome do aplicativo com falha: notepad.exe", "notepad.exe");

        var findings = Run(rule, [obs]);
        Assert.Empty(findings);
    }

    [Fact]
    public void EventViewerDiskIoErrorsRule_EnrichesTitleWithDriveLetters_WhenVolumesPresent()
    {
        var rule = new EventViewerDiskIoErrorsRule();
        var eventObs = CreateEventObservation(
            "System",
            "disk",
            153,
            "Warning",
            801,
            "A operação de E/S no endereço de bloco lógico para o Disco 3 foi repetida.",
            "Disco 3");

        var volumeObs = new Observation
        {
            ScannerId = LeccorWindowsHealthCenter.Diagnostics.Storage.DiskScanner.ScannerId,
            Kind = LeccorWindowsHealthCenter.Diagnostics.Storage.StorageNames.ObservationVolume,
            Subject = "F:\\",
            Attributes = new Dictionary<string, string>
            {
                ["root"] = "F:\\",
                ["disk"] = "3",
                ["label"] = "Backup Externo",
            },
        };

        var findings = Run(rule, [eventObs, volumeObs]);

        var finding = Assert.Single(findings);
        Assert.Equal("Foram detectados eventos recorrentes de I/O (Disco 3 [F:])", finding.Title);
        Assert.Contains("Disco 3 [F:]", finding.Description);
        Assert.Equal(Severity.Critical, finding.Severity);
    }
}
