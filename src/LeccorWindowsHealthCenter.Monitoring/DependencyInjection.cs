using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.DependencyInjection;

namespace LeccorWindowsHealthCenter.Monitoring;

public static class DependencyInjection
{
    public static IServiceCollection AddHealthCenterMonitoring(this IServiceCollection services)
    {
        services.AddSingleton<ILiveMetricsProvider, LiveMetricsProvider>();
        return services;
    }
}
