using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Application.Risk;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.Scoring;
using LeccorWindowsHealthCenter.Application.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeccorWindowsHealthCenter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddHealthCenterApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ScanOptions>(configuration.GetSection(ScanOptions.SectionName));
        services.Configure<RepairOptions>(configuration.GetSection(RepairOptions.SectionName));
        services.Configure<HealthCenterOptions>(configuration.GetSection(HealthCenterOptions.SectionName));
        services.Configure<LoggingFileOptions>(configuration.GetSection(LoggingFileOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IRuleSettings>(_ => new ConfigurationRuleSettings(configuration));
        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddSingleton<IRiskAnalyzer, RiskAnalyzer>();
        services.AddSingleton<IScoringEngine, ScoringEngine>();
        services.AddSingleton<IScanOrchestrator, ScanOrchestrator>();
        return services;
    }
}
