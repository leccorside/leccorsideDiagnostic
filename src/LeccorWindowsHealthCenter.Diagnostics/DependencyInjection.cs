using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Diagnostics.Hardware;
using LeccorWindowsHealthCenter.Diagnostics.Performance;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeccorWindowsHealthCenter.Diagnostics;

public static class DependencyInjection
{
    /// <summary>
    /// Ponto único de registro de scanners (<see cref="IDiagnosticScanner"/>) e regras (<see cref="IRule"/>).
    /// Catálogo e documentação: docs/DIAGNOSTICS.md e docs/RULE_ENGINE.md.
    /// </summary>
    public static IServiceCollection AddHealthCenterDiagnostics(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SamplingOptions>(configuration.GetSection(SamplingOptions.SectionName));

        // Passo 2 — MVP 1 (a): Hardware, CPU, Memória e GPU
        services.AddSingleton<IDiagnosticScanner, HardwareScanner>();
        services.AddSingleton<IDiagnosticScanner, CpuScanner>();
        services.AddSingleton<IDiagnosticScanner, MemoryScanner>();
        services.AddSingleton<IDiagnosticScanner, GpuScanner>();

        services.AddSingleton<IRule, CpuSustainedHighRule>();
        services.AddSingleton<IRule, CpuThrottlingRule>();
        services.AddSingleton<IRule, MemoryPressureRule>();
        services.AddSingleton<IRule, CommitChargeHighRule>();
        services.AddSingleton<IRule, PossibleMemoryLeakRule>();

        // Passo 3 — MVP 1 (b): Discos e Armazenamento
        services.AddSingleton<IDiagnosticScanner, DiskScanner>();
        services.AddSingleton<IDiagnosticScanner, StorageScanner>();

        services.AddSingleton<IRule, LowFreeSpaceRule>();
        services.AddSingleton<IRule, DiskHealthRule>();
        services.AddSingleton<IRule, DiskTemperatureRule>();
        services.AddSingleton<IRule, TrimDisabledRule>();
        services.AddSingleton<IRule, TempCacheLargeRule>();

        // Passo 4 — MVP 1 (c): Processos e Inicialização
        services.AddSingleton<IDiagnosticScanner, Startup.StartupScanner>();
        services.AddSingleton<IRule, StartupMissingExecutableRule>();
        services.AddSingleton<IRule, StartupTempLocationRule>();
        services.AddSingleton<IRule, StartupScriptHostRule>();

        // Passo 5 — MVP 1 (d): Event Viewer e Integridade do Windows
        services.AddSingleton<IDiagnosticScanner, Events.EventViewerScanner>();
        services.AddSingleton<IDiagnosticScanner, WindowsHealth.WindowsIntegrityScanner>();

        services.AddSingleton<IRule, EventViewerDiskIoErrorsRule>();
        services.AddSingleton<IRule, EventViewerGpuDriverCrashRule>();
        services.AddSingleton<IRule, EventViewerHardwareWheaRule>();
        services.AddSingleton<IRule, EventViewerUnexpectedShutdownRule>();
        services.AddSingleton<IRule, EventViewerAppCrashRule>();
        services.AddSingleton<IRule, WindowsComponentStoreRule>();
        services.AddSingleton<IRule, WindowsSystemFilesRule>();
        services.AddSingleton<IRule, WindowsFileSystemRule>();

        // Passo 6 — MVP 2 (a): Explorer, Serviços e Drivers
        services.AddSingleton<IDiagnosticScanner, Explorer.ExplorerScanner>();
        services.AddSingleton<IDiagnosticScanner, Services.ServicesScanner>();
        services.AddSingleton<IDiagnosticScanner, Drivers.DriversScanner>();

        services.AddSingleton<IRule, ExplorerMissingShellExtensionRule>();
        services.AddSingleton<IRule, ExplorerUnsignedShellExtensionRule>();
        services.AddSingleton<IRule, ServicesMissingExecutableRule>();
        services.AddSingleton<IRule, ServicesFailedStartupRule>();
        services.AddSingleton<IRule, DriversDeviceErrorRule>();
        services.AddSingleton<IRule, DriversMissingDriverRule>();

        // Passo 7 — MVP 2 (b): Aplicativos, Rede e Windows Update
        services.AddSingleton<IDiagnosticScanner, Apps.AppsScanner>();
        services.AddSingleton<IDiagnosticScanner, Network.NetworkScanner>();
        services.AddSingleton<IDiagnosticScanner, WindowsUpdate.WindowsUpdateScanner>();

        services.AddSingleton<IRule, AppsOrphanedUninstallRule>();
        services.AddSingleton<IRule, AppsDuplicateInstallRule>();
        services.AddSingleton<IRule, NetworkNoGatewayRule>();
        services.AddSingleton<IRule, NetworkDnsSlowRule>();
        services.AddSingleton<IRule, WindowsUpdateServiceDisabledRule>();
        services.AddSingleton<IRule, WindowsUpdateRebootPendingRule>();

        // Passo 9 — MVP 4: Docker, WSL, Ferramentas de desenvolvimento, Crash Analyzer e Segurança
        services.AddSingleton<IDiagnosticScanner, DevTools.DevToolsScanner>();
        services.AddSingleton<IDiagnosticScanner, Security.SecurityScanner>();
        services.AddSingleton<IDiagnosticScanner, Crash.CrashScanner>();

        services.AddSingleton<IRule, DevWslVhdxLargeRule>();
        services.AddSingleton<IRule, DevPathInvalidEntriesRule>();
        services.AddSingleton<IRule, SecurityAntivirusDisabledRule>();
        services.AddSingleton<IRule, SecurityUacDisabledRule>();
        services.AddSingleton<IRule, SecurityIfeoHijackRule>();
        services.AddSingleton<IRule, CrashKernelMinidumpRule>();
        return services;
    }
}
