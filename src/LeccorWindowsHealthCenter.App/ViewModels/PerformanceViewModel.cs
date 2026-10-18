using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Diagnostics.Performance;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

/// <summary>
/// Linha de processo nas listas de maiores consumidores. O PID fica em coluna própria (nunca cortado: é o que
/// distingue dois processos de mesmo nome) e <see cref="ToolTip"/> mostra o nome completo e o caminho.
/// </summary>
public sealed record ProcessRow(string Name, string Value, string Pid, double Percent, string ToolTip);

/// <summary>
/// Performance (seções 11–13): leituras ao vivo de CPU, memória e GPU, histórico dos últimos 30 min
/// (enquanto o app está aberto) e maiores consumidores. Amostra só com a tela visível.
/// </summary>
public sealed partial class PerformanceViewModel : ObservableObject, IPageLifecycle
{
    private const int HistoryPoints = 360; // 30 min a cada 5 s

    private readonly IPerformanceCounterSessionFactory _counters;
    private readonly IProcessSnapshotProvider _processes;
    private readonly IMetricsHistory _history;
    private readonly ILogger<PerformanceViewModel> _logger;
    private readonly DispatcherTimer _timer;
    private IPerformanceCounterSession? _session;
    private ProcessSnapshot? _previousSnapshot;
    private bool _busy;
    private int _tick;

    public PerformanceViewModel(
        IPerformanceCounterSessionFactory counters,
        IProcessSnapshotProvider processes,
        IMetricsHistory history,
        ILogger<PerformanceViewModel> logger)
    {
        _counters = counters;
        _processes = processes;
        _history = history;
        _logger = logger;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        LogicalProcessors = Environment.ProcessorCount;
    }

    public int LogicalProcessors { get; }

    // Leitura grande: "—" quando ausente (cabe em qualquer largura); a legenda "não disponível" aparece abaixo.
    [ObservableProperty] private string _cpuText = "—";
    [ObservableProperty] private bool _cpuUnavailable;
    [ObservableProperty] private IReadOnlyList<InfoRow> _cpuRows = [];
    [ObservableProperty] private IReadOnlyList<double> _cpuHistory = [];
    [ObservableProperty] private IReadOnlyList<DateTimeOffset> _cpuHistoryTimes = [];

    [ObservableProperty] private string _memoryText = "—";
    [ObservableProperty] private bool _memoryUnavailable;
    [ObservableProperty] private string _memoryDetail = string.Empty;
    [ObservableProperty] private IReadOnlyList<InfoRow> _memoryRows = [];
    [ObservableProperty] private IReadOnlyList<double> _memoryHistory = [];
    [ObservableProperty] private IReadOnlyList<DateTimeOffset> _memoryHistoryTimes = [];

    [ObservableProperty] private string _gpuText = "—";
    [ObservableProperty] private bool _gpuUnavailable;
    [ObservableProperty] private string _gpuDetail = string.Empty;
    [ObservableProperty] private IReadOnlyList<InfoRow> _gpuRows = [];

    [ObservableProperty] private string _historyCaption = "Histórico em formação";
    [ObservableProperty] private IReadOnlyList<ProcessRow> _topCpu = [];
    [ObservableProperty] private IReadOnlyList<ProcessRow> _topMemory = [];
    [ObservableProperty] private string _topCpuEmptyText = "Medindo — o uso de CPU é calculado entre duas leituras.";
    [ObservableProperty] private string _topMemoryEmptyText = "Medindo…";
    [ObservableProperty] private string _processNote = "Medindo processos…";

    public void Activate()
    {
        _session ??= _counters.Create();
        _timer.Start();
        _ = RefreshAsync();
    }

    public void Deactivate()
    {
        _timer.Stop();
        _session?.Dispose();
        _session = null;
        _previousSnapshot = null;
    }

    private async Task RefreshAsync()
    {
        if (_busy || _session is not { } session)
        {
            return;
        }

        _busy = true;
        try
        {
            var takeSnapshot = _tick++ % 2 == 0; // processos a cada 4 s: a lista completa custa dezenas de ms
            var (reading, snapshot) = await Task.Run(() => (session.Read(), takeSnapshot ? _processes.Capture() : null));
            Apply(reading);
            ApplyHistory();
            if (snapshot is not null)
            {
                ApplyProcesses(snapshot);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao atualizar a tela de Performance");
        }
        finally
        {
            _busy = false;
        }
    }

    private void Apply(PerformanceReading r)
    {
        var culture = CultureInfo.CurrentCulture;

        CpuText = Fmt.Reading(r.CpuPercent);
        CpuUnavailable = r.CpuPercent is null;
        CpuRows =
        [
            new("Frequência efetiva", Fmt.GHz(r.EffectiveFrequencyMHz)),
            new("Desempenho", r.ProcessorPerformancePercent is { } perf && r.ProcessorBaseFrequencyMHz is { } nominal
                ? string.Create(culture, $"{perf:0}% da nominal ({Fmt.GHz(nominal)})")
                : Fmt.NotAvailable),
            new("Fila do processador", Fmt.Count(r.ProcessorQueueLength, "thread aguardando", "threads aguardando")),
            new("Processadores lógicos", LogicalProcessors.ToString(culture)),
        ];

        MemoryText = Fmt.Reading(r.MemoryUsedPercent);
        MemoryUnavailable = r.MemoryUsedPercent is null;
        MemoryDetail = r.TotalPhysicalBytes is { } total && r.AvailablePhysicalBytes is { } available
            ? $"{Fmt.BytesOf(total - available, total)} em uso"
            : string.Empty;
        MemoryRows =
        [
            new("Comprometida (commit)", r.CommitTotalBytes is not null && r.CommitLimitBytes is not null
                ? $"{Fmt.BytesOf(r.CommitTotalBytes, r.CommitLimitBytes)} ({Fmt.Percent(r.CommitPercent)})"
                : Fmt.NotAvailable),
            new("Cache do sistema", Fmt.Bytes(r.SystemCacheBytes)),
            new("Pool paginado / não paginado", r.PagedPoolBytes is null && r.NonPagedPoolBytes is null
                ? Fmt.NotAvailable
                : $"{Fmt.Bytes(r.PagedPoolBytes)} / {Fmt.Bytes(r.NonPagedPoolBytes)}"),
            new("Arquivo de paginação em uso", Fmt.Percent(r.PageFileUsagePercent)),
            new("Leituras de página do disco", Fmt.Count(r.PagesInputPerSecond is { } p ? Math.Round(p) : null, "página/s", "páginas/s")),
        ];

        var busiest = r.Gpus.OrderByDescending(g => g.UtilizationPercent).FirstOrDefault();
        GpuText = Fmt.Reading(r.GpuPercent);
        GpuUnavailable = busiest is null;
        if (busiest is null)
        {
            // Sem contadores: só a explicação — linhas "não disponível" repetiriam a mesma informação.
            GpuDetail = "Esta máquina não expõe os contadores de GPU do Windows (comum em máquinas virtuais e drivers antigos).";
            GpuRows = [];
            return;
        }

        GpuDetail = "Motor mais ocupado do adaptador mais ocupado";
        var engines = busiest.EngineUtilization
            .Where(e => e.Value >= 0.5)
            .OrderByDescending(e => e.Value)
            .Take(4)
            .Select(e => new InfoRow(EngineName(e.Key), string.Create(culture, $"{e.Value:0}%")))
            .ToList();
        var vram = r.Gpus.Where(g => g.DedicatedUsageBytes is not null).MaxBy(g => g.DedicatedUsageBytes);
        GpuRows =
        [
            .. engines.Count > 0 ? engines : [new InfoRow("Motores", "todos ociosos")],
            new("VRAM dedicada em uso", Fmt.Bytes(vram?.DedicatedUsageBytes)),
            new("Adaptadores com contadores", r.Gpus.Count.ToString(culture)),
        ];
    }

    private void ApplyHistory()
    {
        var samples = _history.SystemSamples();
        var cpu = samples.Where(s => s.CpuPercent is not null).TakeLast(HistoryPoints).ToArray();
        var memory = samples.Where(s => s.MemoryUsedPercent is not null).TakeLast(HistoryPoints).ToArray();
        CpuHistory = cpu.Select(s => s.CpuPercent!.Value).ToArray();
        CpuHistoryTimes = cpu.Select(s => s.Timestamp).ToArray();
        MemoryHistory = memory.Select(s => s.MemoryUsedPercent!.Value).ToArray();
        MemoryHistoryTimes = memory.Select(s => s.Timestamp).ToArray();
        HistoryCaption = samples.Count < 2
            ? "Histórico em formação (amostras a cada 5 s)"
            : $"Últimos {Math.Max(1, (int)(samples[^1].Timestamp - samples[0].Timestamp).TotalMinutes)} min";
    }

    private void ApplyProcesses(ProcessSnapshot snapshot)
    {
        var culture = CultureInfo.CurrentCulture;
        if (_previousSnapshot is { } previous)
        {
            TopCpu = ProcessUsageCalculator.TopByCpu(previous, snapshot, LogicalProcessors, 8)
                .Select(p => new ProcessRow(p.Name, CpuShare(p.CpuPercent), string.Create(culture, $"PID {p.Pid}"), p.CpuPercent, ToolTipFor(p.Name, p.Pid)))
                .ToList();
            TopCpuEmptyText = "Nenhum processo com uso de CPU mensurável entre as duas últimas leituras.";
        }

        var top = ProcessUsageCalculator.TopByPrivateMemory(snapshot, 8);
        var maxPrivate = top.Count == 0 ? 1 : top[0].PrivateBytes!.Value;
        TopMemory = top
            .Select(p => new ProcessRow(p.Name, Fmt.Bytes(p.PrivateBytes), string.Create(culture, $"PID {p.Pid}"), p.PrivateBytes!.Value * 100.0 / maxPrivate, ToolTipFor(p.Name, p.Pid)))
            .ToList();
        TopMemoryEmptyText = "Nenhum processo com memória privada legível.";

        var protectedCount = snapshot.Processes.Count(p => p.TotalProcessorTime is null);
        ProcessNote = protectedCount switch
        {
            0 => "Uso de CPU medido entre leituras de 4 s; memória privada no instante.",
            1 => "Uso de CPU medido entre leituras de 4 s. 1 processo protegido do sistema não expõe o tempo de CPU sem administrador.",
            _ => string.Create(culture, $"Uso de CPU medido entre leituras de 4 s. {protectedCount} processos protegidos do sistema não expõem o tempo de CPU sem administrador."),
        };
        _previousSnapshot = snapshot;
    }

    /// <summary>Abaixo de 0,5% "0%" seria falso (o processo usou CPU); mostra "&lt; 1%".</summary>
    private static string CpuShare(double percent) => percent < 0.5 ? "< 1%" : Fmt.Percent(percent);

    private string ToolTipFor(string name, int pid)
    {
        var path = _processes.TryGetExecutablePath(pid);
        return path is null ? $"{name} (PID {pid})" : $"{name} (PID {pid}){Environment.NewLine}{path}";
    }

    private static string EngineName(string engine) => engine switch
    {
        "3D" => "3D",
        "Copy" => "Cópia",
        "VideoDecode" => "Decodificação de vídeo",
        "VideoEncode" => "Codificação de vídeo",
        "VideoProcessing" => "Processamento de vídeo",
        _ when engine.StartsWith("Compute", StringComparison.OrdinalIgnoreCase) => "Computação",
        _ => engine,
    };
}
