using LeccorWindowsHealthCenter.Application.SystemInfo;

namespace LeccorWindowsHealthCenter.UnitTests.Startup;

/// <summary>Taxas de CPU e E/S entre duas fotografias da lista de processos.</summary>
public class ProcessRatesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Started = T0.AddHours(-1);

    private static ProcessDetails Process(int pid, double cpuSeconds, long? io = 0, DateTimeOffset? start = null) => new()
    {
        Pid = pid,
        Name = $"p{pid}",
        TotalProcessorTime = TimeSpan.FromSeconds(cpuSeconds),
        IoBytes = io,
        StartTime = start ?? Started,
    };

    private static ProcessInventorySnapshot Snapshot(DateTimeOffset at, params ProcessDetails[] processes) => new(at, processes);

    [Fact]
    public void Cpu_IsShareOfAllLogicalProcessors()
    {
        // 1 s de CPU em 2 s com 4 processadores = 12,5% da máquina (como o Gerenciador de Tarefas).
        var rates = ProcessRates.Compute(Snapshot(T0, Process(10, 5)), Snapshot(T0.AddSeconds(2), Process(10, 6)), 4);

        Assert.Equal(12.5, rates[10].CpuPercent!.Value, 3);
    }

    [Fact]
    public void Cpu_IsClampedTo100()
    {
        var rates = ProcessRates.Compute(Snapshot(T0, Process(10, 0)), Snapshot(T0.AddSeconds(1), Process(10, 10)), 1);

        Assert.Equal(100, rates[10].CpuPercent);
    }

    [Fact]
    public void Io_IsBytesPerSecond_AndNeverNegative()
    {
        var rates = ProcessRates.Compute(
            Snapshot(T0, Process(10, 0, io: 1_000), Process(11, 0, io: 5_000)),
            Snapshot(T0.AddSeconds(2), Process(10, 0, io: 5_000), Process(11, 0, io: 1_000)),
            4);

        Assert.Equal(2_000, rates[10].IoBytesPerSecond);
        Assert.Equal(0, rates[11].IoBytesPerSecond);
    }

    [Fact]
    public void ReusedPid_WithDifferentStart_HasNoRate()
    {
        var rates = ProcessRates.Compute(
            Snapshot(T0, Process(10, 100)),
            Snapshot(T0.AddSeconds(2), Process(10, 1, start: T0.AddSeconds(1))),
            4);

        Assert.False(rates.ContainsKey(10));
    }

    [Fact]
    public void NewProcess_AndUnreadableCounters_HaveNoValue()
    {
        var unreadable = new ProcessDetails { Pid = 12, Name = "protegido", StartTime = Started };
        var rates = ProcessRates.Compute(
            Snapshot(T0, unreadable),
            Snapshot(T0.AddSeconds(2), unreadable, Process(13, 1)),
            4);

        Assert.False(rates.ContainsKey(13));
        Assert.Null(rates[12].CpuPercent);
        Assert.Null(rates[12].IoBytesPerSecond);
    }

    [Fact]
    public void ZeroInterval_ReturnsEmpty()
    {
        var rates = ProcessRates.Compute(Snapshot(T0, Process(10, 0)), Snapshot(T0, Process(10, 1)), 4);

        Assert.Empty(rates);
    }
}
