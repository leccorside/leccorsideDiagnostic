using System.Globalization;
using LeccorWindowsHealthCenter.Application.Events;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Services;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Services;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.Services;

public class ServicesRulesTests
{
    private static List<Finding> Run(IRule rule, string scannerId, ScanModule module, IEnumerable<Observation> observations)
    {
        var engine = new RuleEngine([rule], new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance);
        var result = new DiagnosticResult
        {
            ScannerId = scannerId,
            ScannerName = scannerId,
            Module = module,
            Status = ScannerRunStatus.Completed,
            Metrics = [],
            Observations = observations.ToList(),
        };
        return engine.Evaluate([result]).SelectMany(r => r.Findings).ToList();
    }

    private static Observation CreateServiceObservation(
        string serviceName,
        string displayName,
        string startupType,
        string state,
        string exePath,
        bool fileExists) => new()
    {
        Kind = WindowsServiceNames.ObservationService,
        Subject = serviceName,
        ScannerId = ServicesScanner.ScannerId,
        Attributes = new Dictionary<string, string>
        {
            [WindowsServiceNames.AttrName] = serviceName,
            [WindowsServiceNames.AttrDisplayName] = displayName,
            [WindowsServiceNames.AttrStartupType] = startupType,
            [WindowsServiceNames.AttrState] = state,
            [WindowsServiceNames.AttrExecutablePath] = exePath,
            [WindowsServiceNames.AttrExists] = fileExists ? "true" : "false",
        },
    };

    private static Observation CreateScmEventObservation(
        int eventId,
        string message,
        string targetService,
        int count24h = 1) => new()
    {
        Kind = EventViewerNames.ObservationKind,
        Subject = $"Service Control Manager/{eventId}",
        ScannerId = "eventviewer",
        Attributes = new Dictionary<string, string>
        {
            [EventViewerNames.AttrLogName] = "System",
            [EventViewerNames.AttrProviderName] = "Service Control Manager",
            [EventViewerNames.AttrEventId] = eventId.ToString(CultureInfo.InvariantCulture),
            [EventViewerNames.AttrLevel] = "Error",
            [EventViewerNames.AttrCount24h] = count24h.ToString(CultureInfo.InvariantCulture),
            [EventViewerNames.AttrMessage] = message,
            [EventViewerNames.AttrTargetResource] = targetService,
        },
    };

    [Fact]
    public void ServicesMissingExecutableRule_Triggers_WhenExecutableDoesNotExistAndStartupIsAutomatic()
    {
        var rule = new ServicesMissingExecutableRule();
        var obs = CreateServiceObservation("OrphanSvc", "Orphan Service", "Automatic", "Stopped", @"C:\OldApp\service.exe", fileExists: false);

        var findings = Run(rule, ServicesScanner.ScannerId, ScanModule.Services, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Equal(FindingCategory.Services, finding.Category);
        Assert.Contains("Orphan Service", finding.Title);
    }

    [Fact]
    public void ServicesMissingExecutableRule_DoesNotTrigger_WhenStartupIsManualOrDisabled()
    {
        var rule = new ServicesMissingExecutableRule();
        var obsDisabled = CreateServiceObservation("DisabledSvc", "Disabled Service", "Disabled", "Stopped", @"C:\OldApp\service.exe", fileExists: false);
        var obsManual = CreateServiceObservation("ManualSvc", "Manual Service", "Manual", "Stopped", @"C:\OldApp\service.exe", fileExists: false);

        var findings = Run(rule, ServicesScanner.ScannerId, ScanModule.Services, [obsDisabled, obsManual]);

        Assert.Empty(findings);
    }

    [Fact]
    public void ServicesMissingExecutableRule_DoesNotTrigger_WhenExecutableExists()
    {
        var rule = new ServicesMissingExecutableRule();
        var obs = CreateServiceObservation("GoodSvc", "Good Service", "Automatic", "Running", @"C:\Windows\system32\svchost.exe", fileExists: true);

        var findings = Run(rule, ServicesScanner.ScannerId, ScanModule.Services, [obs]);

        Assert.Empty(findings);
    }

    [Theory]
    [InlineData(7000, "O serviço não pôde ser iniciado.")]
    [InlineData(7009, "Tempo limite atingido.")]
    [InlineData(7024, "O serviço foi finalizado com erro.")]
    [InlineData(7031, "O serviço foi encerrado inesperadamente.")]
    [InlineData(7034, "O serviço foi finalizado inesperadamente.")]
    public void ServicesFailedStartupRule_Triggers_WhenServiceControlManagerReportsError(int eventId, string msg)
    {
        var rule = new ServicesFailedStartupRule();
        var obs = CreateScmEventObservation(eventId, msg, "CrashService");

        var findings = Run(rule, "eventviewer", ScanModule.Events, [obs]);

        var finding = Assert.Single(findings);
        Assert.Equal(rule.Id, finding.RuleId);
        Assert.Equal(Severity.High, finding.Severity);
        Assert.Equal(FindingCategory.Services, finding.Category);
        Assert.Contains("CrashService", finding.Title);
    }

    [Fact]
    public void ServicesFailedStartupRule_DoesNotTrigger_ForNormalServiceEvents()
    {
        var rule = new ServicesFailedStartupRule();
        var obsSuccess = CreateScmEventObservation(7036, "O serviço entrou em execução.", "GoodService");

        var findings = Run(rule, "eventviewer", ScanModule.Events, [obsSuccess]);

        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\App\\app.exe\" -arg1", "C:\\Program Files\\App\\app.exe")]
    [InlineData("\"C:\\Test\\tool.exe\"", "C:\\Test\\tool.exe")]
    [InlineData("C:\\Windows\\system32\\svchost.exe -k netsvcs", "C:\\Windows\\system32\\svchost.exe")]
    [InlineData("C:\\Windows\\System32\\services.exe", "C:\\Windows\\System32\\services.exe")]
    public void WindowsServicesProvider_ExtractExecutablePath_CorrectlyExtractsBinaryPaths(string rawPath, string expected)
    {
        var extracted = WindowsServicesProvider.ExtractExecutablePath(rawPath);
        Assert.Equal(expected, extracted);
    }
}
