using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public sealed class ScanHistoryItemViewModel
{
    public ScanHistoryItemViewModel(ScanSummary summary)
    {
        Summary = summary;
    }

    public ScanSummary Summary { get; }

    public Guid Id => Summary.Id;

    public string ModeText => Summary.Mode switch
    {
        ScanMode.Quick => "Varredura rápida",
        ScanMode.Full => "Varredura completa",
        _ => "Varredura personalizada",
    };

    public string DateText => Summary.StartedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);

    public string ScoreText => Summary.Score?.ToString(CultureInfo.CurrentCulture) ?? "—";

    public string RatingText => Display.Rating(Summary.Rating);

    public HealthRating Rating => Summary.Rating;

    public int ProblemCount => Summary.ProblemCount;

    public int RecommendationCount => Summary.RecommendationCount;

    public string ResultsText => $"{Summary.ProblemCount} problema(s) · {Summary.RecommendationCount} recomendação(ões)";

    public string DurationText
    {
        get
        {
            var dur = Summary.CompletedAt - Summary.StartedAt;
            return dur.TotalSeconds < 60 ? $"{dur.TotalSeconds:0} s" : $"{dur.TotalMinutes:0.#} min";
        }
    }

    public string StatusText => Summary.WasCancelled ? "Cancelada" : "Concluída";

    public string CoverageText => Display.Coverage(Summary.MeasuredAreas) ?? string.Empty;
}

public sealed partial class HistoryViewModel : ObservableObject, IPageLifecycle
{
    private readonly IScanRepository _repository;
    private readonly ILogger<HistoryViewModel> _logger;

    public HistoryViewModel(IScanRepository repository, UiPreferences preferences, ILogger<HistoryViewModel> logger)
    {
        _repository = repository;
        Preferences = preferences;
        _logger = logger;
    }

    public UiPreferences Preferences { get; }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ObservableCollection<ScanHistoryItemViewModel> _scans = [];

    [ObservableProperty]
    private ScanHistoryItemViewModel? _selectedScan;

    [ObservableProperty]
    private bool _viewingScanReport;

    [ObservableProperty]
    private IReadOnlyList<FindingItemViewModel> _problems = [];

    [ObservableProperty]
    private IReadOnlyList<FindingItemViewModel> _recommendations = [];

    [ObservableProperty]
    private string _emptyMessage = "Nenhuma varredura anterior encontrada.";

    public void Activate()
    {
        _ = LoadAsync();
    }

    public void Deactivate()
    {
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private async Task ViewReportAsync(ScanHistoryItemViewModel? item)
    {
        var target = item ?? SelectedScan;
        if (target is null)
        {
            return;
        }

        SelectedScan = target;
        IsLoading = true;
        try
        {
            var findings = await _repository.GetFindingsAsync(target.Id, CancellationToken.None).ConfigureAwait(true);
            Problems = findings.Where(f => !f.IsRecommendation).Select(f => new FindingItemViewModel(f)).ToList();
            Recommendations = findings.Where(f => f.IsRecommendation).Select(f => new FindingItemViewModel(f)).ToList();
            ViewingScanReport = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar detalhes da varredura {ScanId}", target.Id);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void BackToList()
    {
        ViewingScanReport = false;
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var summaries = await _repository.GetRecentAsync(100, CancellationToken.None).ConfigureAwait(true);
            Scans = new ObservableCollection<ScanHistoryItemViewModel>(summaries.Select(s => new ScanHistoryItemViewModel(s)));
            EmptyMessage = Scans.Count == 0 ? "Nenhuma varredura registrada no histórico local ainda." : string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar histórico de varreduras");
            EmptyMessage = $"Erro ao carregar histórico: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

