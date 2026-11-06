using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public sealed partial class AvailableRepairItemViewModel : ObservableObject
{
    private readonly Action _onSelectionChanged;
    private bool _isSelected = true;

    public AvailableRepairItemViewModel(IRepairAction action, Action onSelectionChanged)
    {
        Action = action;
        _onSelectionChanged = onSelectionChanged;
    }

    public IRepairAction Action { get; }

    public string Id => Action.Id;

    public string Title => Action.Title;

    public string Description => Action.Description;

    public RiskLevel Risk => Action.Risk;

    public string RiskBadge => Action.Risk switch
    {
        RiskLevel.Safe => "Segura",
        RiskLevel.Low => "Risco Baixo",
        RiskLevel.Medium => "Risco Médio",
        RiskLevel.High => "Risco Alto",
        _ => "Manual"
    };

    public bool RequiresElevation => Action.RequiresElevation;

    public bool SupportsRollback => Action.SupportsRollback;

    public bool IsMeasurable => Action is IMeasurableRepairAction;

    public string MeasurementInfo => Action is IMeasurableRepairAction measurable
        ? $"Mede antes e depois: {measurable.MeasurementName} ({measurable.MeasurementUnit})"
        : "Sem medição numérica direta";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _onSelectionChanged();
            }
        }
    }

    [ObservableProperty]
    private string? _dryRunResult;
}

public sealed class RepairHistoryItemViewModel
{
    public RepairHistoryItemViewModel(RepairRecord record)
    {
        Record = record;
    }

    public RepairRecord Record { get; }

    public Guid Id => Record.Id;

    public string ActionTitle => Record.ActionTitle;

    public string DateText => Record.StartedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);

    public string OutcomeText => Record.RolledBackAt is not null
        ? "Desfeita (Rollback)"
        : Record.Outcome switch
        {
            RepairOutcome.Succeeded => "Sucesso",
            RepairOutcome.DryRun => "Simulação",
            RepairOutcome.Failed => "Falha",
            RepairOutcome.ValidationFailed => "Validação Falhou",
            RepairOutcome.Blocked => "Bloqueada",
            RepairOutcome.Cancelled => "Cancelada",
            RepairOutcome.RolledBack => "Desfeita",
            _ => "Desconhecido"
        };

    public string ReasonText => !string.IsNullOrWhiteSpace(Record.Reason) ? Record.Reason : "Correção manual executada pelo usuário";

    public string Message => Record.Message;

    public bool CanRollback => Record.CanRollback;

    public string? ComparisonText
    {
        get
        {
            if (Record.Comparison is null)
            {
                return null;
            }

            var c = Record.Comparison;
            var pctText = c.ImprovementPercent.HasValue ? $" ({c.ImprovementPercent.Value:+#0.0;-#0.0;0.0}% de melhoria)" : string.Empty;
            return $"{c.Name}: Antes {c.Before:F1} {c.Unit} → Depois {c.After:F1} {c.Unit}{pctText}";
        }
    }
}

public sealed partial class RepairsViewModel : ObservableObject, IPageLifecycle
{
    private readonly IRepairEngine _repairEngine;
    private readonly IRestorePointService _restorePointService;
    private readonly IRepairHistoryRepository _historyRepository;
    private readonly ILogger<RepairsViewModel> _logger;

    public ObservableCollection<AvailableRepairItemViewModel> AvailableRepairs { get; } = [];

    public ObservableCollection<RepairHistoryItemViewModel> HistoryRecords { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string? _operationFeedback;

    [ObservableProperty]
    private RepairHistoryItemViewModel? _selectedHistoryRecord;

    public RepairsViewModel(
        IRepairEngine repairEngine,
        IRestorePointService restorePointService,
        IRepairHistoryRepository historyRepository,
        ILogger<RepairsViewModel> logger)
    {
        _repairEngine = repairEngine;
        _restorePointService = restorePointService;
        _historyRepository = historyRepository;
        _logger = logger;

        LoadActions();
    }

    public void Activate()
    {
        _ = RefreshDataAsync();
    }

    public void Deactivate()
    {
    }

    private void LoadActions()
    {
        AvailableRepairs.Clear();
        foreach (var action in _repairEngine.Actions)
        {
            AvailableRepairs.Add(new AvailableRepairItemViewModel(action, UpdateSelectedCount));
        }
        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = AvailableRepairs.Count(a => a.IsSelected);
    }

    [RelayCommand]
    public async Task RefreshDataAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Carregando histórico e correções...";
        try
        {
            await LoadHistoryCoreAsync().ConfigureAwait(true);
            StatusMessage = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadHistoryCoreAsync()
    {
        try
        {
            var records = await _historyRepository.ListAsync(50, CancellationToken.None).ConfigureAwait(true);
            HistoryRecords.Clear();
            foreach (var record in records)
            {
                HistoryRecords.Add(new RepairHistoryItemViewModel(record));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar histórico de correções");
            StatusMessage = "Erro ao carregar histórico de correções.";
        }
    }

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var item in AvailableRepairs)
        {
            item.IsSelected = true;
        }
        UpdateSelectedCount();
    }

    [RelayCommand]
    public void DeselectAll()
    {
        foreach (var item in AvailableRepairs)
        {
            item.IsSelected = false;
        }
        UpdateSelectedCount();
    }

    [RelayCommand]
    public async Task CreateRestorePointAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Criando Ponto de Restauração nativo do Windows...";
        OperationFeedback = null;

        try
        {
            var desc = $"Leccor Health Center - Checkpoint {DateTime.Now:dd/MM/yyyy HH:mm}";
            var result = await _restorePointService.CreateRestorePointAsync(desc, CancellationToken.None).ConfigureAwait(true);

            OperationFeedback = result.Message;
            if (result.Success)
            {
                _logger.LogInformation("Ponto de restauração criado via UI: {Desc}", desc);
            }
            else
            {
                _logger.LogWarning("Não foi possível criar ponto de restauração via UI: {Msg}", result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar ponto de restauração");
            OperationFeedback = $"Falha ao criar ponto de restauração: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            StatusMessage = null;
        }
    }

    [RelayCommand]
    public async Task RunDryRunAsync()
    {
        var selected = AvailableRepairs.Where(a => a.IsSelected).ToList();
        if (selected.Count == 0 || IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Simulando correções selecionadas (Dry Run)...";
        OperationFeedback = null;

        try
        {
            var results = new List<string>();
            foreach (var item in selected)
            {
                var request = new RepairRequest
                {
                    ActionId = item.Action.Id,
                    DryRun = true,
                    Approval = UserApproval.Confirmed,
                    Reason = "Simulação solicitada pelo usuário"
                };

                var exec = await _repairEngine.ExecuteAsync(request, CancellationToken.None).ConfigureAwait(true);
                var changeCount = exec.Changes.Count;
                var details = $"{item.Title}: {changeCount} alteração(ões) planejada(s). {exec.Message}";
                item.DryRunResult = details;
                results.Add(details);
            }

            OperationFeedback = $"Simulação concluída ({selected.Count} correções analisadas):\n" + string.Join("\n", results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante simulação de correções");
            OperationFeedback = $"Erro durante a simulação: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            StatusMessage = null;
        }
    }

    [RelayCommand]
    public async Task ApplySelectedRepairsAsync()
    {
        var selected = AvailableRepairs.Where(a => a.IsSelected).ToList();
        if (selected.Count == 0 || IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = $"Executando {selected.Count} correção(ões) com backup prévio...";
        OperationFeedback = null;

        try
        {
            var executedCount = 0;
            var failedCount = 0;
            var summaryList = new List<string>();

            foreach (var item in selected)
            {
                var approval = item.Risk == RiskLevel.High ? UserApproval.ExplicitlyConfirmed : UserApproval.Confirmed;
                var request = new RepairRequest
                {
                    ActionId = item.Action.Id,
                    Approval = approval,
                    DryRun = false,
                    Reason = $"Correção \"{item.Title}\" aplicada pelo usuário"
                };

                var exec = await _repairEngine.ExecuteAsync(request, CancellationToken.None).ConfigureAwait(true);
                if (exec.Outcome == RepairOutcome.Succeeded)
                {
                    executedCount++;
                    var compText = exec.Comparison != null
                        ? $" ({exec.Comparison.Name}: {exec.Comparison.Before:F1} → {exec.Comparison.After:F1} {exec.Comparison.Unit})"
                        : string.Empty;
                    summaryList.Add($"✔ {item.Title}: {exec.Message}{compText}");
                }
                else
                {
                    failedCount++;
                    summaryList.Add($"✖ {item.Title}: {exec.Message} (Status: {exec.Outcome})");
                }
            }

            OperationFeedback = $"Execução finalizada: {executedCount} sucesso(s), {failedCount} falha(s).\n\n" + string.Join("\n", summaryList);
            await LoadHistoryCoreAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao aplicar correções selecionadas");
            OperationFeedback = $"Erro ao aplicar correções: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            StatusMessage = null;
        }
    }

    [RelayCommand]
    public async Task RollbackRecordAsync(RepairHistoryItemViewModel? item)
    {
        item ??= SelectedHistoryRecord;
        if (item == null || !item.CanRollback || IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = $"Desfazendo correção \"{item.ActionTitle}\"...";
        OperationFeedback = null;

        try
        {
            var rollback = await _repairEngine.RollbackAsync(item.Id, CancellationToken.None).ConfigureAwait(true);
            OperationFeedback = rollback.Success
                ? $"Rollback concluído com sucesso: {rollback.Message}"
                : $"Falha ao desfazer correção: {rollback.Message}";

            await LoadHistoryCoreAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante rollback da correção {Id}", item.Id);
            OperationFeedback = $"Erro ao desfazer correção: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            StatusMessage = null;
        }
    }
}
