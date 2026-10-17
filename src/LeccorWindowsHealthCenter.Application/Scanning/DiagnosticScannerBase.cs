using System.Diagnostics;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Application.Scanning;

/// <summary>
/// Base para scanners: mede duração, registra logs/observabilidade, converte exceções em
/// resultado <see cref="ScannerRunStatus.Failed"/> e respeita cancelamento.
/// Scanners concretos implementam apenas <see cref="CollectAsync"/>.
/// </summary>
public abstract class DiagnosticScannerBase : IDiagnosticScanner
{
    protected DiagnosticScannerBase(ILogger logger)
    {
        Logger = logger;
    }

    public abstract string Id { get; }

    public abstract string Name { get; }

    public abstract ScanModule Module { get; }

    public virtual bool IncludedInQuickScan => true;

    public virtual int Order => 1000;

    protected ILogger Logger { get; }

    public async Task<DiagnosticResult> ScanAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var collector = new ScanCollector(Id);
        Logger.LogInformation("Scanner {ScannerId} iniciado", Id);

        try
        {
            await CollectAsync(collector, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            Logger.LogInformation(
                "Scanner {ScannerId} concluído em {DurationMs} ms ({MetricCount} métricas, {ObservationCount} observações)",
                Id, stopwatch.ElapsedMilliseconds, collector.Metrics.Count, collector.Observations.Count);

            return new DiagnosticResult
            {
                ScannerId = Id,
                ScannerName = Name,
                Module = Module,
                Status = ScannerRunStatus.Completed,
                Duration = stopwatch.Elapsed,
                Metrics = collector.Metrics,
                Observations = collector.Observations,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Logger.LogWarning("Scanner {ScannerId} cancelado após {DurationMs} ms", Id, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Scanner {ScannerId} falhou após {DurationMs} ms", Id, stopwatch.ElapsedMilliseconds);
            return DiagnosticResult.Failed(Id, Name, Module, stopwatch.Elapsed, $"Falha ao coletar dados: {ex.Message}");
        }
    }

    protected abstract Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken);
}
