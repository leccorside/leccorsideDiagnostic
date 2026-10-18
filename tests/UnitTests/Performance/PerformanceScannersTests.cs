using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Diagnostics.Hardware;
using LeccorWindowsHealthCenter.Diagnostics.Performance;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.UnitTests.Performance;

public class PerformanceScannersTests
{
    private static readonly IOptions<SamplingOptions> FastSampling = Options.Create(new SamplingOptions
    {
        Window = TimeSpan.FromMilliseconds(5),
        Interval = TimeSpan.FromMilliseconds(1),
        GpuWindow = TimeSpan.FromMilliseconds(3),
        TopProcesses = 3,
    });

    private sealed class FakeSession : IPerformanceCounterSession
    {
        private int _reads;

        public Func<int, PerformanceReading> Reading { get; init; } = i => new PerformanceReading { CpuPercent = 10 + i };

        public void Dispose()
        {
        }

        public PerformanceReading Read() => Reading(_reads++) with { Timestamp = DateTimeOffset.UtcNow.AddMilliseconds(_reads) };
    }

    private sealed class FakeFactory : IPerformanceCounterSessionFactory
    {
        public Func<int, PerformanceReading>? Reading { get; init; }

        public IPerformanceCounterSession Create() => Reading is null ? new FakeSession() : new FakeSession { Reading = Reading };
    }

    private sealed class FakeProcesses : IProcessSnapshotProvider
    {
        // Horário de início fixo: o calculador descarta processos cujo início muda (PID reutilizado).
        private readonly DateTimeOffset _start = DateTimeOffset.UtcNow.AddHours(-1);
        private int _calls;

        public ProcessSnapshot Capture(bool includePaths = false)
        {
            var start = _start;
            var cpu = _calls++ == 0 ? 10 : 15; // +5 s de CPU entre as duas fotografias
            return new ProcessSnapshot(DateTimeOffset.UtcNow.AddSeconds(_calls == 1 ? -10 : 0),
            [
                new ProcessSample { Pid = 7, Name = "devenv", TotalProcessorTime = TimeSpan.FromSeconds(cpu), StartTime = start, PrivateBytes = 900_000_000, WorkingSetBytes = 700_000_000 },
                new ProcessSample { Pid = 8, Name = "notepad", TotalProcessorTime = TimeSpan.FromSeconds(1), StartTime = start, PrivateBytes = 5_000_000 },
            ]);
        }

        public string? TryGetExecutablePath(int pid) => pid == 7 ? @"C:\VS\devenv.exe" : null;
    }

    private sealed class FakeHistory : IMetricsHistory
    {
        public TimeSpan Retention => TimeSpan.FromMinutes(30);

        public IReadOnlyList<SystemHistorySample> SystemSamples() =>
        [
            new(DateTimeOffset.UtcNow.AddMinutes(-2), 50, 60, 40),
            new(DateTimeOffset.UtcNow.AddMinutes(-1), null, 61, 41), // CPU indisponível nesta amostra
        ];

        public IReadOnlyList<ProcessMemorySample> ProcessMemorySamples() =>
        [
            new(DateTimeOffset.UtcNow.AddMinutes(-2), 7, "devenv", DateTimeOffset.UtcNow.AddHours(-1), 800_000_000),
            new(DateTimeOffset.UtcNow.AddMinutes(-1), 7, "devenv", DateTimeOffset.UtcNow.AddHours(-1), 850_000_000),
        ];
    }

    private sealed class FakeInventory : IHardwareInventoryProvider
    {
        public Task<HardwareInventory> GetAsync(CancellationToken cancellationToken, bool forceRefresh = false) => Task.FromResult(new HardwareInventory
        {
            Computer = new ComputerInfo { Model = "Workstation" },
            TotalPhysicalMemoryBytes = 34_000_000_000,
            Processors = [new ProcessorInfo { Name = "CPU X", Cores = 8, LogicalProcessors = 16 }],
            MemoryModules = [new MemoryModuleInfo { CapacityBytes = 17_000_000_000 }, new MemoryModuleInfo { CapacityBytes = 17_000_000_000 }],
            Gpus = [new GpuInfo { Name = "GPU Y", Vendor = "NVIDIA", DedicatedMemoryBytes = 8L * 1024 * 1024 * 1024, DriverVersion = "32.0.15.6094" }],
        });
    }

    [Fact]
    public async Task Cpu_scanner_merges_history_with_scan_samples_and_reports_top_processes()
    {
        var scanner = new CpuScanner(new FakeFactory(), new FakeProcesses(), new FakeHistory(), FastSampling, TimeProvider.System, NullLogger<CpuScanner>.Instance);

        var result = await scanner.ScanAsync(CancellationToken.None);

        Assert.Equal(ScannerRunStatus.Completed, result.Status);
        var cpu = result.Metrics.Where(m => m.Name == PerfNames.CpuTotal).ToList();
        Assert.Single(cpu, m => m.HasTag(PerfNames.SourceTag, PerfNames.SourceHistory)); // amostra sem CPU não vira 0
        Assert.Equal(FastSampling.Value.SampleCount, cpu.Count(m => m.HasTag(PerfNames.SourceTag, PerfNames.SourceScan)));
        Assert.DoesNotContain(result.Metrics, m => m.Name == PerfNames.CpuPerformance); // leitura sem o contador: nada inventado

        var top = Assert.Single(result.Observations, o => o.Kind == PerfNames.ObservationProcessCpu);
        Assert.Equal("devenv", top.GetAttribute("name"));
        Assert.Equal(@"C:\VS\devenv.exe", top.GetAttribute("path"));
    }

    [Fact]
    public async Task Memory_scanner_reports_absolutes_once_and_process_history_with_identity_tags()
    {
        var factory = new FakeFactory
        {
            Reading = i => new PerformanceReading
            {
                TotalPhysicalBytes = 32_000_000_000,
                AvailablePhysicalBytes = 8_000_000_000,
                CommitTotalBytes = 30_000_000_000,
                CommitLimitBytes = 40_000_000_000,
                PagesInputPerSecond = i,
            },
        };
        var scanner = new MemoryScanner(factory, new FakeProcesses(), new FakeHistory(), FastSampling, TimeProvider.System, NullLogger<MemoryScanner>.Instance);

        var result = await scanner.ScanAsync(CancellationToken.None);

        Assert.Single(result.Metrics, m => m.Name == PerfNames.MemoryTotal);
        Assert.Equal(75, result.Metrics.First(m => m.Name == PerfNames.MemoryUsed && m.HasTag(PerfNames.SourceTag, PerfNames.SourceScan)).Value);
        Assert.Equal(2, result.Metrics.Count(m => m.Name == PerfNames.ProcessPrivate && m.HasTag("pid", "7") && m.Tags.ContainsKey("start")));
        Assert.Equal("devenv", result.Observations.First(o => o.Kind == PerfNames.ObservationProcessMemory).GetAttribute("name"));
    }

    [Fact]
    public async Task Hardware_and_gpu_scanners_describe_the_inventory()
    {
        var hardware = await new HardwareScanner(new FakeInventory(), NullLogger<HardwareScanner>.Instance).ScanAsync(CancellationToken.None);
        var gpu = await new GpuScanner(new FakeInventory(), new FakeFactory
        {
            Reading = _ => new PerformanceReading { Gpus = [new GpuAdapterUsage { AdapterLuid = "0x0_0x1", UtilizationPercent = 33, DedicatedUsageBytes = 1_000 }] },
        }, FastSampling, TimeProvider.System, NullLogger<GpuScanner>.Instance).ScanAsync(CancellationToken.None);

        Assert.Equal(8, hardware.Metrics.Single(m => m.Name == PerfNames.CpuCores).Value);
        Assert.Equal(2, hardware.Metrics.Single(m => m.Name == PerfNames.MemoryModules).Value);
        Assert.Contains("\"Model\":\"Workstation\"", Assert.Single(hardware.Observations, o => o.Kind == HardwareInventory.ObservationKind).GetAttribute("json"), StringComparison.Ordinal);

        Assert.Equal("NVIDIA", Assert.Single(gpu.Observations, o => o.Kind == PerfNames.ObservationGpuAdapter).GetAttribute("vendor"));
        Assert.All(gpu.Metrics.Where(m => m.Name == PerfNames.GpuUtilization), m => Assert.Equal(33, m.Value));
        Assert.Equal(1, gpu.Metrics.Single(m => m.Name == PerfNames.GpuCount).Value);
    }
}
