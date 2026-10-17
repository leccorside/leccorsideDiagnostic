using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

/// <summary>Varredura rápida/completa/personalizada com progresso por scanner e cancelamento (seções 9, 44, 52 e 53).</summary>
public sealed partial class ScanViewModel : ObservableObject
{
    private readonly IScanOrchestrator _orchestrator;
    private readonly ScanSession _session;
    private readonly ILogger<ScanViewModel> _logger;
    private CancellationTokenSource? _cts;

    public ScanViewModel(IScanOrchestrator orchestrator, ScanSession session, UiPreferences preferences, ILogger<ScanViewModel> logger)
    {
        _orchestrator = orchestrator;
        _session = session;
        _logger = logger;
        Preferences = preferences;
        Modules = new ObservableCollection<ModuleOption>(Enum.GetValues<ScanModule>().Select(m => new ModuleOption(m, Display.Module(m))));
        AvailableScannerCount = orchestrator.Scanners.Count;
    }

    public UiPreferences Preferences { get; }

    public ObservableCollection<ModuleOption> Modules { get; }

    public ObservableCollection<ScannerStateItem> Scanners { get; } = [];

    public int AvailableScannerCount { get; }

    public bool NoScannersInstalled => AvailableScannerCount == 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QuickScanCommand), nameof(FullScanCommand), nameof(CustomScanCommand), nameof(CancelCommand))]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanTitle = string.Empty;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string _resultTitle = string.Empty;

    [ObservableProperty]
    private string _scoreText = "—";

    [ObservableProperty]
    private string _ratingText = string.Empty;

    [ObservableProperty]
    private HealthRating _rating;

    [ObservableProperty]
    private string _countsText = string.Empty;

    [ObservableProperty]
    private string _resultNote = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<FindingItemViewModel> _problems = [];

    [ObservableProperty]
    private IReadOnlyList<FindingItemViewModel> _recommendations = [];

    [ObservableProperty]
    private IReadOnlyList<string> _scannerErrors = [];

    public Task StartAsync(ScanPlan plan) => IsScanning ? Task.CompletedTask : RunAsync(plan);

    private bool CanStart() => !IsScanning;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task QuickScan() => RunAsync(ScanPlan.Quick);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task FullScan() => RunAsync(ScanPlan.Full);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task CustomScan()
    {
        var selected = Modules.Where(m => m.IsSelected).Select(m => m.Module).ToList();
        return RunAsync(ScanPlan.Custom(selected));
    }

    [RelayCommand(CanExecute = nameof(IsScanning))]
    private void Cancel() => _cts?.Cancel();

    private async Task RunAsync(ScanPlan plan)
    {
        IsScanning = true;
        HasResult = false;
        ProgressPercent = 0;
        ScanTitle = plan.Mode switch
        {
            ScanMode.Quick => "Varredura rápida",
            ScanMode.Full => "Varredura completa",
            _ => "Varredura personalizada",
        };
        Scanners.Clear();

        using var cts = new CancellationTokenSource();
        _cts = cts;
        // Progress<T> criado na thread da UI: os reports chegam já no Dispatcher.
        var progress = new Progress<ScanProgress>(UpdateProgress);

        try
        {
            var report = await _orchestrator.RunAsync(plan, progress, cts.Token);
            ShowResult(report);
            _session.Publish(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada na varredura");
            HasResult = true;
            ResultTitle = "A varredura não pôde ser concluída";
            ResultNote = ex.Message;
        }
        finally
        {
            _cts = null;
            IsScanning = false;
        }
    }

    private void UpdateProgress(ScanProgress progress)
    {
        ProgressPercent = progress.Percent;
        Scanners.Clear();
        foreach (var scanner in progress.Scanners)
        {
            Scanners.Add(new ScannerStateItem(scanner.ScannerName, Display.StatusGlyph(scanner.Status), scanner.Status));
        }
    }

    private void ShowResult(ScanReport report)
    {
        HasResult = true;
        ResultTitle = report.WasCancelled ? "Varredura cancelada — resultado parcial" : "Varredura concluída";
        Rating = report.Score.Rating;
        ScoreText = report.Score.Overall?.ToString(CultureInfo.CurrentCulture) ?? "—";
        RatingText = Display.Rating(report.Score.Rating);

        var findings = report.Findings.ToList();
        CountsText = string.Create(CultureInfo.CurrentCulture,
            $"{report.CountBySeverity(Severity.Critical)} crítico · {report.CountBySeverity(Severity.High)} alto · {report.CountBySeverity(Severity.Medium)} médio · {report.CountBySeverity(Severity.Low)} baixo · {report.CountBySeverity(Severity.Info)} recomendação");

        Problems = report.Problems.Select(f => new FindingItemViewModel(f)).ToList();
        Recommendations = report.Recommendations.Select(f => new FindingItemViewModel(f)).ToList();
        ScannerErrors = report.Results
            .Where(r => r.Status == ScannerRunStatus.Failed)
            .Select(r => $"{r.ScannerName}: {r.Error}")
            .ToList();

        ResultNote = report.Results.Count == 0
            ? "Nenhum scanner de diagnóstico foi executado. Os scanners do MVP 1 serão adicionados nos próximos passos — por isso não há score: sem medição, não há nota."
            : findings.Count == 0
                ? "Nenhum problema encontrado pelos scanners executados."
                : $"Encontramos {report.ConfirmedProblemCount} problema(s) confirmado(s) e {report.Recommendations.Count} recomendação(ões).";
    }
}

public sealed partial class ModuleOption : ObservableObject
{
    public ModuleOption(ScanModule module, string label)
    {
        Module = module;
        Label = label;
    }

    public ScanModule Module { get; }

    public string Label { get; }

    [ObservableProperty]
    private bool _isSelected = true;
}

public sealed record ScannerStateItem(string Name, string Glyph, ScannerRunStatus Status)
{
    /// <summary>Nome acessível do ícone de status (leitores de tela não leem glifos de fonte de ícones).</summary>
    public string StatusText => Display.Status(Status);
}
