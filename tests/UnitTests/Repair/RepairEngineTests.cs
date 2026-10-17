using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Application.Risk;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;
using LeccorWindowsHealthCenter.Repair;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.UnitTests.Repair;

public class RepairEngineTests
{
    private const string StartupKey = "HKCU\\Run\\App";

    private readonly List<string> _log = [];
    private readonly InMemoryRepairHistory _history = new();
    private readonly FakePrivileges _privileges = new();
    private readonly InMemoryBackupStore _backups;

    public RepairEngineTests()
    {
        _backups = new InMemoryBackupStore(_log);
    }

    private RepairEngine Engine(IRepairAction action, bool globalDryRun = false) => new(
        [action],
        new RiskAnalyzer(),
        _backups,
        _history,
        _privileges,
        Options.Create(new RepairOptions { DryRun = globalDryRun }),
        TimeProvider.System,
        NullLogger<RepairEngine>.Instance);

    private static RepairRequest Request(UserApproval approval = UserApproval.Confirmed, bool dryRun = false) =>
        new() { ActionId = "fake.disable-startup", Approval = approval, DryRun = dryRun, Reason = "Startup lento" };

    [Fact]
    public async Task Dry_run_reports_plan_without_changing_anything()
    {
        var action = new FakeRepairAction(_log);

        var execution = await Engine(action).ExecuteAsync(Request(dryRun: true), CancellationToken.None);

        Assert.Equal(RepairOutcome.DryRun, execution.Outcome);
        Assert.Equal(RepairEngine.DryRunMessage, execution.Message);
        Assert.Single(execution.Changes);
        Assert.Equal("C:\\app.exe", action.State[StartupKey]);
        Assert.Empty(_log);
        Assert.Empty(_history.Records);
    }

    [Fact]
    public async Task Global_dry_run_overrides_request()
    {
        var action = new FakeRepairAction(_log);

        var execution = await Engine(action, globalDryRun: true).ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(RepairOutcome.DryRun, execution.Outcome);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task Blocks_without_required_approval()
    {
        var action = new FakeRepairAction(_log, RiskLevel.Medium);

        var execution = await Engine(action).ExecuteAsync(Request(UserApproval.None), CancellationToken.None);

        Assert.Equal(RepairOutcome.Blocked, execution.Outcome);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task High_risk_requires_explicit_confirmation()
    {
        var action = new FakeRepairAction(_log, RiskLevel.High);

        var confirmed = await Engine(action).ExecuteAsync(Request(UserApproval.Confirmed), CancellationToken.None);
        var explicitly = await Engine(action).ExecuteAsync(Request(UserApproval.ExplicitlyConfirmed), CancellationToken.None);

        Assert.Equal(RepairOutcome.Blocked, confirmed.Outcome);
        Assert.Equal(RepairOutcome.Succeeded, explicitly.Outcome);
    }

    [Fact]
    public async Task Critical_risk_is_never_executed()
    {
        var action = new FakeRepairAction(_log, RiskLevel.Critical);

        var execution = await Engine(action).ExecuteAsync(Request(UserApproval.ExplicitlyConfirmed), CancellationToken.None);

        Assert.Equal(RepairOutcome.Blocked, execution.Outcome);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task Elevation_required_but_not_elevated_is_blocked()
    {
        var action = new FakeRepairAction(_log) { RequiresElevation = true };

        var execution = await Engine(action).ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(RepairOutcome.Blocked, execution.Outcome);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task Backup_happens_before_execution_and_history_is_recorded()
    {
        var action = new FakeRepairAction(_log);

        var execution = await Engine(action).ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(RepairOutcome.Succeeded, execution.Outcome);
        Assert.Equal("backup", _log[0]);
        Assert.Equal("execute", _log[1]);

        var record = Assert.Single(_history.Records);
        Assert.True(record.CanRollback);
        Assert.Equal("Startup lento", record.Reason);
        Assert.Equal(@"PC\teste", record.User);

        var backup = _backups.Records[record.BackupId!.Value];
        Assert.Equal("C:\\app.exe", backup.PreviousValues[StartupKey]);
        Assert.Null(backup.NewValues[StartupKey]);
    }

    [Fact]
    public async Task Measures_before_and_after()
    {
        var action = new FakeRepairAction(_log) { MeasureValue = 10 };

        var execution = await Engine(action).ExecuteAsync(Request(), CancellationToken.None);

        Assert.NotNull(execution.Comparison);
        Assert.Equal(10, execution.Comparison.Before);
        Assert.Equal(2, execution.Comparison.After);
        Assert.Equal(80, execution.Comparison.ImprovementPercent);
    }

    [Fact]
    public async Task Backup_failure_aborts_without_changes()
    {
        _backups.Fail = true;
        var action = new FakeRepairAction(_log);

        var execution = await Engine(action).ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(RepairOutcome.Failed, execution.Outcome);
        Assert.DoesNotContain("execute", _log);
        Assert.Equal("C:\\app.exe", action.State[StartupKey]);
    }

    [Fact]
    public async Task Execution_failure_triggers_automatic_rollback()
    {
        var action = new FakeRepairAction(_log) { ThrowOnExecute = true };

        var execution = await Engine(action).ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(RepairOutcome.Failed, execution.Outcome);
        Assert.Contains("rollback", _log);
        Assert.Equal("C:\\app.exe", action.State[StartupKey]);
    }

    [Fact]
    public async Task Validation_failure_keeps_rollback_available()
    {
        var action = new FakeRepairAction(_log) { FailValidation = true };
        var engine = Engine(action);

        var execution = await engine.ExecuteAsync(Request(), CancellationToken.None);
        var rollback = await engine.RollbackAsync(execution.Record!.Id, CancellationToken.None);

        Assert.Equal(RepairOutcome.ValidationFailed, execution.Outcome);
        Assert.True(rollback.Success);
    }

    [Fact]
    public async Task Rollback_restores_previous_state_and_cannot_run_twice()
    {
        var action = new FakeRepairAction(_log);
        var engine = Engine(action);
        var execution = await engine.ExecuteAsync(Request(), CancellationToken.None);
        Assert.Null(action.State[StartupKey]);

        var first = await engine.RollbackAsync(execution.Record!.Id, CancellationToken.None);
        var second = await engine.RollbackAsync(execution.Record!.Id, CancellationToken.None);

        Assert.True(first.Success);
        Assert.Equal("C:\\app.exe", action.State[StartupKey]);
        Assert.NotNull(_history.Records[0].RolledBackAt);
        Assert.False(second.Success);
    }

    [Fact]
    public async Task Unknown_action_is_rejected()
    {
        var engine = Engine(new FakeRepairAction(_log));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            engine.ExecuteAsync(new RepairRequest { ActionId = "nao.existe" }, CancellationToken.None));
    }
}
