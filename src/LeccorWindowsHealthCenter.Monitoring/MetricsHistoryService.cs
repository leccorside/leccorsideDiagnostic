using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.Monitoring;

public sealed class MetricsHistoryOptions
{
    public const string SectionName = "MetricsHistory";

    public bool Enabled { get; set; } = true;

    /// <summary>Intervalo das amostras de CPU/memória do sistema. Custo: duas chamadas Win32 (microssegundos).</summary>
    public TimeSpan SystemInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Intervalo das amostras de memória por processo (lista de processos; alguns ms).</summary>
    public TimeSpan ProcessInterval { get; set; } = TimeSpan.FromSeconds(60);

    public TimeSpan Retention { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Só processos com pelo menos esta memória privada entram no histórico (limita memória e ruído).</summary>
    public long ProcessMinimumPrivateBytes { get; set; } = 50L * 1024 * 1024;
}

/// <summary>
/// Histórico em memória de CPU, memória e memória privada por processo, mantido enquanto o app está aberto.
/// Dá às regras duração e recorrência (seção 49) sem depender de a varredura durar minutos.
/// Nada é gravado em disco; ao fechar o app o histórico some.
/// </summary>
public sealed class MetricsHistoryService : BackgroundService, IMetricsHistory
{
    private readonly MetricsHistoryOptions _options;
    private readonly IProcessSnapshotProvider _processes;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MetricsHistoryService> _logger;
    private readonly IMetricSampleRepository? _metricSampleRepository;
    private readonly CpuUsageMeter _cpu = new();
    private readonly object _gate = new();
    private readonly Queue<SystemHistorySample> _system = new();
    private readonly Queue<ProcessMemorySample> _processSamples = new();
    private DateTimeOffset _lastPersistedAt = DateTimeOffset.MinValue;

    public MetricsHistoryService(
        IOptions<MetricsHistoryOptions> options,
        IProcessSnapshotProvider processes,
        TimeProvider timeProvider,
        ILogger<MetricsHistoryService> logger,
        IMetricSampleRepository? metricSampleRepository = null)
    {
        _options = options.Value;
        _processes = processes;
        _timeProvider = timeProvider;
        _logger = logger;
        _metricSampleRepository = metricSampleRepository;
    }

    public TimeSpan Retention => _options.Retention;

    public IReadOnlyList<SystemHistorySample> SystemSamples()
    {
        lock (_gate)
        {
            return _system.ToArray();
        }
    }

    public IReadOnlyList<ProcessMemorySample> ProcessMemorySamples()
    {
        lock (_gate)
        {
            return _processSamples.ToArray();
        }
    }

    /// <summary>Executa uma amostragem de sistema (público para testes).</summary>
    public void SampleSystem()
    {
        var cpu = _cpu.Sample();
        var memory = SystemMemory.Read();
        var now = _timeProvider.GetUtcNow();
        var sample = new SystemHistorySample(now, cpu, memory?.UsedPercent, memory?.CommitPercent);
        lock (_gate)
        {
            _system.Enqueue(sample);
            Trim(_system, s => s.Timestamp);
        }

        // Persistência contínua no SQLite a cada 60s (Pacote A)
        if (_metricSampleRepository is not null && now - _lastPersistedAt >= TimeSpan.FromMinutes(1))
        {
            _lastPersistedAt = now;
            var record = new MetricSampleRecord
            {
                Timestamp = now,
                CpuPercent = cpu ?? 0,
                MemoryUsedPercent = memory?.UsedPercent ?? 0,
                MemoryUsedBytes = memory is not null ? memory.TotalPhysical - memory.AvailablePhysical : 0,
                CommitUsedPercent = memory?.CommitPercent ?? 0,
                CommitUsedBytes = memory?.CommitTotal ?? 0,
            };

            _ = Task.Run(async () =>
            {
                try
                {
                    await _metricSampleRepository.SaveSampleAsync(record, CancellationToken.None).ConfigureAwait(false);
                    // Retenção automática de 7 dias para não crescer indefinidamente
                    await _metricSampleRepository.PruneOlderThanAsync(now.AddDays(-7), CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Falha ao persistir amostra de telemetria no SQLite");
                }
            });
        }
    }

    /// <summary>Executa uma amostragem de memória por processo (público para testes).</summary>
    public void SampleProcesses()
    {
        var snapshot = _processes.Capture();
        var samples = snapshot.Processes
            .Where(p => p.PrivateBytes >= _options.ProcessMinimumPrivateBytes)
            .Select(p => new ProcessMemorySample(snapshot.CapturedAt, p.Pid, p.Name, p.StartTime, p.PrivateBytes!.Value))
            .ToList();

        lock (_gate)
        {
            foreach (var sample in samples)
            {
                _processSamples.Enqueue(sample);
            }

            Trim(_processSamples, s => s.Timestamp);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Histórico de métricas desativado por configuração");
            return;
        }

        _logger.LogInformation("Histórico de métricas iniciado (sistema a cada {SystemInterval}, processos a cada {ProcessInterval}, retenção {Retention})",
            _options.SystemInterval, _options.ProcessInterval, _options.Retention);

        var ticksPerProcessSample = Math.Max(1, (int)Math.Round(_options.ProcessInterval / _options.SystemInterval));
        var tick = 0;
        using var timer = new PeriodicTimer(_options.SystemInterval, _timeProvider);
        try
        {
            SampleSystem(); // linha de base da CPU
            do
            {
                try
                {
                    SampleSystem();
                    if (tick++ % ticksPerProcessSample == 0)
                    {
                        SampleProcesses();
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Falha em uma amostragem do histórico de métricas; continuará no próximo ciclo");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private void Trim<T>(Queue<T> queue, Func<T, DateTimeOffset> timestamp)
    {
        var cutoff = _timeProvider.GetUtcNow() - _options.Retention;
        while (queue.Count > 0 && timestamp(queue.Peek()) < cutoff)
        {
            queue.Dequeue();
        }
    }
}
