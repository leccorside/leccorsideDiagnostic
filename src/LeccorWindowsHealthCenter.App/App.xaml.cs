using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Notifications;
using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.Settings;
using LeccorWindowsHealthCenter.Domain;
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
        // Evita que o WPF encerre prematuramente durante os awaits assíncronos antes da MainWindow ser instanciada
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
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

        // Execução em segundo plano para agendamento (seção 60)
        if (e.Args.Any(a => string.Equals(a, "--scheduled-scan", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(a, "--background-scan", StringComparison.OrdinalIgnoreCase)))
        {
            _logger?.LogInformation("Executando varredura agendada em segundo plano via argumento CLI...");
            try
            {
                var orchestrator = _host.Services.GetRequiredService<IScanOrchestrator>();
                var scanRepo = _host.Services.GetRequiredService<IScanRepository>();
                var notifier = _host.Services.GetRequiredService<INotificationService>();

                var report = await orchestrator.RunAsync(ScanPlan.Quick, null, CancellationToken.None);
                await scanRepo.SaveAsync(report, CancellationToken.None);

                // Gravar amostra contínua de telemetria no SQLite durante varredura em segundo plano (Pacote A)
                var sampleRepo = _host.Services.GetService<IMetricSampleRepository>();
                if (sampleRepo is not null)
                {
                    var mem = LeccorWindowsHealthCenter.Monitoring.SystemMemory.Read();
                    if (mem is not null)
                    {
                        var sample = new MetricSampleRecord
                        {
                            Timestamp = DateTimeOffset.UtcNow,
                            CpuPercent = 0,
                            MemoryUsedPercent = mem.UsedPercent,
                            MemoryUsedBytes = mem.TotalPhysical - mem.AvailablePhysical,
                            CommitUsedPercent = mem.CommitPercent,
                            CommitUsedBytes = mem.CommitTotal,
                        };
                        await sampleRepo.SaveSampleAsync(sample, CancellationToken.None);
                    }
                }

                var problems = report.ConfirmedProblemCount;
                if (problems > 0)
                {
                    await notifier.ShowNotificationAsync(
                        "Leccor Windows Health Center",
                        $"Varredura agendada concluída: {problems} problema(s) detectado(s). Score: {report.Score.Overall?.ToString(CultureInfo.CurrentCulture) ?? "—"}/100");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Falha na execução da varredura agendada");
            }

            Shutdown(0);
            return;
        }

        // Aplicação de preferências persistidas no SQLite (seção 66)
        try
        {
            var configRepo = _host.Services.GetRequiredService<IConfigurationRepository>();
            var savedTheme = await configRepo.GetValueAsync("ui.theme");
            if (!string.IsNullOrEmpty(savedTheme) && Enum.TryParse<AppTheme>(savedTheme, true, out var theme))
            {
                _host.Services.GetRequiredService<ThemeService>().Apply(theme);
            }
            else
            {
                var options = _host.Services.GetRequiredService<IOptions<HealthCenterOptions>>().Value;
                _host.Services.GetRequiredService<ThemeService>().Apply(options.Theme);
            }

            var savedAdvMode = await configRepo.GetValueAsync("ui.advanced-mode");
            if (!string.IsNullOrEmpty(savedAdvMode) && bool.TryParse(savedAdvMode, out var adv))
            {
                _host.Services.GetRequiredService<UiPreferences>().AdvancedMode = adv;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Falha ao carregar preferências persistidas");
            var options = _host.Services.GetRequiredService<IOptions<HealthCenterOptions>>().Value;
            _host.Services.GetRequiredService<ThemeService>().Apply(options.Theme);
        }

        var window = _host.Services.GetRequiredService<MainWindow>();
        _startup.Mark("Janela principal criada");
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.ContentRendered += OnFirstRender;
#if DEBUG
        DevData.DevDataToolbar.Attach(window,
            _host.Services.GetRequiredService<DevData.DevDataSwitch>(),
            _host.Services.GetRequiredService<ViewModels.HardwareViewModel>(),
            _host.Services.GetRequiredService<ViewModels.StorageViewModel>(),
            _host.Services.GetRequiredService<ViewModels.StartupViewModel>());
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
