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

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "O App vive o processo inteiro; _startup é descartado em OnExit (e ao concluir a inicialização).")]
public partial class App : System.Windows.Application
{
    // Criado no construtor: cobre inclusive o carregamento do XAML do App, antes de existir logger.
    private readonly StartupTrace _startup = new(StartupTrace.DefaultLogsDirectory());
    private IHost? _host;
    private ILogger<App>? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _startup.Mark("XAML do App carregado e Dispatcher em execução");
        base.OnStartup(e);

        try
        {
            var paths = AppPaths.ResolveWritable(AppContext.BaseDirectory);
            _startup.Mark("Diretórios de dados resolvidos");
            _host = HealthCenterHost.Build(e.Args, paths);
            _startup.Mark("Host construído (configuração, logging, DI)");
        }
        catch (Exception ex)
        {
            _startup.Dispose();
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
        _startup.Mark("Host iniciado");
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

        _startup.Mark("Banco de dados preparado");

        var options = _host.Services.GetRequiredService<IOptions<HealthCenterOptions>>().Value;
        _host.Services.GetRequiredService<ThemeService>().Apply(options.Theme);

        var window = _host.Services.GetRequiredService<MainWindow>();
        _startup.Mark("Janela principal criada");
        MainWindow = window;
        window.ContentRendered += OnFirstRender;
#if DEBUG
        DevData.DevDataToolbar.Attach(window,
            _host.Services.GetRequiredService<DevData.DevDataSwitch>(),
            _host.Services.GetRequiredService<ViewModels.HardwareViewModel>(),
            _host.Services.GetRequiredService<ViewModels.StorageViewModel>());
#endif
        window.Show();
    }

    private void OnFirstRender(object? sender, EventArgs e)
    {
        ((Window)sender!).ContentRendered -= OnFirstRender;
        _startup.Mark("Primeiro quadro renderizado");
        var summary = _startup.Complete();
        if (_startup.IsSlow)
        {
            _logger?.LogWarning("Inicialização lenta ({TotalMs} ms): {StartupPhases}. Relatório: {SlowReport}",
                (long)_startup.Total.TotalMilliseconds, summary, _startup.SlowReportPath);
        }
        else
        {
            _logger?.LogInformation("Inicialização concluída em {TotalMs} ms: {StartupPhases}", (long)_startup.Total.TotalMilliseconds, summary);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _logger?.LogInformation("Aplicativo encerrado (código {ExitCode})", e.ApplicationExitCode);
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        _startup.Dispose();
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
