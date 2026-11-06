using LeccorWindowsHealthCenter.Application.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace LeccorWindowsHealthCenter.Reporting;

public static class DependencyInjection
{
    public static IServiceCollection AddHealthCenterReporting(this IServiceCollection services)
    {
        services.AddSingleton<IReportExporter, JsonReportExporter>();
        services.AddSingleton<IReportExporter, HtmlReportExporter>();
        services.AddSingleton<IReportExporter, CsvReportExporter>();
        services.AddSingleton<IReportExporter, PdfReportExporter>();
        services.AddSingleton<Application.Storage.ISpaceAnalysisExporter, SpaceAnalysisExporter>();
        return services;
    }
}
