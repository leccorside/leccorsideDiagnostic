using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.Settings;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Shared.Formatting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.App.ViewModels;

/// <summary>Tela inicial (seção 7): score, uso atual de recursos e resumo da última varredura.</summary>
public sealed partial class DashboardViewModel : ObservableObject, IPageLifecycle
{
    private const int HistoryLength = 60;
    private const string NotAvailable = "não disponível";

    private readonly ILiveMetricsProvider _metrics;
    private readonly IScanRepository _repository;
    private readonly ScanSession _session;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly DispatcherTimer _timer;
    private readonly Queue<double> _cpuHistory = new();
    private readonly Queue<double> _ramHistory = new();

    public DashboardViewModel(
        ILiveMetricsProvider metrics,
        IScanRepository repository,
        ScanSession session,
        IOptions<HealthCenterOptions> options,
        ILogger<DashboardViewModel> logger)
    {
        _metrics = metrics;
        _repository = repository;
        _session = session;
        _logger = logger;

        var interval = options.Value.DashboardRefreshInterval;
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = interval < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : interval,
        };
        _timer.Tick += (_, _) => RefreshMetrics();
        _session.ReportChanged += (_, _) => ApplyReport(_session.LastReport!);
    }

    /// <summary>Disparado quando o usuário pede uma varredura a partir do dashboard.</summary>
    public event EventHandler<ScanPlan>? ScanRequested;

    [ObservableProperty]
    private string _scoreText = "—";

    [ObservableProperty]
    private string _ratingText = Display.Rating(HealthRating.Unknown);

    [ObservableProperty]
    private HealthRating _rating = HealthRating.Unknown;

    [ObservableProperty]
    private string _scoreSubtitle = "Nenhuma varredura realizada ainda. O score só é calculado a partir de medições reais.";

    [ObservableProperty]
    private bool _hasScanResult;

    /// <summary>Quais áreas o score avaliou — um 100 sobre 2 de 6 áreas não é "computador perfeito".</summary>
    [ObservableProperty]
    private string _coverageText = string.Empty;

    [ObservableProperty]
    private string _cpuText = "…";

    [ObservableProperty]
    private double _cpuPercent;

    [ObservableProperty]
    private string _ramText = "…";

    [ObservableProperty]
    private double _ramPercent;

    [ObservableProperty]
    private string _ramDetail = string.Empty;

    [ObservableProperty]
    private string _diskLabel = "Disco C:";

    [ObservableProperty]
    private string _diskText = "…";

    [ObservableProperty]
    private double _diskPercent;

    [ObservableProperty]
    private string _gpuText = NotAvailable;

    [ObservableProperty]
    private double _gpuPercent;

    [ObservableProperty]
    private string _gpuCaption = "Lendo contadores de GPU…";

    [ObservableProperty]
    private IReadOnlyList<double> _cpuHistoryValues = [];

    [ObservableProperty]
    private string _cpuHistoryCaption = "Medindo…";

    [ObservableProperty]
    private IReadOnlyList<double> _ramHistoryValues = [];

    [ObservableProperty]
    private int _criticalCount;

    [ObservableProperty]
    private int _importantCount;

    [ObservableProperty]
    private int _minorCount;

    [ObservableProperty]
    private int _recommendationCount;

    [ObservableProperty]
    private string _findingsSummary = string.Empty;

    [ObservableProperty]
    private FindingItemViewModel? _topProblem;

    public void Activate()
    {
        RefreshMetrics();
        _timer.Start();
    }

    public void Deactivate() => _timer.Stop();

    public async Task LoadLastScanAsync()
    {
        if (_session.LastReport is not null)
        {
            return;
        }

        try
        {
            var recent = await _repository.GetRecentAsync(1, CancellationToken.None);
            if (recent.Count == 0)
            {
                return;
            }

            var last = recent[0];
            var findings = await _repository.GetFindingsAsync(last.Id, CancellationToken.None);
            ApplySummary(last.Score, last.Rating, last.CompletedAt, findings, last.WasCancelled, last.MeasuredAreas);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível carregar a última varredura do histórico");
        }
    }

    [RelayCommand]
    private void StartQuickScan() => ScanRequested?.Invoke(this, ScanPlan.Quick);

    [RelayCommand]
    private void StartFullScan() => ScanRequested?.Invoke(this, ScanPlan.Full);

    private void ApplyReport(ScanReport report) =>
        ApplySummary(report.Score.Overall, report.Score.Rating, report.CompletedAt, report.Findings.ToList(), report.WasCancelled, report.Score.Categories.Keys);

    private void ApplySummary(int? score, HealthRating rating, DateTimeOffset completedAt, IReadOnlyList<Finding> findings, bool cancelled, IEnumerable<ScoreCategory> measuredAreas)
    {
        HasScanResult = true;
        CoverageText = Display.Coverage(measuredAreas) ?? string.Empty;
        Rating = rating;
        ScoreText = score?.ToString(CultureInfo.CurrentCulture) ?? "—";
        RatingText = Display.Rating(rating);
        var when = completedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        ScoreSubtitle = score is null
            ? $"Varredura de {when} sem dados suficientes para calcular o score."
            : $"Última varredura: {when}{(cancelled ? " (cancelada — resultado parcial)" : string.Empty)}";

        CriticalCount = findings.Count(f => f.Severity == Severity.Critical);
        ImportantCount = findings.Count(f => f.Severity is Severity.High or Severity.Medium);
        MinorCount = findings.Count(f => f.Severity == Severity.Low);
        RecommendationCount = findings.Count(f => f.IsRecommendation);

        var confirmed = findings.Count(f => !f.IsRecommendation && f.Confidence.IsConfirmed);
        var possible = findings.Count(f => !f.IsRecommendation && !f.Confidence.IsConfirmed);
        FindingsSummary = $"Encontramos {confirmed} problema(s) confirmado(s), {possible} possível(is) problema(s) e {RecommendationCount} recomendação(ões).";

        var top = findings.Where(f => !f.IsRecommendation)
            .OrderByDescending(f => f.Severity)
            .ThenByDescending(f => f.Confidence.Percent)
            .FirstOrDefault();
        TopProblem = top is null ? null : new FindingItemViewModel(top);
    }

    private void RefreshMetrics()
    {
        LiveSystemMetrics sample;
        try
        {
            sample = _metrics.Sample();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao amostrar métricas do dashboard");
            return;
        }

        CpuPercent = sample.CpuPercent ?? 0;
        CpuText = Percent(sample.CpuPercent);
        RamPercent = sample.MemoryPercent ?? 0;
        RamText = Percent(sample.MemoryPercent);
        RamDetail = sample.MemoryTotalBytes is { } total && sample.MemoryAvailableBytes is { } available
            ? $"{Fmt.Bytes(total - available)} de {Fmt.Bytes(total)}"
            : string.Empty;
        DiskLabel = $"Disco {sample.SystemDrive}";
        DiskPercent = sample.SystemDrivePercent ?? 0;
        DiskText = Percent(sample.SystemDrivePercent);
        GpuText = Percent(sample.GpuPercent);
        GpuPercent = sample.GpuPercent ?? 0;
        GpuCaption = sample.GpuPercent is null
            ? "Esta máquina não expõe os contadores de GPU do Windows. Não exibimos valores estimados."
            : "Motor mais ocupado (3D, cópia ou vídeo)";

        if (sample.CpuPercent is { } cpu)
        {
            CpuHistoryValues = Push(_cpuHistory, cpu);
            var span = TimeSpan.FromTicks(_timer.Interval.Ticks * Math.Max(0, _cpuHistory.Count - 1));
            CpuHistoryCaption = span.TotalMinutes >= 1
                ? string.Create(CultureInfo.CurrentCulture, $"Últimos {span.TotalMinutes:0} min")
                : string.Create(CultureInfo.CurrentCulture, $"Últimos {span.TotalSeconds:0} s");
        }

        if (sample.MemoryPercent is { } ram)
        {
            RamHistoryValues = Push(_ramHistory, ram);
        }
    }

    private static double[] Push(Queue<double> queue, double value)
    {
        queue.Enqueue(value);
        while (queue.Count > HistoryLength)
        {
            queue.Dequeue();
        }

        return queue.ToArray();
    }

    private static string Percent(double? value) =>
        value is null ? NotAvailable : string.Create(CultureInfo.CurrentCulture, $"{value:0}%");
}
