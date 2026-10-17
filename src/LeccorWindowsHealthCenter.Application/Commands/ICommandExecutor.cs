namespace LeccorWindowsHealthCenter.Application.Commands;

/// <summary>
/// Único ponto da aplicação autorizado a executar processos externos (seção 23).
/// Argumentos são passados como lista (ArgumentList) — nunca concatenados em uma string de comando.
/// </summary>
public interface ICommandExecutor
{
    Task<CommandResult> ExecuteAsync(CommandRequest request, CancellationToken cancellationToken);
}

public sealed record CommandRequest
{
    /// <summary>Caminho absoluto do executável. Validado contra a lista de diretórios confiáveis.</summary>
    public required string ExecutablePath { get; init; }

    /// <summary>Cada argumento é um item separado. Não use aspas nem concatene entrada do usuário.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    public bool RequiresElevation { get; init; }

    /// <summary>Descrição para log/UI (modo avançado), ex.: "Verificação do Component Store".</summary>
    public string? Purpose { get; init; }

    /// <summary>Limite de bytes capturados de stdout/stderr (proteção de memória).</summary>
    public int MaxOutputChars { get; init; } = 2_000_000;
}

public sealed record CommandResult
{
    public required string CommandLine { get; init; }

    public required int? ExitCode { get; init; }

    public required string StandardOutput { get; init; }

    public required string StandardError { get; init; }

    public required TimeSpan Duration { get; init; }

    public bool TimedOut { get; init; }

    public bool Cancelled { get; init; }

    public bool OutputTruncated { get; init; }

    public bool Succeeded => !TimedOut && !Cancelled && ExitCode == 0;
}

/// <summary>Lançada quando a requisição viola as políticas de segurança do executor.</summary>
public sealed class CommandRejectedException : Exception
{
    public CommandRejectedException(string message)
        : base(message)
    {
    }

    public CommandRejectedException()
    {
    }

    public CommandRejectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
