using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;

namespace LeccorWindowsHealthCenter.Application.Persistence;

public sealed record ScanSummary(Guid Id, ScanMode Mode, DateTimeOffset StartedAt, DateTimeOffset CompletedAt, int? Score, HealthRating Rating, int ProblemCount, int RecommendationCount, bool WasCancelled);

public interface IScanRepository
{
    Task SaveAsync(ScanReport report, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScanSummary>> GetRecentAsync(int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<Finding>> GetFindingsAsync(Guid scanId, CancellationToken cancellationToken);
}

public interface IRepairHistoryRepository
{
    Task AddAsync(RepairRecord record, CancellationToken cancellationToken);

    Task MarkRolledBackAsync(Guid recordId, DateTimeOffset rolledBackAt, string message, CancellationToken cancellationToken);

    Task<RepairRecord?> GetAsync(Guid recordId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RepairRecord>> ListAsync(int limit, CancellationToken cancellationToken);
}

/// <summary>Backup Engine (seção 36): estado anterior salvo antes de qualquer alteração relevante.</summary>
public interface IBackupStore
{
    Task SaveAsync(BackupRecord record, CancellationToken cancellationToken);

    Task<BackupRecord?> GetAsync(Guid backupId, CancellationToken cancellationToken);
}
