namespace LeccorWindowsHealthCenter.Application.Storage;

/// <summary>
/// Resumo de um snapshot salvo de análise de espaço em disco no SQLite (Pacote B).
/// </summary>
public sealed record SpaceAnalysisSnapshotSummary(
    long Id,
    string VolumeRoot,
    DateTimeOffset CapturedAt,
    long TotalSizeBytes,
    long TotalFilesCount,
    TimeSpan Duration);

/// <summary>
/// Repositório para persistência e recuperação do mapa de diretórios e arquivos em disco.
/// Permite carregar instantaneamente o estado anterior da tela sem precisar reanalisar todo o volume.
/// </summary>
public interface ISpaceAnalysisSnapshotRepository
{
    Task SaveSnapshotAsync(string volumeRoot, SpaceAnalysis analysis, CancellationToken cancellationToken);

    Task<SpaceAnalysis?> GetLatestSnapshotAsync(string volumeRoot, CancellationToken cancellationToken);

    Task<IReadOnlyList<SpaceAnalysisSnapshotSummary>> GetSnapshotsHistoryAsync(string volumeRoot, CancellationToken cancellationToken);

    Task DeleteSnapshotAsync(long snapshotId, CancellationToken cancellationToken);
}

