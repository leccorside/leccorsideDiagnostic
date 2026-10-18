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

        var commandOptions = new CommandExecutionOptions();
        foreach (var executable in configuration.GetSection(CommandExecutionOptions.SectionName).GetSection("AdditionalTrustedExecutables").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(executable.Value))
            {
                commandOptions.AdditionalTrustedExecutables.Add(executable.Value);
            }
        }

        services.AddSingleton(new ExecutablePolicy(commandOptions.AdditionalTrustedExecutables));
        services.AddSingleton<ICommandExecutor, CommandExecutor>();

        services.AddSingleton(new SqliteDatabase(paths.Database));
        services.AddSingleton<MigrationRunner>();
        services.AddSingleton<IScanRepository, SqliteScanRepository>();
        services.AddSingleton<IRepairHistoryRepository, SqliteRepairHistoryRepository>();
        services.AddSingleton<IBackupStore>(sp => new FileBackupStore(paths.Backups, sp.GetRequiredService<ILogger<FileBackupStore>>()));
        return services;
    }
}
