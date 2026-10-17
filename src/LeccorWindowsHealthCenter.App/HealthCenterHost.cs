using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.App.ViewModels;
using LeccorWindowsHealthCenter.Application;
using LeccorWindowsHealthCenter.Application.Settings;
using LeccorWindowsHealthCenter.Diagnostics;
using LeccorWindowsHealthCenter.Infrastructure;
using LeccorWindowsHealthCenter.Infrastructure.Logging;
using LeccorWindowsHealthCenter.Monitoring;
using LeccorWindowsHealthCenter.Repair;
using LeccorWindowsHealthCenter.Reporting;
using LeccorWindowsHealthCenter.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace LeccorWindowsHealthCenter.App;

/// <summary>
/// Composition root: configuração, logging, DI de todas as camadas e da UI.
/// Separado do <see cref="App"/> para ser exercitado pelos System Tests sem abrir janelas.
/// </summary>
public static class HealthCenterHost
{
    public static IHost Build(string[] args, AppPaths paths, Action<IServiceCollection>? overrideServices = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        paths.EnsureCreated();

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ApplicationName = "LeccorWindowsHealthCenter",
            ContentRootPath = AppContext.BaseDirectory,
        });

        var fileLogging = builder.Configuration.GetSection(LoggingFileOptions.SectionName).Get<LoggingFileOptions>() ?? new LoggingFileOptions();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(LoggingSetup.CreateLogger(paths, fileLogging), dispose: true);

        builder.Services
            .AddHealthCenterApplication(builder.Configuration)
            .AddHealthCenterInfrastructure(builder.Configuration, paths)
            .AddHealthCenterDiagnostics()
            .AddHealthCenterRepair()
            .AddHealthCenterMonitoring()
            .AddHealthCenterReporting();

        // UI
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<ScanSession>();
        builder.Services.AddSingleton<UiPreferences>();
        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<ScanViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        overrideServices?.Invoke(builder.Services);

        builder.ConfigureContainer(new Microsoft.Extensions.DependencyInjection.DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        }));

        return builder.Build();
    }
}
