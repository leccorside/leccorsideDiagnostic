using LeccorWindowsHealthCenter.Application.Risk;
using LeccorWindowsHealthCenter.Domain.Repairs;

namespace LeccorWindowsHealthCenter.Application.Repair;

public sealed record RepairRequest
{
    public required string ActionId { get; init; }

    public UserApproval Approval { get; init; } = UserApproval.None;

    /// <summary>Simulação: nada é alterado, apenas o plano é devolvido.</summary>
    public bool DryRun { get; init; }

    public Guid? FindingId { get; init; }

    /// <summary>Motivo exibido no histórico, normalmente o título do finding.</summary>
    public string? Reason { get; init; }
}

public sealed record RepairExecution
{
    public required RepairOutcome Outcome { get; init; }

    public required string Message { get; init; }

    public required RiskAssessment Risk { get; init; }

    public IReadOnlyList<PlannedChange> Changes { get; init; } = [];

    public RepairRecord? Record { get; init; }

    public MeasurementComparison? Comparison { get; init; }
}

public interface IRepairEngine
{
    IReadOnlyList<IRepairAction> Actions { get; }

    Task<RepairExecution> ExecuteAsync(RepairRequest request, CancellationToken cancellationToken);

    Task<RollbackResult> RollbackAsync(Guid repairRecordId, CancellationToken cancellationToken);
}

public sealed class RepairOptions
{
    public const string SectionName = "Repair";

    /// <summary>Quando true, TODA execução vira simulação (modo Dry Run global — seção 51).</summary>
    public bool DryRun { get; set; }
}
