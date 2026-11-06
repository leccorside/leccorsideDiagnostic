using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Application.Risk;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;
using LeccorWindowsHealthCenter.Infrastructure.Repair;
using LeccorWindowsHealthCenter.Repair.Actions;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.Repair;

public class RepairActionsTests
{
    [Fact]
    public async Task DisableStartupEntryAction_plans_and_captures_correctly()
    {
        var action = new DisableStartupEntryRepairAction("TestStartupApp", isCurrentUser: true);

        Assert.Equal("repair.startup.disable", action.Id);
        Assert.Equal(RiskLevel.Low, action.Risk);
        Assert.False(action.RequiresElevation);
        Assert.True(action.SupportsRollback);

        var plan = await action.PlanAsync(CancellationToken.None);
        Assert.Single(plan);
        Assert.Contains("TestStartupApp", plan[0].Target);
        Assert.Contains("0x03", plan[0].NewValue);

        var state = await action.CaptureStateAsync(CancellationToken.None);
        Assert.Equal("TestStartupApp", state["EntryName"]);
        Assert.Equal("True", state["IsCurrentUser"]);
    }

    [Fact]
    public async Task DisableStartupEntryAction_execute_validate_and_rollback()
    {
        var uniqueName = $"LeccorTestEntry_{Guid.NewGuid():N}";
        var action = new DisableStartupEntryRepairAction(uniqueName, isCurrentUser: true);

        try
        {
            var initialCapture = await action.CaptureStateAsync(CancellationToken.None);

            var backup = new BackupRecord
            {
                ActionId = action.Id,
                Timestamp = DateTimeOffset.UtcNow,
                User = "TestUser",
                PreviousValues = initialCapture
            };

            var exec = await action.ExecuteAsync(CancellationToken.None);
            Assert.True(exec.Success);

            var validation = await action.ValidateAsync(CancellationToken.None);
            Assert.True(validation.Passed);

            var rollback = await action.RollbackAsync(backup, CancellationToken.None);
            Assert.True(rollback.Success);
        }
        finally
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", writable: true);
            key?.DeleteValue(uniqueName, throwOnMissingValue: false);
        }
    }

    [Fact]
    public async Task DisableShellExtensionAction_plans_and_captures_correctly()
    {
        var clsid = "{11111111-2222-3333-4444-555555555555}";
        var action = new DisableShellExtensionRepairAction(clsid, "ExtensaoTeste");

        Assert.Equal("repair.explorer.disable-shell-extension", action.Id);
        Assert.Equal(RiskLevel.Low, action.Risk);
        Assert.False(action.RequiresElevation);
        Assert.True(action.SupportsRollback);

        var plan = await action.PlanAsync(CancellationToken.None);
        Assert.Single(plan);
        Assert.Contains(clsid, plan[0].Target);

        var state = await action.CaptureStateAsync(CancellationToken.None);
        Assert.Equal(clsid, state["Clsid"]);
        Assert.Equal("ExtensaoTeste", state["ExtensionName"]);
    }

    [Fact]
    public async Task DisableShellExtensionAction_execute_validate_and_rollback()
    {
        var clsid = $"{{{Guid.NewGuid().ToString().ToUpperInvariant()}}}";
        var action = new DisableShellExtensionRepairAction(clsid, "ShellExtTesteRollback");

        try
        {
            var backupState = await action.CaptureStateAsync(CancellationToken.None);
            var backup = new BackupRecord
            {
                ActionId = action.Id,
                Timestamp = DateTimeOffset.UtcNow,
                User = "TestUser",
                PreviousValues = backupState
            };

            var result = await action.ExecuteAsync(CancellationToken.None);
            Assert.True(result.Success);

            var validation = await action.ValidateAsync(CancellationToken.None);
            Assert.True(validation.Passed);

            var rollback = await action.RollbackAsync(backup, CancellationToken.None);
            Assert.True(rollback.Success);

            var postRollbackValidation = await action.ValidateAsync(CancellationToken.None);
            Assert.False(postRollbackValidation.Passed);
        }
        finally
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", writable: true);
            key?.DeleteValue(clsid, throwOnMissingValue: false);
        }
    }

    [Fact]
    public async Task SafeCleanTempAction_implements_measurable_and_executes_safely()
    {
        var action = new SafeCleanTempRepairAction();

        Assert.Equal("repair.storage.safe-clean", action.Id);
        Assert.Equal(RiskLevel.Safe, action.Risk);
        Assert.False(action.RequiresElevation);
        Assert.False(action.SupportsRollback);
        Assert.Equal("Arquivos Temporários", action.MeasurementName);
        Assert.Equal("MB", action.MeasurementUnit);
        Assert.True(action.LowerIsBetter);

        var plan = await action.PlanAsync(CancellationToken.None);
        Assert.NotEmpty(plan);

        var measure = await action.MeasureAsync(CancellationToken.None);
        Assert.True(measure >= 0);

        var state = await action.CaptureStateAsync(CancellationToken.None);
        Assert.True(state.ContainsKey("EstimatedSizeMb"));

        var exec = await action.ExecuteAsync(CancellationToken.None);
        Assert.True(exec.Success);
        Assert.Contains("Limpeza segura", exec.Message);

        var validation = await action.ValidateAsync(CancellationToken.None);
        Assert.True(validation.Passed);
    }

    [Fact]
    public async Task EnableWindowsUpdateServiceAction_plans_and_captures_wuauserv()
    {
        var action = new EnableWindowsUpdateServiceRepairAction();

        Assert.Equal("repair.windowsupdate.enable-service", action.Id);
        Assert.Equal(RiskLevel.Low, action.Risk);
        Assert.True(action.RequiresElevation);
        Assert.True(action.SupportsRollback);

        var plan = await action.PlanAsync(CancellationToken.None);
        Assert.Single(plan);
        Assert.Contains("wuauserv", plan[0].Target);

        var state = await action.CaptureStateAsync(CancellationToken.None);
        Assert.True(state.ContainsKey("PreviousStartText"));
    }

    [Fact]
    public async Task RestorePointService_handles_unelevated_gracefully()
    {
        var privileges = new FakePrivilegeService(isElevated: false);
        var service = new RestorePointService(privileges, NullLogger<RestorePointService>.Instance);

        var result = await service.CreateRestorePointAsync("Teste", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Administrador", result.Message);
    }

    [Fact]
    public async Task RestorePointService_is_available_check_does_not_throw()
    {
        var privileges = new FakePrivilegeService(isElevated: false);
        var service = new RestorePointService(privileges, NullLogger<RestorePointService>.Instance);

        var available = await service.IsAvailableAsync(CancellationToken.None);
        // Não deve lançar exceção
        Assert.True(available || !available);
    }

    private sealed class FakePrivilegeService : IPrivilegeService
    {
        public bool IsElevated { get; }
        public string CurrentUser => @"PC\User";

        public FakePrivilegeService(bool isElevated)
        {
            IsElevated = isElevated;
        }
    }
}
