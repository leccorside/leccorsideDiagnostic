using System.Windows;
using System.Windows.Threading;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Settings;
using LeccorWindowsHealthCenter.Infrastructure.Persistence;
using LeccorWindowsHealthCenter.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private ILogger<App>? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var paths = AppPaths.ResolveWritable(AppContext.BaseDirectory);
            _host = HealthCenterHost.Build(e.Args, paths);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível iniciar o Leccor Windows Health Center.\n\n{ex.Message}",
                "Leccor Windows Health Center", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        _logger = _host.Services.GetRequiredService<ILogger<App>>();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        await _host.StartAsync();
        _logger.LogInformation("Aplicativo iniciado (versão {Version}, PID {Pid})",
            typeof(App).Assembly.GetName().Version, Environment.ProcessId);

        try
        {
            var version = await _host.Services.GetRequiredService<MigrationRunner>().MigrateAsync(CancellationToken.None);
            _logger.LogInformation("Banco de dados pronto (schema v{Version})", version);
        }
        catch (Exception ex)
        {
            // Sem banco o diagnóstico continua funcionando; apenas o histórico fica indisponível.
            _logger.LogError(ex, "Falha ao preparar o banco de dados local");
            MessageBox.Show($"O histórico local não pôde ser preparado e ficará indisponível nesta sessão.\n\n{ex.Message}",
                "Leccor Windows Health Center", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        var options = _host.Services.GetRequiredService<IOptions<HealthCenterOptions>>().Value;
        _host.Services.GetRequiredService<ThemeService>().Apply(options.Theme);

        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _logger?.LogInformation("Aplicativo encerrado (código {ExitCode})", e.ApplicationExitCode);
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Exceção não tratada na interface");
        MessageBox.Show($"Ocorreu um erro inesperado. Os detalhes foram registrados no log.\n\n{e.Exception.Message}",
            "Leccor Windows Health Center", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Exceção não observada em tarefa assíncrona");
        e.SetObserved();
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        _logger?.LogCritical(e.ExceptionObject as Exception, "Exceção fatal (terminando: {IsTerminating})", e.IsTerminating);
}
