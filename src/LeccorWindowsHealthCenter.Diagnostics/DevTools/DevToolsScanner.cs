using System.Globalization;
using LeccorWindowsHealthCenter.Application.DevTools;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Diagnostics.DevTools;

public sealed class DevToolsScanner : DiagnosticScannerBase
{
    public const string ScannerId = "devtools";

    private readonly IDevToolsProvider _devToolsProvider;

    public DevToolsScanner(IDevToolsProvider devToolsProvider, ILogger<DevToolsScanner> logger)
        : base(logger)
    {
        _devToolsProvider = devToolsProvider;
    }

    public override string Id => ScannerId;

    public override string Name => "Desenvolvimento (Docker, WSL e Ferramentas)";

    public override ScanModule Module => ScanModule.Applications;

    public override bool IncludedInQuickScan => false;

    public override int Order => 115;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var env = await _devToolsProvider.GetDevEnvironmentAsync(cancellationToken);

        // Métricas
        collector.AddMetric("devtools.installed_tools_count", env.Tools.Count(t => t.IsInstalled), "count");
        collector.AddMetric("devtools.wsl_distros_count", env.WslDistributions.Count, "count");
        collector.AddMetric("devtools.path_invalid_count", env.PathAnalysis.InvalidEntries.Count, "count");

        // Observações de Ferramentas
        foreach (var tool in env.Tools)
        {
            var attrs = new Dictionary<string, string>
            {
                [DevToolsNames.AttrToolName] = tool.Name,
                [DevToolsNames.AttrCategory] = tool.Category,
                [DevToolsNames.AttrInstalled] = tool.IsInstalled ? "true" : "false",
                [DevToolsNames.AttrVersion] = tool.Version ?? string.Empty
            };
            collector.AddObservation(DevToolsNames.ObservationDevTool, tool.Name, attrs);
        }

        // Observações de WSL
        foreach (var distro in env.WslDistributions)
        {
            var attrs = new Dictionary<string, string>
            {
                [DevToolsNames.AttrWslName] = distro.Name,
                [DevToolsNames.AttrWslVersion] = distro.WslVersion.ToString(CultureInfo.InvariantCulture),
                [DevToolsNames.AttrWslVhdxSize] = distro.VhdxSizeBytes?.ToString(CultureInfo.InvariantCulture) ?? "0"
            };
            collector.AddObservation(DevToolsNames.ObservationWslDistro, distro.Name, attrs);
        }

        // Observação do Docker
        var dockerAttrs = new Dictionary<string, string>
        {
            [DevToolsNames.AttrDockerRunning] = env.Docker.IsRunning ? "true" : "false",
            [DevToolsNames.AttrDockerTotalUsage] = env.Docker.TotalDiskUsageBytes?.ToString(CultureInfo.InvariantCulture) ?? "0"
        };
        collector.AddObservation(DevToolsNames.ObservationDocker, "DockerDesktop", dockerAttrs);

        // Observação do PATH
        var pathAttrs = new Dictionary<string, string>
        {
            [DevToolsNames.AttrPathInvalidCount] = env.PathAnalysis.InvalidEntries.Count.ToString(CultureInfo.InvariantCulture)
        };
        collector.AddObservation(DevToolsNames.ObservationPath, "EnvironmentPath", pathAttrs);
    }
}

