using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;

namespace LeccorWindowsHealthCenter.Application.Risk;

public enum ApprovalRequirement
{
    /// <summary>SAFE: pode executar sem perguntar.</summary>
    None,

    /// <summary>LOW/MEDIUM: exige confirmação.</summary>
    Confirmation,

    /// <summary>HIGH: exige confirmação explícita após exibir riscos e alterações.</summary>
    ExplicitConfirmation,

    /// <summary>CRITICAL: a ferramenta nunca executa; apenas orienta o usuário.</summary>
    ManualOnly,
}

public sealed record RiskAssessment
{
    public required RiskLevel Declared { get; init; }

    /// <summary>Risco após as regras de escalonamento.</summary>
    public required RiskLevel Effective { get; init; }

    public required ApprovalRequirement Requirement { get; init; }

    public bool AllowsAutomaticExecution => Requirement == ApprovalRequirement.None;

    /// <summary>Motivos do escalonamento (exibidos ao usuário).</summary>
    public IReadOnlyList<string> Reasons { get; init; } = [];

    public bool IsSatisfiedBy(UserApproval approval) => Requirement switch
    {
        ApprovalRequirement.None => true,
        ApprovalRequirement.Confirmation => approval >= UserApproval.Confirmed,
        ApprovalRequirement.ExplicitConfirmation => approval == UserApproval.ExplicitlyConfirmed,
        _ => false,
    };
}

public interface IRiskAnalyzer
{
    RiskAssessment Assess(IRepairAction action);
}

/// <summary>
/// Risk Analyzer (seções 5 e 6). Nunca reduz o risco declarado pela ação, apenas eleva quando
/// as características da ação contradizem o nível declarado.
/// </summary>
public sealed class RiskAnalyzer : IRiskAnalyzer
{
    public RiskAssessment Assess(IRepairAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var effective = action.Risk;
        var reasons = new List<string>();

        // SAFE/LOW pressupõem reversibilidade; sem rollback, no mínimo MEDIUM.
        if (!action.SupportsRollback && effective < RiskLevel.Medium)
        {
            effective = RiskLevel.Medium;
            reasons.Add("A ação não pode ser desfeita automaticamente.");
        }

        // Exigir reinicialização altera o comportamento do sistema: no mínimo MEDIUM.
        if (action.RequiresRestart && effective < RiskLevel.Medium)
        {
            effective = RiskLevel.Medium;
            reasons.Add("A ação exige reinicialização do computador.");
        }

        // Ação administrativa nunca é executada sem o usuário saber: no mínimo LOW.
        if (action.RequiresElevation && effective < RiskLevel.Low)
        {
            effective = RiskLevel.Low;
            reasons.Add("A ação exige privilégios de administrador.");
        }

        return new RiskAssessment
        {
            Declared = action.Risk,
            Effective = effective,
            Requirement = RequirementFor(effective),
            Reasons = reasons,
        };
    }

    public static ApprovalRequirement RequirementFor(RiskLevel level) => level switch
    {
        RiskLevel.Safe => ApprovalRequirement.None,
        RiskLevel.Low or RiskLevel.Medium => ApprovalRequirement.Confirmation,
        RiskLevel.High => ApprovalRequirement.ExplicitConfirmation,
        _ => ApprovalRequirement.ManualOnly,
    };
}
