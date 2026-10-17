using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Application.Risk;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Domain.Repairs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.Repair;

/// <summary>
/// Repair Engine (seções 5, 35–39 e 51). Fluxo obrigatório:
/// Risk Analyzer → Dry Run? → User Approval → [medição antes] → Backup → Repair → Validation → [medição depois] → histórico com rollback.
/// Nenhuma alteração acontece antes de o backup estar salvo.
/// </summary>
public sealed class RepairEngine : IRepairEngine
{
    public const string DryRunMessage = "Nenhuma alteração foi realizada.";

    private readonly Dictionary<string, IRepairAction> _actions;
    private readonly IRiskAnalyzer _riskAnalyzer;
    private readonly IBackupStore _backupStore;
    private readonly IRepairHistoryRepository _history;
    private readonly IPrivilegeService _privileges;
    private readonly RepairOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RepairEngine> _logger;

    public RepairEngine(
        IEnumerable<IRepairAction> actions,
        IRiskAnalyzer riskAnalyzer,
        IBackupStore backupStore,
        IRepairHistoryRepository history,
        IPrivilegeService privileges,
        IOptions<RepairOptions> options,
        TimeProvider timeProvider,
        ILogger<RepairEngine> logger)
    {
        _actions = actions.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
        _riskAnalyzer = riskAnalyzer;
        _backupStore = backupStore;
        _history = history;
        _privileges = privileges;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public IReadOnlyList<IRepairAction> Actions => _actions.Values.ToList();

    public async Task<RepairExecution> ExecuteAsync(RepairRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_actions.TryGetValue(request.ActionId, out var action))
        {
            throw new KeyNotFoundException($"Ação de reparo desconhecida: '{request.ActionId}'.");
        }

        var risk = _riskAnalyzer.Assess(action);
        var plan = await action.PlanAsync(cancellationToken).ConfigureAwait(false);

        if (request.DryRun || _options.DryRun)
        {
            _logger.LogInformation("Dry Run da ação {ActionId} (risco {Risk}, {ChangeCount} alterações planejadas)", action.Id, risk.Effective, plan.Count);
            return new RepairExecution { Outcome = RepairOutcome.DryRun, Message = DryRunMessage, Risk = risk, Changes = plan };
        }

        var blocked = CheckPreconditions(action, risk, request.Approval);
        if (blocked is not null)
        {
            _logger.LogWarning("Ação {ActionId} bloqueada: {Reason}", action.Id, blocked);
            return new RepairExecution { Outcome = RepairOutcome.Blocked, Message = blocked, Risk = risk, Changes = plan };
        }

        var startedAt = _timeProvider.GetUtcNow();
        var measurable = action as IMeasurableRepairAction;
        double? before = measurable is null ? null : await measurable.MeasureAsync(cancellationToken).ConfigureAwait(false);

        // 1) Backup ANTES de qualquer alteração. Se falhar, nada é executado.
        BackupRecord backup;
        try
        {
            var previous = await action.CaptureStateAsync(cancellationToken).ConfigureAwait(false);
            backup = new BackupRecord
            {
                ActionId = action.Id,
                Timestamp = startedAt,
                User = _privileges.CurrentUser,
                PreviousValues = previous,
            };
            await _backupStore.SaveAsync(backup, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new RepairExecution { Outcome = RepairOutcome.Cancelled, Message = "Operação cancelada antes de qualquer alteração.", Risk = risk, Changes = plan };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao criar backup para a ação {ActionId}; nenhuma alteração foi feita", action.Id);
            return new RepairExecution { Outcome = RepairOutcome.Failed, Message = $"Backup falhou; nenhuma alteração foi feita. {ex.Message}", Risk = risk, Changes = plan };
        }

        // 2) Execução.
        RepairResult result;
        try
        {
            result = await action.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var cancelled = ex is OperationCanceledException && cancellationToken.IsCancellationRequested;
            _logger.LogError(ex, "Ação {ActionId} falhou durante a execução", action.Id);
            var rollbackNote = await TryAutomaticRollbackAsync(action, backup).ConfigureAwait(false);
            result = new RepairResult(false, (cancelled ? "Operação cancelada durante a execução." : $"Erro: {ex.Message}") + rollbackNote);
            return await RecordAsync(action, request, risk, backup, startedAt, cancelled ? RepairOutcome.Cancelled : RepairOutcome.Failed, result.Message, plan, null).ConfigureAwait(false);
        }

        if (!result.Success)
        {
            var rollbackNote = await TryAutomaticRollbackAsync(action, backup).ConfigureAwait(false);
            return await RecordAsync(action, request, risk, backup, startedAt, RepairOutcome.Failed, result.Message + rollbackNote, plan, null).ConfigureAwait(false);
        }

        // 3) Validação + medição depois. Melhoria só é afirmada com as duas medições (seção 39).
        var validation = await SafeValidateAsync(action).ConfigureAwait(false);
        MeasurementComparison? comparison = validation.Comparison;
        if (comparison is null && measurable is not null && before is not null)
        {
            var after = await measurable.MeasureAsync(CancellationToken.None).ConfigureAwait(false);
            comparison = new MeasurementComparison
            {
                Name = measurable.MeasurementName,
                Unit = measurable.MeasurementUnit,
                Before = before.Value,
                After = after,
                LowerIsBetter = measurable.LowerIsBetter,
            };
        }

        var changes = result.AppliedChanges.Count > 0 ? result.AppliedChanges : plan;
        await SaveBackupResultAsync(action, backup, validation.Passed ? "Sucesso" : "Validação falhou").ConfigureAwait(false);

        var outcome = validation.Passed ? RepairOutcome.Succeeded : RepairOutcome.ValidationFailed;
        var message = validation.Passed ? result.Message : $"{result.Message} Validação: {validation.Message}";
        return await RecordAsync(action, request, risk, backup, startedAt, outcome, message, changes, comparison).ConfigureAwait(false);
    }

    public async Task<RollbackResult> RollbackAsync(Guid repairRecordId, CancellationToken cancellationToken)
    {
        var record = await _history.GetAsync(repairRecordId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return new RollbackResult(false, "Registro de correção não encontrado.");
        }

        if (!record.SupportsRollback || record.RolledBackAt is not null || record.BackupId is null ||
            record.Outcome is not (RepairOutcome.Succeeded or RepairOutcome.ValidationFailed))
        {
            return new RollbackResult(false, "Esta correção não pode ser desfeita.");
        }

        if (!_actions.TryGetValue(record.ActionId, out var action))
        {
            return new RollbackResult(false, $"A ação '{record.ActionId}' não está mais disponível nesta versão.");
        }

        var backup = await _backupStore.GetAsync(record.BackupId.Value, cancellationToken).ConfigureAwait(false);
        if (backup is null)
        {
            return new RollbackResult(false, "Backup da correção não encontrado; rollback automático indisponível.");
        }

        var rollback = await action.RollbackAsync(backup, cancellationToken).ConfigureAwait(false);
        if (rollback.Success)
        {
            await _history.MarkRolledBackAsync(record.Id, _timeProvider.GetUtcNow(), rollback.Message, CancellationToken.None).ConfigureAwait(false);
            _logger.LogInformation("Correção {RecordId} ({ActionId}) desfeita", record.Id, record.ActionId);
        }
        else
        {
            _logger.LogError("Rollback da correção {RecordId} ({ActionId}) falhou: {Message}", record.Id, record.ActionId, rollback.Message);
        }

        return rollback;
    }

    private string? CheckPreconditions(IRepairAction action, RiskAssessment risk, UserApproval approval)
    {
        if (risk.Requirement == ApprovalRequirement.ManualOnly)
        {
            return "Ação de risco CRÍTICO: nunca é executada pela ferramenta. Siga a recomendação manualmente.";
        }

        if (!risk.IsSatisfiedBy(approval))
        {
            return risk.Requirement == ApprovalRequirement.ExplicitConfirmation
                ? "Ação de risco ALTO: exige confirmação explícita do usuário."
                : "Esta ação exige confirmação do usuário.";
        }

        if (action.RequiresElevation && !_privileges.IsElevated)
        {
            return "Esta ação exige privilégios de administrador.";
        }

        return null;
    }

    private async Task<string> TryAutomaticRollbackAsync(IRepairAction action, BackupRecord backup)
    {
        if (!action.SupportsRollback)
        {
            return " A ação não suporta rollback automático; o backup foi preservado.";
        }

        try
        {
            var rollback = await action.RollbackAsync(backup, CancellationToken.None).ConfigureAwait(false);
            return rollback.Success ? " O estado anterior foi restaurado automaticamente." : $" Rollback automático falhou: {rollback.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback automático da ação {ActionId} falhou", action.Id);
            return $" Rollback automático falhou: {ex.Message}";
        }
    }

    private async Task<RepairValidation> SafeValidateAsync(IRepairAction action)
    {
        try
        {
            return await action.ValidateAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validação da ação {ActionId} falhou", action.Id);
            return new RepairValidation(false, $"Não foi possível validar: {ex.Message}");
        }
    }

    private async Task SaveBackupResultAsync(IRepairAction action, BackupRecord backup, string result)
    {
        try
        {
            var newValues = await action.CaptureStateAsync(CancellationToken.None).ConfigureAwait(false);
            await _backupStore.SaveAsync(backup with { NewValues = newValues, Result = result }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // O backup original já está salvo; falhar aqui só perde os "novos valores" informativos.
            _logger.LogWarning(ex, "Não foi possível registrar os novos valores no backup {BackupId}", backup.Id);
        }
    }

    private async Task<RepairExecution> RecordAsync(
        IRepairAction action,
        RepairRequest request,
        RiskAssessment risk,
        BackupRecord backup,
        DateTimeOffset startedAt,
        RepairOutcome outcome,
        string message,
        IReadOnlyList<PlannedChange> changes,
        MeasurementComparison? comparison)
    {
        var record = new RepairRecord
        {
            ActionId = action.Id,
            ActionTitle = action.Title,
            FindingId = request.FindingId,
            Risk = risk.Effective,
            Outcome = outcome,
            StartedAt = startedAt,
            CompletedAt = _timeProvider.GetUtcNow(),
            User = _privileges.CurrentUser,
            Reason = request.Reason,
            Message = message,
            BackupId = backup.Id,
            SupportsRollback = action.SupportsRollback,
            Changes = changes,
            Comparison = comparison,
        };

        try
        {
            await _history.AddAsync(record, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar a correção {ActionId} no histórico", action.Id);
        }

        _logger.LogInformation("Ação {ActionId} finalizada com resultado {Outcome}", action.Id, outcome);
        return new RepairExecution { Outcome = outcome, Message = message, Risk = risk, Changes = changes, Record = record, Comparison = comparison };
    }
}
