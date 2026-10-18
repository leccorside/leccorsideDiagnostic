using LeccorWindowsHealthCenter.Infrastructure.SystemInfo;
using LeccorWindowsHealthCenter.Monitoring;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.IntegrationTests.SystemInfo;

/// <summary>
/// Lê as fontes reais da máquina (somente leitura, sem administrador). Valida layouts de P/Invoke e consultas WMI;
/// as asserções aceitam "não disponível" onde o hardware pode legitimamente não expor o dado.
/// </summary>
public class SystemSourcesTests
{
    [Fact]
    public async Task Wmi_inventory_describes_this_machine()
    {
        using var provider = new WmiHardwareInventoryProvider(NullLogger<WmiHardwareInventoryProvider>.Instance, TimeProvider.System);

        var inventory = await provider.GetAsync(CancellationToken.None);

        // Regressão: no .NET 10, opções de enumeração erradas devolviam tudo vazio sem nenhum aviso.
        Assert.True(inventory.Processors.Count > 0, "Inventário vazio. Avisos: " + string.Join(" | ", inventory.Warnings));
        Assert.False(string.IsNullOrWhiteSpace(inventory.Processors[0].Name));
        Assert.True(inventory.Processors.Sum(p => p.LogicalProcessors ?? 0) >= 1);
        Assert.True(inventory.TotalPhysicalMemoryBytes > 0);
        Assert.False(string.IsNullOrWhiteSpace(inventory.Computer.OperatingSystem));
        Assert.All(inventory.MemoryModules, m => Assert.True(m.CapacityBytes > 0));
        Assert.All(inventory.Gpus, g => Assert.False(string.IsNullOrWhiteSpace(g.Name)));
        Assert.All(inventory.Gpus.Where(g => g.DedicatedMemoryBytes is not null), g => Assert.True(g.DedicatedMemoryBytes > 0));
    }

    [Fact]
    public async Task Wmi_inventory_is_cached_unless_refresh_is_forced()
    {
        using var provider = new WmiHardwareInventoryProvider(NullLogger<WmiHardwareInventoryProvider>.Instance, TimeProvider.System);

        var first = await provider.GetAsync(CancellationToken.None);
        var cached = await provider.GetAsync(CancellationToken.None);
        var refreshed = await provider.GetAsync(CancellationToken.None, forceRefresh: true);

        Assert.Same(first, cached);
        Assert.NotSame(first, refreshed);
    }

    [Fact]
    public async Task Pdh_session_reads_cpu_memory_and_rates_after_the_first_interval()
    {
        using var session = new PdhPerformanceCounterSession(NullLogger.Instance);
        await Task.Delay(1100);

        var reading = session.Read();

        Assert.InRange(reading.CpuPercent!.Value, 0, 100);
        Assert.True(reading.ProcessorPerformancePercent > 0);
        Assert.True(reading.ProcessorBaseFrequencyMHz > 0);
        Assert.True(reading.EffectiveFrequencyMHz > 0);
        Assert.NotNull(reading.PagesInputPerSecond);
        Assert.InRange(reading.MemoryUsedPercent!.Value, 1, 100);
        Assert.InRange(reading.CommitPercent!.Value, 1, 100);
        Assert.True(reading.CommitLimitBytes >= reading.TotalPhysicalBytes / 2);
        Assert.True(reading.NonPagedPoolBytes > 0);
        Assert.All(reading.Gpus, g => Assert.InRange(g.UtilizationPercent, 0, 100));
    }

    [Fact]
    public void System_memory_matches_total_physical_memory_order_of_magnitude()
    {
        var memory = SystemMemory.Read();

        Assert.NotNull(memory);
        Assert.True(memory.TotalPhysical > 512L * 1024 * 1024);
        Assert.InRange(memory.AvailablePhysical, 1, memory.TotalPhysical);
        Assert.True(memory.CommitLimit >= memory.CommitTotal);
    }

    [Fact]
    public void Process_snapshot_contains_this_process_with_cpu_time_and_path()
    {
        var provider = new ProcessSnapshotProvider(TimeProvider.System);

        var snapshot = provider.Capture();
        var self = Assert.Single(snapshot.Processes, p => p.Pid == Environment.ProcessId);

        Assert.NotNull(self.TotalProcessorTime);
        Assert.True(self.PrivateBytes > 0);
        Assert.NotNull(self.StartTime);
        Assert.Equal(Environment.ProcessPath, provider.TryGetExecutablePath(Environment.ProcessId), ignoreCase: true);
        Assert.DoesNotContain(snapshot.Processes, p => p.Pid == 0);
    }

    [Fact]
    public void Metrics_history_records_system_and_process_samples()
    {
        var history = new MetricsHistoryService(
            Options.Create(new MetricsHistoryOptions { ProcessMinimumPrivateBytes = 1 }),
            new ProcessSnapshotProvider(TimeProvider.System),
            TimeProvider.System,
            NullLogger<MetricsHistoryService>.Instance);

        history.SampleSystem();
        Thread.Sleep(200);
        history.SampleSystem();
        history.SampleProcesses();

        var system = history.SystemSamples();
        Assert.Equal(2, system.Count);
        Assert.Null(system[0].CpuPercent); // primeira leitura não tem delta
        Assert.InRange(system[1].CpuPercent!.Value, 0, 100);
        Assert.Contains(history.ProcessMemorySamples(), s => s.Pid == Environment.ProcessId);
    }
}
