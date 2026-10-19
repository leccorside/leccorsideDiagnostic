namespace LeccorWindowsHealthCenter.Application.Startup;

public enum StartupSourceKind
{
    RegistryRun,
    RegistryRunOnce,
    StartupFolder,
    ScheduledTask,
    Service,
}

/// <summary>
/// Algo que o Windows inicia sozinho no logon/boot (seção 19). Somente leitura neste passo: desativar com rollback
/// chega no Passo 8 (RepairEngine).
/// </summary>
public sealed record StartupEntry
{
    /// <summary>Identificador estável (fonte + local + nome) para rastrear a entrada entre leituras e findings.</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required StartupSourceKind Source { get; init; }

    /// <summary>Onde a entrada está: chave de registro, pasta, caminho da tarefa ou nome do serviço.</summary>
    public required string Location { get; init; }

    /// <summary>Vale só para o usuário atual (HKCU, pasta do usuário) ou para todos.</summary>
    public bool CurrentUserOnly { get; init; }

    /// <summary>Linha de comando como está registrada.</summary>
    public string? Command { get; init; }

    /// <summary>Executável interpretado da linha de comando (variáveis expandidas). Null quando não deu para interpretar.</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>O executável existe no disco. Null quando não há caminho para conferir.</summary>
    public bool? ExecutableExists { get; init; }

    /// <summary>Ativo (o Windows inicia). Null quando o estado não pôde ser lido.</summary>
    public bool? Enabled { get; init; }

    /// <summary>Quem desativou, quando conhecido ("Gerenciador de Tarefas", "tarefa desativada", "serviço manual"…).</summary>
    public string? StateDetail { get; init; }

    /// <summary>
    /// Parte do próprio Windows (executável na pasta do Windows ou tarefa em \Microsoft\Windows\). A tela os esconde
    /// por padrão: desativá-los raramente ajuda e pode quebrar recursos do sistema.
    /// </summary>
    public bool IsWindowsComponent { get; init; }
}

public sealed record StartupInventory
{
    public IReadOnlyList<StartupEntry> Entries { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}

public interface IStartupInventoryProvider
{
    /// <summary>Lê todas as fontes (cache de 30 s; <paramref name="forceRefresh"/> ignora o cache).</summary>
    Task<StartupInventory> GetAsync(CancellationToken cancellationToken, bool forceRefresh = false);
}
