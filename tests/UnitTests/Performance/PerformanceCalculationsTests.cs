using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Diagnostics.Performance;
using LeccorWindowsHealthCenter.Monitoring.Pdh;
using Metric = LeccorWindowsHealthCenter.Domain.Metric;

namespace LeccorWindowsHealthCenter.UnitTests.Performance;

public class PerformanceCalculationsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Gpu_usage_sums_processes_per_engine_and_takes_the_busiest_engine()
    {
        var engines = new Dictionary<string, double>
        {
            ["pid_100_luid_0x00000000_0x0000D1E8_phys_0_eng_0_engtype_3D"] = 30,
            ["pid_200_luid_0x00000000_0x0000D1E8_phys_0_eng_0_engtype_3D"] = 25,
            ["pid_100_luid_0x00000000_0x0000D1E8_phys_0_eng_5_engtype_VideoDecode"] = 40,
            ["pid_300_luid_0x00000000_0x0000AAAA_phys_0_eng_1_engtype_Copy"] = 10,
            ["lixo_sem_padrao"] = 99,
        };
        var dedicated = new Dictionary<string, double> { ["luid_0x00000000_0x0000D1E8_phys_0"] = 2_000_000_000 };

        var adapters = GpuCounterParser.Aggregate(engines, dedicated);

        var main = Assert.Single(adapters, a => a.AdapterLuid == "0x00000000_0x0000d1e8");
        Assert.Equal(55, main.UtilizationPercent);
        Assert.Equal(55, main.EngineUtilization["3D"]);
        Assert.Equal(40, main.EngineUtilization["VideoDecode"]);
        Assert.Equal(2_000_000_000, main.DedicatedUsageBytes);
        Assert.Equal(10, Assert.Single(adapters, a => a.AdapterLuid == "0x00000000_0x0000aaaa").UtilizationPercent);
    }

    [Fact]
    public void Gpu_usage_is_capped_at_100_percent()
    {
        var engines = new Dictionary<string, double>
        {
            ["pid_1_luid_0x0_0x1_phys_0_eng_0_engtype_3D"] = 80,
            ["pid_2_luid_0x0_0x1_phys_0_eng_0_engtype_3D"] = 70,
        };

        Assert.Equal(100, Assert.Single(GpuCounterParser.Aggregate(engines)).UtilizationPercent);
    }

    [Fact]
    public void Reading_reports_gpu_as_unavailable_without_adapters()
    {
        Assert.Null(new PerformanceReading().GpuPercent);
        Assert.Null(new PerformanceReading().MemoryUsedPercent);
        Assert.Equal(2400, new PerformanceReading { ProcessorBaseFrequencyMHz = 3000, ProcessorPerformancePercent = 80 }.EffectiveFrequencyMHz);
    }

    [Fact]
    public void Process_cpu_is_a_share_of_total_machine_capacity()
    {
        var start = new ProcessSnapshot(T0,
        [
            new ProcessSample { Pid = 10, Name = "build", TotalProcessorTime = TimeSpan.FromSeconds(100), StartTime = T0.AddHours(-1) },
            new ProcessSample { Pid = 20, Name = "idle-app", TotalProcessorTime = TimeSpan.FromSeconds(5), StartTime = T0.AddHours(-1) },
            new ProcessSample { Pid = 30, Name = "protegido", TotalProcessorTime = null },
            new ProcessSample { Pid = 40, Name = "reutilizado", TotalProcessorTime = TimeSpan.FromSeconds(1), StartTime = T0.AddHours(-2) },
        ]);
        var end = new ProcessSnapshot(T0.AddSeconds(10),
        [
            new ProcessSample { Pid = 10, Name = "build", TotalProcessorTime = TimeSpan.FromSeconds(140), StartTime = T0.AddHours(-1) },
            new ProcessSample { Pid = 20, Name = "idle-app", TotalProcessorTime = TimeSpan.FromSeconds(5), StartTime = T0.AddHours(-1) },
            new ProcessSample { Pid = 30, Name = "protegido", TotalProcessorTime = null },
            new ProcessSample { Pid = 40, Name = "novo-com-pid-antigo", TotalProcessorTime = TimeSpan.FromSeconds(9), StartTime = T0.AddSeconds(5) },
        ]);

        var top = ProcessUsageCalculator.TopByCpu(start, end, logicalProcessors: 8, top: 5);

        // 40 s de CPU em 10 s numa máquina de 8 processadores lógicos = 50% da capacidade total.
        var build = Assert.Single(top);
        Assert.Equal("build", build.Name);
        Assert.Equal(50, build.CpuPercent, precision: 6);
    }

    [Fact]
    public void Longest_run_breaks_on_gaps_larger_than_allowed()
    {
        // 95% antes e depois de uma suspensão de 2 horas: sem medição no meio, não é carga contínua.
        var samples = new List<Metric>();
        for (var i = 0; i < 6; i++)
        {
            samples.Add(Findings.Sample("cpu.total", 95, T0.AddSeconds(i * 5)));
        }

        for (var i = 0; i < 6; i++)
        {
            samples.Add(Findings.Sample("cpu.total", 95, T0.AddHours(2).AddSeconds(i * 5)));
        }

        var series = new MetricSeries("cpu.total", samples);

        Assert.True(series.LongestRunAtOrAbove(90) > TimeSpan.FromHours(2));
        Assert.Equal(TimeSpan.FromSeconds(25), series.LongestRunAtOrAbove(90, TimeSpan.FromSeconds(30)));
    }
}
