using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Repair.Actions;
using Microsoft.Extensions.DependencyInjection;

namespace LeccorWindowsHealthCenter.Repair;

public static class DependencyInjection
{
    /// <summary>
    /// Registra o RepairEngine e as ações de reparo reais (MVP 3 - Passo 8).
    /// </summary>
    public static IServiceCollection AddHealthCenterRepair(this IServiceCollection services)
    {
        services.AddSingleton<IRepairEngine, RepairEngine>();

        // Ações de reparo reais
        services.AddSingleton<IRepairAction, DisableStartupEntryRepairAction>();
        services.AddSingleton<IRepairAction, DisableShellExtensionRepairAction>();
        services.AddSingleton<IRepairAction, SafeCleanTempRepairAction>();
        services.AddSingleton<IRepairAction, EnableWindowsUpdateServiceRepairAction>();

        return services;
    }
}
