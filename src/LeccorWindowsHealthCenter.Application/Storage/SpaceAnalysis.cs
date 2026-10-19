namespace LeccorWindowsHealthCenter.Application.Storage;

/// <summary>
/// Pasta analisada (seção 15). Só as maiores subpastas são mantidas (<see cref="SpaceAnalysisOptions.MaxChildrenPerFolder"/>,
/// acima de <see cref="SpaceAnalysisOptions.MinFolderBytes"/>); as demais entram em <see cref="HiddenChildrenBytes"/>.
/// Assim um disco com 400 mil pastas não vira 400 mil objetos em memória.
/// </summary>
public sealed record FolderNode
{
    public required string Name { get; init; }

    public required string Path { get; init; }

    /// <summary>Tamanho lógico da pasta inteira (arquivos diretos + subpastas), sem arquivos só na nuvem.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Arquivos na pasta inteira (inclui subpastas).</summary>
    public long FileCount { get; init; }

    /// <summary>Bytes dos arquivos diretamente nesta pasta (fora de subpastas).</summary>
    public long FilesBytes { get; init; }

    public IReadOnlyList<FolderNode> Children { get; init; } = [];

    /// <summary>Subpastas pequenas ou além do limite, somadas em vez de listadas.</summary>
    public long HiddenChildrenBytes { get; init; }

    public int HiddenChildrenCount { get; init; }

    /// <summary>Pastas sem permissão de leitura dentro desta (inclui ela mesma). O tamanho real pode ser maior.</summary>
    public int InaccessibleCount { get; init; }

    /// <summary>Arquivos do OneDrive/nuvem que não estão baixados: aparecem no Explorer, mas não ocupam o disco.</summary>
    public long CloudOnlyBytes { get; init; }

    /// <summary>Esta pasta não pôde ser lida (acesso negado).</summary>
    public bool Inaccessible { get; init; }
}

public sealed record FileEntry(string Path, long SizeBytes, DateTimeOffset LastWrite);

public sealed record SpaceAnalysis
{
    public required FolderNode Root { get; init; }

    /// <summary>Maiores arquivos de toda a árvore, do maior para o menor.</summary>
    public IReadOnlyList<FileEntry> LargestFiles { get; init; } = [];

    /// <summary>Arquivos grandes sem modificação há muito tempo (<see cref="SpaceAnalysisOptions.OldFileAge"/>), do maior para o menor.</summary>
    public IReadOnlyList<FileEntry> OldFiles { get; init; } = [];

    public TimeSpan Duration { get; init; }

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record SpaceProgress(long FilesScanned, long BytesScanned, long FoldersScanned, string CurrentFolder);

/// <summary>Seção "SpaceAnalysis" do appsettings.json.</summary>
public sealed class SpaceAnalysisOptions
{
    public const string SectionName = "SpaceAnalysis";

    /// <summary>
    /// Pastas lidas em paralelo em SSD/NVMe. Medido em 18/10/2026 (C:\Program Files, 458 mil arquivos): 1 leitor = 5,2 s,
    /// 4 leitores = 2,2–4,4 s; com cache frio, 1 leitor por vez deixava o C: inteiro em ~9 min.
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// Em HDD (ou tipo não identificado), uma leitura por vez: leituras simultâneas obrigam a cabeça do disco a saltar
    /// entre pastas e deixam tudo mais lento — a ferramenta não pode causar a lentidão que mede (seção 58).
    /// </summary>
    public int RotationalMaxConcurrency { get; set; } = 1;

    public int MaxChildrenPerFolder { get; set; } = 50;

    public long MinFolderBytes { get; set; } = 1024 * 1024;

    public int TopFiles { get; set; } = 50;

    public TimeSpan OldFileAge { get; set; } = TimeSpan.FromDays(730);

    public long OldFileMinimumBytes { get; set; } = 10L * 1024 * 1024;
}

public enum StorageLocationKind
{
    Temporary,
    Cache,
    CrashData,
    Update,
    RecycleBin,
}

/// <summary>
/// Local conhecido de temporários/caches. Só é MEDIDO neste passo: limpeza segura chega no Passo 7, e
/// nada aqui é apagado (seção 15: não excluir automaticamente).
/// </summary>
/// <param name="PathPatterns">Caminhos com variáveis de ambiente; "*" substitui um único nível (ex.: perfis do navegador).</param>
public sealed record StorageLocation(string Id, string Title, StorageLocationKind Kind, string Description, IReadOnlyList<string> PathPatterns);

public sealed record LocationMeasure
{
    public required StorageLocation Location { get; init; }

    public long SizeBytes { get; init; }

    public long FileCount { get; init; }

    /// <summary>Pastas sem permissão de leitura (o tamanho real pode ser maior; ex.: Windows\Temp sem administrador).</summary>
    public int InaccessibleCount { get; init; }

    /// <summary>Algum dos caminhos existe nesta máquina.</summary>
    public bool Found { get; init; }

    public IReadOnlyList<string> ResolvedPaths { get; init; } = [];
}

public interface ISpaceAnalyzer
{
    /// <summary>
    /// Percorre <paramref name="root"/> inteira. <paramref name="solidState"/> escolhe o paralelismo (SSD lê várias pastas
    /// ao mesmo tempo; HDD, uma). Cancelamento lança <see cref="OperationCanceledException"/>.
    /// </summary>
    Task<SpaceAnalysis> AnalyzeAsync(string root, bool solidState, IProgress<SpaceProgress>? progress, CancellationToken cancellationToken);

    /// <summary>Mede os locais conhecidos de temporários e caches (somente leitura).</summary>
    Task<IReadOnlyList<LocationMeasure>> MeasureLocationsAsync(IReadOnlyList<StorageLocation> locations, CancellationToken cancellationToken);
}

/// <summary>Catálogo de temporários e caches medidos. Cada item diz por que costuma ser descartável — sem prometer limpeza.</summary>
public static class StorageLocations
{
    public const string RecycleBinId = "recycle-bin";

    public static IReadOnlyList<StorageLocation> All { get; } =
    [
        new("user-temp", "Temporários do usuário", StorageLocationKind.Temporary,
            "Arquivos que aplicativos e instaladores deixam para trás. Itens em uso não podem ser removidos.",
            [@"%TEMP%"]),
        new("windows-temp", "Temporários do Windows", StorageLocationKind.Temporary,
            "Temporários de serviços e instaladores do sistema.",
            [@"%WINDIR%\Temp"]),
        new("windows-update", "Downloads do Windows Update", StorageLocationKind.Update,
            "Pacotes já baixados pelo Windows Update. O próprio Windows os limpa após instalar.",
            [@"%WINDIR%\SoftwareDistribution\Download"]),
        new("crash-dumps", "Despejos de memória e relatórios de erro", StorageLocationKind.CrashData,
            "Despejos de travamento e relatórios do Relatório de Erros do Windows. Úteis para investigar falhas antes de apagar.",
            [@"%LOCALAPPDATA%\CrashDumps", @"%WINDIR%\Minidump", @"%WINDIR%\MEMORY.DMP", @"%LOCALAPPDATA%\Microsoft\Windows\WER", @"%PROGRAMDATA%\Microsoft\Windows\WER"]),
        new("thumbnail-cache", "Cache de miniaturas do Explorer", StorageLocationKind.Cache,
            "Miniaturas de imagens e vídeos. O Explorer recria conforme as pastas são abertas.",
            [@"%LOCALAPPDATA%\Microsoft\Windows\Explorer"]),
        new("edge-cache", "Cache do Microsoft Edge", StorageLocationKind.Cache,
            "Páginas e scripts guardados para abrir sites mais rápido. Não inclui senhas, favoritos nem histórico.",
            [@"%LOCALAPPDATA%\Microsoft\Edge\User Data\*\Cache", @"%LOCALAPPDATA%\Microsoft\Edge\User Data\*\Code Cache", @"%LOCALAPPDATA%\Microsoft\Edge\User Data\*\GPUCache"]),
        new("chrome-cache", "Cache do Google Chrome", StorageLocationKind.Cache,
            "Páginas e scripts guardados para abrir sites mais rápido. Não inclui senhas, favoritos nem histórico.",
            [@"%LOCALAPPDATA%\Google\Chrome\User Data\*\Cache", @"%LOCALAPPDATA%\Google\Chrome\User Data\*\Code Cache", @"%LOCALAPPDATA%\Google\Chrome\User Data\*\GPUCache"]),
        new(RecycleBinId, "Lixeira", StorageLocationKind.RecycleBin,
            "Arquivos que você já apagou e ainda podem ser restaurados. Esvaziar é decisão sua.",
            []),
    ];
}
