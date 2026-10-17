using LeccorWindowsHealthCenter.Application.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace LeccorWindowsHealthCenter.Reporting;

public static class DependencyInjection
{
    public static IServiceCollection AddHealthCenterReporting(this IServiceCollection services)
    {
        services.AddSingleton<IReportExporter, JsonReportExporter>();
        return services;
    }
}
