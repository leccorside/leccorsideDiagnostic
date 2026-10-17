using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;

namespace LeccorWindowsHealthCenter.Application.Repair;

/// <summary>
/// Ação de reparo (seção 35). Separada dos scanners: encontrar um problema não significa corrigi-lo.
/// O <c>RepairEngine</c> orquestra: risco → aprovação → backup → execução → validação → rollback.
/// </summary>
public interface IRepairAction
{
    string Id { get; }

    string Title { get; }

    string Description { get; }

    RiskLevel Risk { get; }

    bool RequiresElevation { get; }

    bool RequiresRestart { get; }

    bool SupportsRollback { get; }

    /// <summary>Lista as alterações que seriam feitas, sem alterar nada (Dry Run — seção 51).</summary>
    Task<IReadOnlyList<PlannedChange>> PlanAsync(CancellationToken cancellationToken);

    /// <summary>Captura o estado atual que será alterado, para backup e rollback.</summary>
    Task<IReadOnlyDictionary<string, string?>> CaptureStateAsync(CancellationToken cancellationToken);

    Task<RepairResult> ExecuteAsync(CancellationToken cancellationToken);

    /// <summary>Verifica se a correção surtiu efeito.</summary>
    Task<RepairValidation> ValidateAsync(CancellationToken cancellationToken);

    /// <summary>Restaura o estado salvo no backup.</summary>
    Task<RollbackResult> RollbackAsync(BackupRecord backup, CancellationToken cancellationToken);
}

/// <summary>
/// Ação cujo efeito pode ser medido antes e depois (seção 39). O RepairEngine mede antes de
/// executar e depois de executar; melhoria só é afirmada com as duas medições.
/// </summary>
public interface IMeasurableRepairAction : IRepairAction
{
    string MeasurementName { get; }

    string MeasurementUnit { get; }

    bool LowerIsBetter { get; }

    Task<double> MeasureAsync(CancellationToken cancellationToken);
}
