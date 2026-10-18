#if DEBUG
using LeccorWindowsHealthCenter.Application.SystemInfo;

namespace LeccorWindowsHealthCenter.App.DevData;

/// <summary>
/// Fixtures de teste de estresse da interface (skill break-ui). SÓ EM DEBUG — não existe no build Release.
/// Cada valor é algo que uma máquina Windows real produz (nada de "aaaa…"): textos padrão de placa genérica,
/// códigos JEDEC no lugar do fabricante da RAM, part numbers com espaços, nomes reais e longos de processos.
/// </summary>
public static class DevDataFixtures
{
    private static readonly DateTimeOffset Epoch = DateTimeOffset.UtcNow.AddHours(-3);

    // ---------------- Inventário ----------------

    public static HardwareInventory Inventory(DevDataMode mode) => mode switch
    {
        DevDataMode.Empty => new HardwareInventory
        {
            // O bug real do Passo 2: WMI devolvendo tudo vazio sem erro.
            Computer = new ComputerInfo(),
            CapturedAt = DateTimeOffset.UtcNow,
        },
        DevDataMode.One => new HardwareInventory
        {
            // VM mínima: 1 núcleo, 1 thread, 1 módulo, adaptador básico da Microsoft.
            Computer = new ComputerInfo { Manufacturer = "Microsoft Corporation", Model = "Virtual Machine", OperatingSystem = "Microsoft Windows 10 Pro", OsArchitecture = "64 bits", OsVersion = "10.0.19045", OsBuild = "19045" },
            Processors = [new ProcessorInfo { Name = "Intel(R) Xeon(R) CPU E5-2673 v4 @ 2.30GHz", Manufacturer = "GenuineIntel", Cores = 1, LogicalProcessors = 1, MaxClockMHz = 2295 }],
            MemoryModules = [new MemoryModuleInfo { CapacityBytes = 1L << 30, Slot = "M0001" }],
            TotalPhysicalMemoryBytes = 1L << 30,
            Gpus = [new GpuInfo { Name = "Microsoft Hyper-V Video", Vendor = "Microsoft Corporation", Status = "OK" }],
            CapturedAt = DateTimeOffset.UtcNow,
        },
        DevDataMode.Many => WorstInventory(modules: 24, extraGpus: true),
        _ => WorstInventory(modules: 16, extraGpus: true),
    };

    private static HardwareInventory WorstInventory(int modules, bool extraGpus) => new()
    {
        Computer = new ComputerInfo
        {
            Manufacturer = "To Be Filled By O.E.M.",
            Model = "To Be Filled By O.E.M.",
            OperatingSystem = "Microsoft Windows 11 Pro for Workstations Insider Preview",
            OsArchitecture = "64 bits",
            OsVersion = "10.0.27718",
            OsBuild = "27718",
            LastBoot = DateTimeOffset.UtcNow.AddDays(-47),
            BiosVendor = "American Megatrends International, LLC.",
            BiosVersion = "P3.10 (Beta) - Instant Flash compatible",
            BiosDate = null, // data da BIOS ausente
            Motherboard = "ASRock Rack WRX90 WS EVO Dual Socket Workstation Board (Rev. 1.02)",
        },
        Processors =
        [
            new ProcessorInfo { Name = "Intel(R) Xeon(R) Platinum 8480+ CPU @ 2.00GHz (Sapphire Rapids, 56-Core Processor)  ", Manufacturer = "GenuineIntel", Cores = 56, LogicalProcessors = 112, MaxClockMHz = 2001, L2CacheKb = 114688, L3CacheKb = 107520, VirtualizationFirmwareEnabled = true },
            new ProcessorInfo { Name = "Intel(R) Xeon(R) Platinum 8480+ CPU @ 2.00GHz (Sapphire Rapids, 56-Core Processor)  ", Manufacturer = null, Cores = null, LogicalProcessors = null, MaxClockMHz = null },
        ],
        MemoryModules = Enumerable.Range(0, modules).Select(i => new MemoryModuleInfo
        {
            CapacityBytes = 64L << 30,
            SpeedMTs = i % 7 == 3 ? null : (i % 2 == 0 ? 4800 : 4400),
            Manufacturer = i % 3 == 0 ? "00AD063200AD" : i % 3 == 1 ? "Samsung" : null,
            PartNumber = i % 3 == 0 ? "HMCG94AEBRA109N        " : "M321R8GA0BB0-CQKZJ ",
            Slot = $"CPU{(i / 8) + 1}_DIMM_{(char)('A' + (i % 8))}{(i % 2) + 1}",
        }).ToList(),
        TotalPhysicalMemoryBytes = (long)(modules * (64L << 30) * 0.998),
        Gpus = extraGpus
            ?
            [
                new GpuInfo { Name = "NVIDIA RTX 6000 Ada Generation with ECC enabled (Workstation Display Driver)", Vendor = "NVIDIA", DedicatedMemoryBytes = 48L << 30, DriverVersion = "32.0.15.6094", DriverDate = DateTimeOffset.UtcNow.AddDays(-400), Status = "OK" },
                new GpuInfo { Name = "Parsec Virtual Display Adapter", Vendor = "Parsec Cloud, Inc.", DedicatedMemoryBytes = null, DriverVersion = "0.45.0.0", DriverDate = null, Status = "Degraded" },
                new GpuInfo { Name = "Microsoft Basic Display Adapter", Vendor = "(Standard display types)", DedicatedMemoryBytes = null, DriverVersion = null, DriverDate = null, Status = "Error" },
            ]
            : [],
        Warnings = ["Win32_PhysicalMemory: Acesso negado.", "VRAM (registro): O acesso à chave do Registro 'HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0002' foi negado."],
        CapturedAt = DateTimeOffset.UtcNow,
    };

    // ---------------- Contadores ----------------

    public static PerformanceReading Reading(DevDataMode mode) => mode switch
    {
        // GPU sem contadores, frequência indisponível, tudo null.
        DevDataMode.Empty => new PerformanceReading(),
        DevDataMode.One => new PerformanceReading
        {
            CpuPercent = 1,
            TotalPhysicalBytes = 1L << 30,
            AvailablePhysicalBytes = 10L << 20,
            CommitTotalBytes = 1L << 30,
            CommitLimitBytes = 1L << 30,
            ProcessorQueueLength = 1,
            PagesInputPerSecond = 1,
            PageFileUsagePercent = 1,
        },
        _ => new PerformanceReading
        {
            CpuPercent = 100,
            ProcessorPerformancePercent = 23.4,      // limitação severa
            ProcessorBaseFrequencyMHz = 2001,
            ProcessorQueueLength = 147,
            TotalPhysicalBytes = 1024L << 30,         // 1 TB
            AvailablePhysicalBytes = 300L << 20,
            CommitTotalBytes = (long)(2047.9 * (1L << 30)),
            CommitLimitBytes = 2048L << 30,
            SystemCacheBytes = 0,
            PagedPoolBytes = 12_345_678_901,
            NonPagedPoolBytes = 9_876_543_210,
            PagesInputPerSecond = 123_456.789,
            PageFileUsagePercent = 100,
            Gpus = [], // GPU sem contadores "GPU Engine" (VM / driver antigo)
        },
    };

    // ---------------- Processos ----------------

    private sealed record FakeProcess(int Pid, string Name, double CpuShare, long PrivateBytes, bool Protected = false);

    private static readonly FakeProcess[] WorstProcesses =
    [
        new(213456, "ServiceHub.RoslynCodeAnalysisService", 0.31, 31L << 30),
        new(4, "System", 0.12, 0, Protected: true),
        new(98123, "WindowsPackageManagerServer", 0.0049, 29_876_543_210),
        new(7, "MicrosoftEdgeWebView2RuntimeInstallerX64WithAVeryRealisticLongName_v130.0.2849.80", 0.08, 2_345_678_901),
        new(1048575, "Integração Contábil — Escritório São João", 0.07, 1_234_567_890),
        new(55, "Jo", 0.06, 1L << 30),
        new(31337, "vmmem", 0.05, 30L << 30),
        new(31338, "vmmem", 0.05, 30L << 30),
        new(1201, "msedgewebview2", 0.02, 800_000_000),
        new(1202, "msedgewebview2", 0.02, 800_000_000),
    ];

    public static ProcessSnapshot Processes(DevDataMode mode, int logicalProcessors)
    {
        var now = DateTimeOffset.UtcNow;
        var list = mode switch
        {
            DevDataMode.Empty => [],
            DevDataMode.One => [new FakeProcess(1, "a", 0.01, 1)],
            DevDataMode.Many => Enumerable.Range(0, 1000).Select(i => new FakeProcess(10_000 + i, $"worker-{i:0000}", 0.0005 * (i % 7), 50_000_000 + (i * 1_000_000L))).Concat(WorstProcesses).ToArray(),
            _ => WorstProcesses,
        };

        var elapsed = now - Epoch;
        return new ProcessSnapshot(now, list.Select(p => new ProcessSample
        {
            Pid = p.Pid,
            Name = p.Name,
            // Tempo de CPU cresce com o relógio na fração desejada da máquina: duas fotografias dão o uso esperado.
            TotalProcessorTime = p.Protected ? null : TimeSpan.FromMilliseconds(elapsed.TotalMilliseconds * logicalProcessors * p.CpuShare),
            StartTime = Epoch,
            PrivateBytes = p.PrivateBytes,
            WorkingSetBytes = p.PrivateBytes / 2,
            ThreadCount = 1,
            HandleCount = 1,
        }).ToList());
    }

    // ---------------- Histórico ----------------

    public static IReadOnlyList<SystemHistorySample> History(DevDataMode mode)
    {
        var now = DateTimeOffset.UtcNow;
        return mode switch
        {
            DevDataMode.Empty => [],
            DevDataMode.One => [new SystemHistorySample(now, 50, 50, 50)],
            // 30 min completos, com valores extremos e lacunas (computador suspenso).
            _ => Enumerable.Range(0, 360)
                .Where(i => i is < 150 or > 200)
                .Select(i => new SystemHistorySample(now.AddSeconds((i - 360) * 5), i % 40 < 20 ? 100 : 0, 99.9, null))
                .ToList(),
        };
    }
}
#endif
