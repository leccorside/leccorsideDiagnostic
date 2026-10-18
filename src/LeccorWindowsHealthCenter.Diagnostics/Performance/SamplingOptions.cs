namespace LeccorWindowsHealthCenter.Diagnostics.Performance;

/// <summary>Amostragem dos scanners de performance durante a varredura (seção "Sampling" do appsettings.json).</summary>
public sealed class SamplingOptions
{
    public const string SectionName = "Sampling";

    /// <summary>
    /// Janela de amostragem de CPU/memória na varredura. Curta de propósito (varredura rápida em 1–3 min);
    /// a duração e a recorrência vêm do histórico de fundo (IMetricsHistory).
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(15);

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Janela da GPU (contadores "GPU Engine" são mais caros de ler).</summary>
    public TimeSpan GpuWindow { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Quantos processos aparecem como "maiores consumidores".</summary>
    public int TopProcesses { get; set; } = 10;

    public int SampleCount => Math.Max(1, (int)Math.Round(Window / Interval));

    public int GpuSampleCount => Math.Max(1, (int)Math.Round(GpuWindow / Interval));
}

/// <summary>Nomes de métricas e observações produzidos pelos scanners de performance e hardware.</summary>
public static class PerfNames
{
    public const string SourceTag = "source";
    public const string SourceScan = "scan";
    public const string SourceHistory = "history";

    public const string CpuTotal = "cpu.total";
    public const string CpuPerformance = "cpu.performance";
    public const string CpuBaseFrequency = "cpu.frequency.base";
    public const string CpuEffectiveFrequency = "cpu.frequency.effective";
    public const string CpuQueue = "cpu.queue";
    public const string CpuLogical = "cpu.logical";
    public const string CpuCores = "cpu.cores";

    public const string MemoryUsed = "memory.used";
    public const string MemoryTotal = "memory.total";
    public const string MemoryAvailable = "memory.available";
    public const string MemoryCommit = "memory.commit";
    public const string MemoryCommitTotal = "memory.commit.total";
    public const string MemoryCommitLimit = "memory.commit.limit";
    public const string MemoryCache = "memory.cache";
    public const string MemoryPagedPool = "memory.pool.paged";
    public const string MemoryNonPagedPool = "memory.pool.nonpaged";
    public const string MemoryPagesInput = "memory.pages_input";
    public const string MemoryPageFile = "memory.pagefile";
    public const string MemoryInstalled = "memory.installed";
    public const string MemoryModules = "memory.modules";

    public const string ProcessPrivate = "process.private";

    public const string GpuUtilization = "gpu.utilization";
    public const string GpuDedicatedUsed = "gpu.dedicated.used";
    public const string GpuCount = "gpu.count";

    public const string ObservationProcessCpu = "process.cpu";
    public const string ObservationProcessMemory = "process.memory";
    public const string ObservationHardwareInventory = Application.SystemInfo.HardwareInventory.ObservationKind;
    public const string ObservationGpuAdapter = "gpu.adapter";
    public const string ObservationSampling = "sampling";
}
