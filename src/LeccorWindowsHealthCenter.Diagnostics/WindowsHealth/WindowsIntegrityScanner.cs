using System.Globalization;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.WindowsHealth;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Diagnostics.WindowsHealth;

/// <summary>
/// Scanner de Integridade do Windows (seção 22 do PROMPT):
/// DISM (/CheckHealth ou /ScanHealth), SFC (/VERIFYONLY) e CHKDSK (somente leitura).
/// </summary>
public sealed class WindowsIntegrityScanner : DiagnosticScannerBase
{
    public const string ScannerId = "windows.integrity";

    private readonly IWindowsIntegrityProvider _provider;

    public WindowsIntegrityScanner(IWindowsIntegrityProvider provider, ILogger<WindowsIntegrityScanner> logger)
        : base(logger)
    {
        _provider = provider;
    }

    public override string Id => "windows.integrity";

    public override string Name => "Integridade do Windows";

    public override ScanModule Module => ScanModule.Windows;

    public override bool IncludedInQuickScan => true;

    public override int Order => 65;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        // DISM /CheckHealth é rápido (< 1s) e verifica o estado do Component Store
        var componentStore = await _provider.CheckComponentStoreAsync(deepScan: false, cancellationToken).ConfigureAwait(false);

        collector.AddMetric("windows.component_store.status", (int)componentStore.Status, "status");
        collector.AddObservation(
            WindowsIntegrityNames.ObservationComponentStore,
            $"Component Store: {componentStore.Message}",
            new Dictionary<string, string>
            {
                [WindowsIntegrityNames.AttrStatus] = componentStore.Status.ToString(),
                [WindowsIntegrityNames.AttrMessage] = componentStore.Message,
                [WindowsIntegrityNames.AttrExitCode] = componentStore.CommandResult?.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "-1",
                [WindowsIntegrityNames.AttrCommandLine] = componentStore.CommandResult?.CommandLine ?? "dism.exe /Online /Cleanup-Image /CheckHealth",
                [WindowsIntegrityNames.AttrDurationMs] = ((long)(componentStore.CommandResult?.Duration.TotalMilliseconds ?? 0)).ToString(CultureInfo.InvariantCulture),
                [WindowsIntegrityNames.AttrOutput] = componentStore.CommandResult?.StandardOutput ?? string.Empty,
            },
            [
                new Evidence
                {
                    Source = EvidenceSource.Command,
                    Description = componentStore.Message,
                    Value = (int)componentStore.Status,
                    Unit = "status",
                    Reference = componentStore.CommandResult?.CommandLine ?? "DISM",
                    SampleCount = 1,
                    CollectedAt = DateTimeOffset.UtcNow,
                },
            ]);

        // Verificação de arquivos do sistema (SFC) e sistema de arquivos (CHKDSK)
        var systemFiles = await _provider.CheckSystemFilesAsync(cancellationToken).ConfigureAwait(false);
        collector.AddMetric("windows.system_files.status", (int)systemFiles.Status, "status");
        collector.AddObservation(
            WindowsIntegrityNames.ObservationSystemFiles,
            $"System Files: {systemFiles.Message}",
            new Dictionary<string, string>
            {
                [WindowsIntegrityNames.AttrStatus] = systemFiles.Status.ToString(),
                [WindowsIntegrityNames.AttrMessage] = systemFiles.Message,
                [WindowsIntegrityNames.AttrExitCode] = systemFiles.CommandResult?.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "-1",
                [WindowsIntegrityNames.AttrCommandLine] = systemFiles.CommandResult?.CommandLine ?? "sfc.exe /VERIFYONLY",
                [WindowsIntegrityNames.AttrDurationMs] = ((long)(systemFiles.CommandResult?.Duration.TotalMilliseconds ?? 0)).ToString(CultureInfo.InvariantCulture),
                [WindowsIntegrityNames.AttrOutput] = systemFiles.CommandResult?.StandardOutput ?? string.Empty,
            },
            [
                new Evidence
                {
                    Source = EvidenceSource.Command,
                    Description = systemFiles.Message,
                    Value = (int)systemFiles.Status,
                    Unit = "status",
                    Reference = systemFiles.CommandResult?.CommandLine ?? "SFC",
                    SampleCount = 1,
                    CollectedAt = DateTimeOffset.UtcNow,
                },
            ]);

        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:";
        var fileSystem = await _provider.CheckFileSystemAsync(systemDrive, cancellationToken).ConfigureAwait(false);
        collector.AddMetric("windows.filesystem.status", (int)fileSystem.Status, "status", new Dictionary<string, string> { ["drive"] = systemDrive });
        collector.AddObservation(
            WindowsIntegrityNames.ObservationFileSystem,
            $"Sistema de arquivos ({systemDrive}): {fileSystem.Message}",
            new Dictionary<string, string>
            {
                [WindowsIntegrityNames.AttrDrive] = systemDrive,
                [WindowsIntegrityNames.AttrStatus] = fileSystem.Status.ToString(),
                [WindowsIntegrityNames.AttrMessage] = fileSystem.Message,
                [WindowsIntegrityNames.AttrExitCode] = fileSystem.CommandResult?.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "-1",
                [WindowsIntegrityNames.AttrCommandLine] = fileSystem.CommandResult?.CommandLine ?? $"chkdsk.exe {systemDrive}",
                [WindowsIntegrityNames.AttrDurationMs] = ((long)(fileSystem.CommandResult?.Duration.TotalMilliseconds ?? 0)).ToString(CultureInfo.InvariantCulture),
                [WindowsIntegrityNames.AttrOutput] = fileSystem.CommandResult?.StandardOutput ?? string.Empty,
            },
            [
                new Evidence
                {
                    Source = EvidenceSource.Command,
                    Description = fileSystem.Message,
                    Value = (int)fileSystem.Status,
                    Unit = "status",
                    Reference = fileSystem.CommandResult?.CommandLine ?? "CHKDSK",
                    SampleCount = 1,
                    CollectedAt = DateTimeOffset.UtcNow,
                },
            ]);
    }
}
