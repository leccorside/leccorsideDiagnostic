using LeccorWindowsHealthCenter.App.ViewModels;
using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Application.Risk;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;
using LeccorWindowsHealthCenter.Repair;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.SystemTests.ViewModels;

public class RepairsViewModelTests
{
    [Fact]
    public async Task RepairsViewModel_manages_actions_dryrun_and_execution()
    {
        var log = new List<string>();
        var fakeAction = new TestRepairAction(log);
        var mockRestorePoint = new TestRestorePointService();
        var mockHistory = new TestRepairHistory();

        var engine = new RepairEngine(
            [fakeAction],
            new RiskAnalyzer(),
            new TestBackupStore(log),
            mockHistory,
            new TestPrivileges(),
            Options.Create(new RepairOptions()),
            TimeProvider.System,
            NullLogger<RepairEngine>.Instance);

        var vm = new RepairsViewModel(engine, mockRestorePoint, mockHistory, NullLogger<RepairsViewModel>.Instance);

        Assert.Single(vm.AvailableRepairs);
        Assert.Equal(1, vm.SelectedCount);

        vm.DeselectAll();
        Assert.Equal(0, vm.SelectedCount);

        vm.SelectAll();
        Assert.Equal(1, vm.SelectedCount);

        // Dry run
        await vm.RunDryRunAsync();
        Assert.NotNull(vm.OperationFeedback);
        Assert.Contains("Simulação concluída", vm.OperationFeedback);

        // Criação de ponto de restauração
        await vm.CreateRestorePointAsync();
        Assert.True(mockRestorePoint.CreateCalled);

        // Aplicação de correção
        await vm.ApplySelectedRepairsAsync();
        Assert.Contains("Execução finalizada", vm.OperationFeedback);
        Assert.Single(vm.HistoryRecords);

        // Rollback
        var historyItem = vm.HistoryRecords[0];
        Assert.True(historyItem.CanRollback);
        await vm.RollbackRecordAsync(historyItem);
        Assert.Contains("Rollback concluído", vm.OperationFeedback);
    }

    private sealed class TestRepairAction : IRepairAction
    {
        private readonly List<string> _log;

        public TestRepairAction(List<string> log)
        {
            _log = log;
        }

        public string Id => "test.repair";
        public string Title => "Ação de Teste";
        public string Description => "Ação de teste do ViewModel";
        public RiskLevel Risk => RiskLevel.Low;
        public bool RequiresElevation => false;
        public bool RequiresRestart => false;
        public bool SupportsRollback => true;

        public Task<IReadOnlyList<PlannedChange>> PlanAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<PlannedChange> changes = [new PlannedChange { Target = "Target1", Description = "Desc1" }];
            return Task.FromResult(changes);
        }

        public Task<IReadOnlyDictionary<string, string?>> CaptureStateAsync(CancellationToken cancellationToken)
        {
            _log.Add("backup");
            IReadOnlyDictionary<string, string?> dict = new Dictionary<string, string?> { ["Key"] = "Val" };
            return Task.FromResult(dict);
        }

        public Task<RepairResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            _log.Add("execute");
            return Task.FromResult(new RepairResult(true, "Sucesso no teste"));
        }

        public Task<RepairValidation> ValidateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new RepairValidation(true, "Validado"));
        }

        public Task<RollbackResult> RollbackAsync(BackupRecord backup, CancellationToken cancellationToken)
        {
            _log.Add("rollback");
            return Task.FromResult(new RollbackResult(true, "Rollback com sucesso"));
        }
    }

    private sealed class TestPrivileges : IPrivilegeService
    {
        public bool IsElevated => true;
        public string CurrentUser => @"PC\Test";
    }

    private sealed class TestRestorePointService : IRestorePointService
    {
        public bool CreateCalled { get; private set; }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<RestorePointResult> CreateRestorePointAsync(string description, CancellationToken cancellationToken)
        {
            CreateCalled = true;
            return Task.FromResult(new RestorePointResult(true, "Ponto de restauração de teste criado com sucesso."));
        }
    }

    private sealed class TestBackupStore : IBackupStore
    {
        private readonly Dictionary<Guid, BackupRecord> _records = new();
        private readonly List<string> _log;

        public TestBackupStore(List<string> log)
        {
            _log = log;
        }

        public Task SaveAsync(BackupRecord record, CancellationToken cancellationToken)
        {
            _records[record.Id] = record;
            return Task.CompletedTask;
        }

        public Task<BackupRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(_records.GetValueOrDefault(id));
        }

        public Task<IReadOnlyList<BackupRecord>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<BackupRecord>>(_records.Values.ToList());
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            _records.Remove(id);
            return Task.CompletedTask;
        }
    }

    private sealed class TestRepairHistory : IRepairHistoryRepository
    {
        private readonly List<RepairRecord> _records = [];

        public Task AddAsync(RepairRecord record, CancellationToken cancellationToken)
        {
            _records.Add(record);
            return Task.CompletedTask;
        }

        public Task MarkRolledBackAsync(Guid recordId, DateTimeOffset rolledBackAt, string message, CancellationToken cancellationToken)
        {
            var existing = _records.FirstOrDefault(r => r.Id == recordId);
            if (existing != null)
            {
                var index = _records.IndexOf(existing);
                // substitui com RolledBackAt
                _records[index] = existing with { RolledBackAt = rolledBackAt };
            }
            return Task.CompletedTask;
        }

        public Task<RepairRecord?> GetAsync(Guid recordId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_records.FirstOrDefault(r => r.Id == recordId));
        }

        public Task<IReadOnlyList<RepairRecord>> ListAsync(int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RepairRecord>>(_records.Take(limit).ToList());
        }
    }
}

