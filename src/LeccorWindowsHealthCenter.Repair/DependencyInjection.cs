using LeccorWindowsHealthCenter.Application.Repair;
using Microsoft.Extensions.DependencyInjection;

namespace LeccorWindowsHealthCenter.Repair;

public static class DependencyInjection
{
    /// <summary>
    /// Registra o RepairEngine. Ações concretas (<see cref="IRepairAction"/>) entram a partir do MVP 3,
    /// cada uma registrada aqui com <c>services.AddSingleton&lt;IRepairAction, ...&gt;()</c>.
    /// </summary>
    public static IServiceCollection AddHealthCenterRepair(this IServiceCollection services)
    {
        services.AddSingleton<IRepairEngine, RepairEngine>();
        return services;
    }
}
