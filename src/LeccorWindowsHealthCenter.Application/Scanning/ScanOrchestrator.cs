using System.Diagnostics;
using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Scoring;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.Application.Scanning;

public interface IScanOrchestrator
{
    IReadOnlyList<IDiagnosticScanner> Scanners { get; }

    Task<ScanReport> RunAsync(ScanPlan plan, IProgress<ScanProgress>? progress, CancellationToken cancellationToken);
}

/// <summary>
/// Orquestra VARREDURA → DIAGNÓSTICO → CLASSIFICAÇÃO (seção 1): executa scanners com concorrência
/// limitada e timeout individual, aplica o RuleEngine, calcula o score e persiste o resultado.
/// Falha de um scanner nunca derruba a varredura; cancelamento preserva o que já foi coletado.
/// </summary>
public sealed class ScanOrchestrator : IScanOrchestrator
{
    private readonly IRuleEngine _ruleEngine;
    private readonly IScoringEngine _scoringEngine;
    private readonly IScanRepository _repository;
    private readonly ScanOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ScanOrchestrator> _logger;

    public ScanOrchestrator(
        IEnumerable<IDiagnosticScanner> scanners,
        IRuleEngine ruleEngine,
        IScoringEngine scoringEngine,
        IScanRepository repository,
        IOptions<ScanOptions> options,
        TimeProvider timeProvider,
        ILogger<ScanOrchestrator> logger)
    {
        Scanners = scanners.OrderBy(s => s.Order).ThenBy(s => s.Name, StringComparer.CurrentCulture).ToList();
        _ruleEngine = ruleEngine;
        _scoringEngine = scoringEngine;
        _repository = repository;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public IReadOnlyList<IDiagnosticScanner> Scanners { get; }

    public async Task<ScanReport> RunAsync(ScanPlan plan, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var startedAt = _timeProvider.GetUtcNow();
        var selected = Scanners.Where(plan.Includes).ToList();
        var states = selected.Select(s => new ScannerProgress(s.Id, s.Name, ScannerRunStatus.Pending)).ToArray();
        var gate = new object();

        void SetState(int index, ScannerRunStatus status)
        {
            ScanProgress snapshot;
            lock (gate)
            {
                states[index] = states[index] with { Status = status };
                snapshot = new ScanProgress(states.ToArray());
            }

            progress?.Report(snapshot);
        }

        _logger.LogInformation("Varredura {Mode} iniciada com {Count} scanners (concorrência máx. {MaxConcurrency})",
            plan.Mode, selected.Count, _options.MaxConcurrency);
        progress?.Report(new ScanProgress(states.ToArray()));

        using var throttle = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrency));
        var tasks = selected.Select((scanner, index) => RunScannerAsync(scanner, index, throttle, SetState, cancellationToken)).ToArray();
        var rawResults = await Task.WhenAll(tasks).ConfigureAwait(false);

        var wasCancelled = cancellationToken.IsCancellationRequested;
        var evaluated = _ruleEngine.Evaluate(rawResults);
        var score = _scoringEngine.Calculate(evaluated);

        var report = new ScanReport
        {
            Mode = plan.Mode,
            StartedAt = startedAt,
            CompletedAt = _timeProvider.GetUtcNow(),
            WasCancelled = wasCancelled,
            Results = evaluated,
            Score = score,
        };

        _logger.LogInformation(
            "Varredura {ScanId} {Status} em {DurationMs} ms: score {Score}, {Problems} problemas, {Recommendations} recomendações",
            report.Id, wasCancelled ? "cancelada" : "concluída", (long)report.Duration.TotalMilliseconds,
            score.Overall?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "sem dados", report.Problems.Count, report.Recommendations.Count);

        try
        {
            // Persistir mesmo quando cancelada: o usuário pode ver o que foi coletado. Não usa o token cancelado.
            await _repository.SaveAsync(report, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao salvar a varredura {ScanId} no histórico", report.Id);
        }

        return report;
    }

    private async Task<DiagnosticResult> RunScannerAsync(
        IDiagnosticScanner scanner,
        int index,
        SemaphoreSlim throttle,
        Action<int, ScannerRunStatus> setState,
        CancellationToken cancellationToken)
    {
        var stopwatch = new Stopwatch();
        var acquired = false;
        try
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;
            setState(index, ScannerRunStatus.Running);
            stopwatch.Start();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.ScannerTimeout);

            DiagnosticResult result;
            try
            {
                // Task.Run: scanners podem ter trechos síncronos (WMI, EventLog) — nunca bloquear a UI.
                result = await Task.Run(() => scanner.ScanAsync(timeout.Token), timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Scanner {ScannerId} excedeu o tempo limite de {Timeout}", scanner.Id, _options.ScannerTimeout);
                result = DiagnosticResult.Failed(scanner.Id, scanner.Name, scanner.Module, stopwatch.Elapsed,
                    $"Tempo limite excedido ({_options.ScannerTimeout.TotalSeconds:0}s).");
            }

            setState(index, result.Status);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            setState(index, ScannerRunStatus.Cancelled);
            return new DiagnosticResult
            {
                ScannerId = scanner.Id,
                ScannerName = scanner.Name,
                Module = scanner.Module,
                Status = ScannerRunStatus.Cancelled,
                Duration = stopwatch.Elapsed,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao executar o scanner {ScannerId}", scanner.Id);
            setState(index, ScannerRunStatus.Failed);
            return DiagnosticResult.Failed(scanner.Id, scanner.Name, scanner.Module, stopwatch.Elapsed, $"Erro inesperado: {ex.Message}");
        }
        finally
        {
            if (acquired)
            {
                throttle.Release();
            }
        }
    }
}
