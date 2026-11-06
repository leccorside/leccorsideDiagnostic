using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Events;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public sealed class EventGroupItemViewModel
{
    public EventGroupItemViewModel(EventGroupSummary group)
    {
        Group = group;
    }

    public EventGroupSummary Group { get; }

    public string LogName => Group.LogName;

    public string ProviderName => Group.ProviderName;

    public int EventId => Group.EventId;

    public string Level => Group.Level;

    public int Count24h => Group.Count24h;

    public string CountText => Fmt.Count(Group.Count24h, "ocorrência", "ocorrências");

    public DateTimeOffset FirstSeen => Group.FirstSeen;

    public DateTimeOffset LastSeen => Group.LastSeen;

    public string LastSeenText => Group.LastSeen.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);

    public string Message => Group.Message;

    public string? TargetResource => Group.TargetResource;

    public string HeaderText => $"{Group.ProviderName} (Evento {Group.EventId})";

    public string SubtitleText => $"{Group.LogName} · {CountText} nas últimas 24h · Último às {LastSeenText}";

    public string SeverityTone => Group.Level switch
    {
        "Crítico" => "Critical",
        "Erro" => "Critical",
        "Aviso" => "Warning",
        _ => "Neutral",
    };

    public string Details => string.IsNullOrWhiteSpace(Group.TechnicalDetails) ? Group.Message : Group.TechnicalDetails;
}

public sealed partial class EventsViewModel : ObservableObject, IPageLifecycle
{
    private readonly IEventViewerProvider _provider;
    private readonly ILogger<EventsViewModel> _logger;
    private List<EventGroupItemViewModel> _allItems = [];
    private bool _hasLoaded;

    public EventsViewModel(IEventViewerProvider provider, UiPreferences preferences, ILogger<EventsViewModel> logger)
    {
        _provider = provider;
        Preferences = preferences;
        _logger = logger;

        LevelFilters = ["Todos", "Críticos e Erros", "Avisos"];
        _selectedLevelFilter = LevelFilters[0];
    }

    public UiPreferences Preferences { get; }

    public IReadOnlyList<string> LevelFilters { get; }

    [ObservableProperty]
    private string _selectedLevelFilter;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ObservableCollection<EventGroupItemViewModel> _filteredItems = [];

    [ObservableProperty]
    private EventGroupItemViewModel? _selectedItem;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private int _criticalCount;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    public void Activate()
    {
        if (!_hasLoaded)
        {
            _ = LoadEventsAsync();
        }
    }

    public void Deactivate()
    {
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedLevelFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    private Task RefreshAsync() => LoadEventsAsync();

    [RelayCommand]
    private static void OpenEventViewer()
    {
        try
        {
            Process.Start(new ProcessStartInfo("eventvwr.msc") { UseShellExecute = true });
        }
        catch
        {
            // Silencioso se não conseguir abrir o Visualizador do Windows
        }
    }

    [RelayCommand]
    private void CopyDetails()
    {
        if (SelectedItem is null)
        {
            return;
        }

        try
        {
            var text = $"Log: {SelectedItem.LogName}\nOrigem: {SelectedItem.ProviderName}\nEvento: {SelectedItem.EventId}\nNível: {SelectedItem.Level}\nOcorrências 24h: {SelectedItem.Count24h}\nÚltimo: {SelectedItem.LastSeenText}\nMensagem: {SelectedItem.Message}\nDetalhes:\n{SelectedItem.Details}";
            Clipboard.SetText(text);
        }
        catch
        {
            // Falha ao acessar clipboard
        }
    }

    private async Task LoadEventsAsync()
    {
        IsLoading = true;
        try
        {
            var groups = await _provider.GetRecentEventGroupsAsync(TimeSpan.FromHours(24), CancellationToken.None).ConfigureAwait(true);
            _allItems = groups.Select(g => new EventGroupItemViewModel(g)).ToList();

            CriticalCount = groups.Where(g => string.Equals(g.Level, "Crítico", StringComparison.OrdinalIgnoreCase)).Sum(g => g.Count24h);
            ErrorCount = groups.Where(g => string.Equals(g.Level, "Erro", StringComparison.OrdinalIgnoreCase)).Sum(g => g.Count24h);
            WarningCount = groups.Where(g => string.Equals(g.Level, "Aviso", StringComparison.OrdinalIgnoreCase)).Sum(g => g.Count24h);

            SummaryText = $"{_allItems.Count} grupo(s) de eventos únicos analisados nas últimas 24 horas ({CriticalCount} críticos, {ErrorCount} erros, {WarningCount} avisos).";
            _hasLoaded = true;

            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar eventos do Event Viewer");
            SummaryText = $"Não foi possível ler os eventos: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var query = _allItems.AsEnumerable();

        if (SelectedLevelFilter == "Críticos e Erros")
        {
            query = query.Where(i => i.Level is "Crítico" or "Erro");
        }
        else if (SelectedLevelFilter == "Avisos")
        {
            query = query.Where(i => i.Level == "Aviso");
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            query = query.Where(i =>
                i.ProviderName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.LogName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.EventId.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (i.TargetResource?.Contains(search, StringComparison.OrdinalIgnoreCase) == true));
        }

        var list = query.ToList();
        FilteredItems = new ObservableCollection<EventGroupItemViewModel>(list);

        if (SelectedItem is null || !list.Contains(SelectedItem))
        {
            SelectedItem = list.FirstOrDefault();
        }
    }
}
