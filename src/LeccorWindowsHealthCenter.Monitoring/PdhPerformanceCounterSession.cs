using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Monitoring.Pdh;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Monitoring;

/// <summary>
/// Sessão de contadores PDH + memória do sistema. Cada sessão tem sua própria consulta PDH,
/// necessária para calcular taxas entre coletas consecutivas.
/// </summary>
public sealed class PdhPerformanceCounterSession : IPerformanceCounterSession
{
    internal const string CpuTime = @"\Processor Information(_Total)\% Processor Time";
    internal const string CpuPerformance = @"\Processor Information(_Total)\% Processor Performance";
    internal const string CpuFrequency = @"\Processor Information(_Total)\Processor Frequency";
    internal const string QueueLength = @"\System\Processor Queue Length";
    internal const string PagesInput = @"\Memory\Pages Input/sec";
    internal const string PageFileUsage = @"\Paging File(_Total)\% Usage";
    internal const string GpuEngine = @"\GPU Engine(*)\Utilization Percentage";
    internal const string GpuDedicated = @"\GPU Adapter Memory(*)\Dedicated Usage";
    internal const string GpuShared = @"\GPU Adapter Memory(*)\Shared Usage";

    private readonly PdhQuery? _query;
    private readonly object _gate = new();

    public PdhPerformanceCounterSession(ILogger logger, bool includeCpuAndMemory = true, bool includeGpu = true)
    {
        try
        {
            _query = new PdhQuery();
            var paths = new List<string>();
            if (includeCpuAndMemory)
            {
                paths.AddRange([CpuTime, CpuPerformance, CpuFrequency, QueueLength, PagesInput, PageFileUsage]);
            }

            if (includeGpu)
            {
                paths.AddRange([GpuEngine, GpuDedicated, GpuShared]);
            }

            var missing = paths.Where(p => !_query.TryAdd(p)).ToList();
            if (missing.Count > 0)
            {
                logger.LogInformation("Contadores indisponíveis nesta máquina (serão exibidos como \"não disponível\"): {Counters}", string.Join(", ", missing));
            }

            _query.Collect(); // linha de base para os contadores de taxa
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "PDH indisponível; somente métricas de memória do sistema serão lidas");
            _query?.Dispose();
            _query = null;
        }
    }

    public PerformanceReading Read()
    {
        lock (_gate)
        {
            var memory = SystemMemory.Read();
            if (_query is null || !_query.Collect())
            {
                return FromMemory(memory);
            }

            var gpus = _query.Has(GpuEngine) || _query.Has(GpuDedicated)
                ? GpuCounterParser.Aggregate(_query.Values(GpuEngine), _query.Values(GpuDedicated), _query.Values(GpuShared))
                : [];

            return FromMemory(memory) with
            {
                CpuPercent = Clamp(_query.Value(CpuTime)),
                ProcessorPerformancePercent = _query.Value(CpuPerformance),
                ProcessorBaseFrequencyMHz = _query.Value(CpuFrequency),
                ProcessorQueueLength = _query.Value(QueueLength),
                PagesInputPerSecond = _query.Value(PagesInput),
                PageFileUsagePercent = Clamp(_query.Value(PageFileUsage)),
                Gpus = gpus,
            };
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _query?.Dispose();
        }
    }

    private static double? Clamp(double? value) => value is { } v ? Math.Clamp(v, 0, 100) : null;

    private static PerformanceReading FromMemory(SystemMemoryStatus? memory) => new()
    {
        TotalPhysicalBytes = memory?.TotalPhysical,
        AvailablePhysicalBytes = memory?.AvailablePhysical,
        CommitTotalBytes = memory?.CommitTotal,
        CommitLimitBytes = memory?.CommitLimit,
        SystemCacheBytes = memory?.SystemCache,
        PagedPoolBytes = memory?.PagedPool,
        NonPagedPoolBytes = memory?.NonPagedPool,
    };
}

public sealed class PdhPerformanceCounterSessionFactory : IPerformanceCounterSessionFactory
{
    private readonly ILogger<PdhPerformanceCounterSession> _logger;

    public PdhPerformanceCounterSessionFactory(ILogger<PdhPerformanceCounterSession> logger)
    {
        _logger = logger;
    }

    public IPerformanceCounterSession Create() => new PdhPerformanceCounterSession(_logger);
}
