using LeccorWindowsHealthCenter.Application.Risk;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;

namespace LeccorWindowsHealthCenter.UnitTests.Risk;

public class RiskAnalyzerTests
{
    private readonly RiskAnalyzer _analyzer = new();

    [Theory]
    [InlineData(RiskLevel.Safe, ApprovalRequirement.None)]
    [InlineData(RiskLevel.Low, ApprovalRequirement.Confirmation)]
    [InlineData(RiskLevel.Medium, ApprovalRequirement.Confirmation)]
    [InlineData(RiskLevel.High, ApprovalRequirement.ExplicitConfirmation)]
    [InlineData(RiskLevel.Critical, ApprovalRequirement.ManualOnly)]
    public void Maps_risk_level_to_approval_requirement(RiskLevel level, ApprovalRequirement expected) =>
        Assert.Equal(expected, RiskAnalyzer.RequirementFor(level));

    [Fact]
    public void Safe_reversible_action_runs_automatically()
    {
        var assessment = _analyzer.Assess(new FakeRepairAction([], RiskLevel.Safe));

        Assert.Equal(RiskLevel.Safe, assessment.Effective);
        Assert.True(assessment.AllowsAutomaticExecution);
        Assert.Empty(assessment.Reasons);
    }

    [Fact]
    public void Action_without_rollback_is_at_least_medium()
    {
        var assessment = _analyzer.Assess(new FakeRepairAction([], RiskLevel.Safe, supportsRollback: false));

        Assert.Equal(RiskLevel.Medium, assessment.Effective);
        Assert.Equal(RiskLevel.Safe, assessment.Declared);
        Assert.False(assessment.AllowsAutomaticExecution);
        Assert.Single(assessment.Reasons);
    }

    [Fact]
    public void Restart_requirement_escalates_to_medium()
    {
        var assessment = _analyzer.Assess(new FakeRepairAction([], RiskLevel.Low) { RequiresRestart = true });

        Assert.Equal(RiskLevel.Medium, assessment.Effective);
    }

    [Fact]
    public void Elevation_is_never_safe()
    {
        var assessment = _analyzer.Assess(new FakeRepairAction([], RiskLevel.Safe) { RequiresElevation = true });

        Assert.Equal(RiskLevel.Low, assessment.Effective);
        Assert.False(assessment.AllowsAutomaticExecution);
    }

    [Fact]
    public void Never_lowers_declared_risk()
    {
        var assessment = _analyzer.Assess(new FakeRepairAction([], RiskLevel.High));

        Assert.Equal(RiskLevel.High, assessment.Effective);
    }

    [Theory]
    [InlineData(ApprovalRequirement.None, UserApproval.None, true)]
    [InlineData(ApprovalRequirement.Confirmation, UserApproval.None, false)]
    [InlineData(ApprovalRequirement.Confirmation, UserApproval.Confirmed, true)]
    [InlineData(ApprovalRequirement.ExplicitConfirmation, UserApproval.Confirmed, false)]
    [InlineData(ApprovalRequirement.ExplicitConfirmation, UserApproval.ExplicitlyConfirmed, true)]
    [InlineData(ApprovalRequirement.ManualOnly, UserApproval.ExplicitlyConfirmed, false)]
    public void Approval_satisfaction(ApprovalRequirement requirement, UserApproval approval, bool expected)
    {
        var assessment = new RiskAssessment { Declared = RiskLevel.Low, Effective = RiskLevel.Low, Requirement = requirement };

        Assert.Equal(expected, assessment.IsSatisfiedBy(approval));
    }
}
