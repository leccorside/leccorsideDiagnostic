using LeccorWindowsHealthCenter.Application.Commands;
using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Infrastructure.Backup;
using LeccorWindowsHealthCenter.Infrastructure.Commands;
using LeccorWindowsHealthCenter.Infrastructure.Persistence;
using LeccorWindowsHealthCenter.Infrastructure.Security;
using LeccorWindowsHealthCenter.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHealthCenterInfrastructure(this IServiceCollection services, IConfiguration configuration, AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        services.AddSingleton(paths);
        services.AddSingleton<IPrivilegeService, PrivilegeService>();
        services.AddSingleton<IHardwareInventoryProvider, SystemInfo.WmiHardwareInventoryProvider>();
        services.AddSingleton<IStorageInventoryProvider, SystemInfo.WmiStorageInventoryProvider>();
        services.AddSingleton<Application.Storage.ISpaceAnalyzer, Storage.FileSystemSpaceAnalyzer>();
        services.AddSingleton<IShellLauncher, SystemInfo.ShellLauncher>();
        services.AddSingleton<IProcessIoTracer, Processes.EtwProcessIoTracer>();
        services.AddSingleton<IProcessInventoryProvider, SystemInfo.ProcessInventoryProvider>();
        services.AddSingleton<IFileIdentityProvider, SystemInfo.FileIdentityProvider>();
        services.AddSingleton<Application.Startup.IBootPerformanceProvider, Startup.BootPerformanceProvider>();
        services.AddSingleton<Application.Startup.IStartupInventoryProvider, Startup.StartupInventoryProvider>();

        var commandOptions = new CommandExecutionOptions();
        foreach (var executable in configuration.GetSection(CommandExecutionOptions.SectionName).GetSection("AdditionalTrustedExecutables").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(executable.Value))
            {
                commandOptions.AdditionalTrustedExecutables.Add(executable.Value);
            }
        }

        services.AddSingleton(new ExecutablePolicy(commandOptions.AdditionalTrustedExecutables));
        services.AddSingleton<Application.Security.IElevatedClient, ElevatedClient>();
        services.AddSingleton<ICommandExecutor, CommandExecutor>();
        services.AddSingleton<Application.Events.IEventViewerProvider, Events.EventViewerProvider>();
        services.AddSingleton<Application.WindowsHealth.IWindowsIntegrityProvider, WindowsHealth.WindowsIntegrityProvider>();
        services.AddSingleton<Application.Explorer.IExplorerProvider, Explorer.ExplorerProvider>();
        services.AddSingleton<Application.Explorer.IFolderBenchmarkService, Explorer.FolderBenchmarkService>();
        services.AddSingleton<Application.Services.IServicesProvider, Services.WindowsServicesProvider>();
        services.AddSingleton<Application.Drivers.IDriversProvider, Drivers.PnpDeviceDriverProvider>();
        services.AddSingleton<Application.Apps.IAppsProvider, Apps.InstalledAppsProvider>();
        services.AddSingleton<Application.Network.INetworkProvider, Network.NetworkProvider>();
        services.AddSingleton<Application.Network.INetworkDiagnosticsService, Network.NetworkDiagnosticsService>();
        services.AddSingleton<Application.WindowsUpdate.IWindowsUpdateProvider, WindowsUpdate.WindowsUpdateProvider>();
        services.AddSingleton<Application.Storage.ISafeCleanProvider, Storage.SafeCleanProvider>();
        services.AddSingleton<Application.Repair.IRestorePointService, Repair.RestorePointService>();
        services.AddSingleton<Application.DevTools.IDevToolsProvider, DevTools.DevToolsProvider>();
        services.AddSingleton<Application.Security.ISecurityProvider, Security.SecurityProvider>();
        services.AddSingleton<Application.Crash.ICrashAnalyzerProvider, Crash.CrashAnalyzerProvider>();
        services.AddSingleton<Application.Scheduling.IScanSchedulerService, Scheduling.WindowsTaskSchedulerService>();
        services.AddSingleton<Application.Notifications.INotificationService, Notifications.WindowsNotificationService>();

        services.AddSingleton(new SqliteDatabase(paths.Database));
        services.AddSingleton<MigrationRunner>();
        services.AddSingleton<IScanRepository, SqliteScanRepository>();
        services.AddSingleton<IRepairHistoryRepository, SqliteRepairHistoryRepository>();
        services.AddSingleton<Application.Persistence.IMetricSampleRepository, Persistence.SqliteMetricSampleRepository>();
        services.AddSingleton<Application.Settings.IConfigurationRepository, Persistence.SqliteConfigurationRepository>();
        services.AddSingleton<IBackupStore>(sp => new FileBackupStore(paths.Backups, sp.GetRequiredService<ILogger<FileBackupStore>>()));
        return services;
    }
}
