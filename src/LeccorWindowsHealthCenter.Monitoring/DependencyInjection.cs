using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeccorWindowsHealthCenter.Monitoring;

public static class DependencyInjection
{
    public static IServiceCollection AddHealthCenterMonitoring(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MetricsHistoryOptions>(configuration.GetSection(MetricsHistoryOptions.SectionName));
        services.AddSingleton<ILiveMetricsProvider, LiveMetricsProvider>();
        services.AddSingleton<IPerformanceCounterSessionFactory, PdhPerformanceCounterSessionFactory>();
        services.AddSingleton<IProcessSnapshotProvider, ProcessSnapshotProvider>();

        // Mesma instância como serviço de fundo (amostragem) e como fonte de histórico para scanners e telas.
        services.AddSingleton<MetricsHistoryService>();
        services.AddSingleton<IMetricsHistory>(sp => sp.GetRequiredService<MetricsHistoryService>());
        services.AddHostedService(sp => sp.GetRequiredService<MetricsHistoryService>());
        return services;
    }
}
