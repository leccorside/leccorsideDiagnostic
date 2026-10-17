using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using LeccorWindowsHealthCenter.Shared;

namespace LeccorWindowsHealthCenter.App;

/// <summary>
/// Cronometra as fases da inicialização (processo → janela renderizada) e mantém um watchdog:
/// se a inicialização passar de <see cref="SlowThreshold"/>, grava a fase atual em
/// <c>Logs\startup-slow-{pid}.txt</c> — sem depender do logger, que só existe depois do host.
/// Criado após uma inicialização que levou ~30–60 s sem causa identificada (ver CONTEXTO.md).
/// </summary>
public sealed class StartupTrace : IDisposable
{
    public static readonly TimeSpan SlowThreshold = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();
    private readonly List<(string Phase, TimeSpan Elapsed)> _marks = [];
    private readonly DateTime _processStart;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly TimeSpan _processToTrace;
    private readonly string _slowReportPath;
    private readonly Timer _watchdog;
    private bool _completed;

    public StartupTrace(string logsDirectory, TimeSpan? watchdogPeriod = null)
    {
        _processStart = Process.GetCurrentProcess().StartTime;
        _processToTrace = DateTime.Now - _processStart;
        _slowReportPath = Path.Combine(logsDirectory, $"startup-slow-{Environment.ProcessId}.txt");
        var period = watchdogPeriod ?? SlowThreshold;
        _watchdog = new Timer(_ => WriteSlowReport(), null, period, period);
        Mark("Início do rastreamento (CLR e assemblies carregados)");
    }

    /// <summary>Tempo entre a criação do processo e o primeiro código do app (carga do runtime/DLLs, antivírus).</summary>
    public TimeSpan ProcessToTrace => _processToTrace;

    public TimeSpan Total => _processToTrace + _stopwatch.Elapsed;

    public bool IsSlow => Total >= SlowThreshold;

    public string CurrentPhase
    {
        get
        {
            lock (_gate)
            {
                return _marks.Count == 0 ? "(nenhuma)" : _marks[^1].Phase;
            }
        }
    }

    public string SlowReportPath => _slowReportPath;

    public void Mark(string phase)
    {
        lock (_gate)
        {
            _marks.Add((phase, _stopwatch.Elapsed));
        }
    }

    /// <summary>Encerra o watchdog e devolve o resumo das fases (para o log).</summary>
    public string Complete()
    {
        lock (_gate)
        {
            _completed = true;
        }

        _watchdog.Dispose();
        return Summary();
    }

    public string Summary()
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"processo→app {_processToTrace.TotalMilliseconds:0} ms");
        lock (_gate)
        {
            var previous = TimeSpan.Zero;
            foreach (var (phase, elapsed) in _marks)
            {
                builder.Append(CultureInfo.InvariantCulture, $" | {phase}: +{(elapsed - previous).TotalMilliseconds:0} ms");
                previous = elapsed;
            }
        }

        builder.Append(CultureInfo.InvariantCulture, $" | total {Total.TotalMilliseconds:0} ms");
        return builder.ToString();
    }

    public void Dispose() => _watchdog.Dispose();

    private void WriteSlowReport()
    {
        lock (_gate)
        {
            if (_completed)
            {
                return;
            }
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_slowReportPath)!);
            var line = string.Create(CultureInfo.InvariantCulture,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} inicialização em andamento há {Total.TotalSeconds:0.0} s; fase atual: \"{CurrentPhase}\"; {Summary()}{Environment.NewLine}");
            File.AppendAllText(_slowReportPath, line, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Diagnóstico é best-effort: nunca derrubar a inicialização por causa dele.
        }
    }

    /// <summary>Pasta de logs resolvida sem efeitos colaterais (o host ainda não existe neste ponto).</summary>
    public static string DefaultLogsDirectory() => AppPaths.Resolve(AppContext.BaseDirectory).Logs;
}
