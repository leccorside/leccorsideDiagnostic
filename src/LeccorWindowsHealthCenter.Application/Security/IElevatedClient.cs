using LeccorWindowsHealthCenter.Application.Commands;
using LeccorWindowsHealthCenter.Shared.Ipc;

namespace LeccorWindowsHealthCenter.Application.Security;

/// <summary>
/// Cliente IPC para o processo elevado (seção 64 do PROMPT). Permite executar operações
/// que requerem privilégios de administrador de forma autenticada e limitada.
/// </summary>
public interface IElevatedClient
{
    /// <summary>Informa se o serviço elevado está acessível (ou se o processo atual já tem privilégios de administrador).</summary>
    bool IsElevatedOrAvailable { get; }

    /// <summary>Executa um comando permitido com privilégios de administrador.</summary>
    Task<CommandResult> ExecuteCommandAsync(CommandRequest request, CancellationToken cancellationToken);

    /// <summary>Consulta a telemetria avançada de confiabilidade e desgaste dos discos físicos.</summary>
    Task<IReadOnlyList<ElevatedStorageReliabilityDto>> QueryStorageReliabilityAsync(CancellationToken cancellationToken);

    /// <summary>Encerra um processo com privilégios de administrador (conferindo integridade e recusa de processos críticos).</summary>
    Task<bool> TerminateProcessAsync(int processId, string executableName, DateTimeOffset? expectedStartTime, CancellationToken cancellationToken);
}

/// <summary>Lançada quando a elevação administrativa é recusada pelo usuário no prompt UAC.</summary>
public sealed class ElevationCancelledException : OperationCanceledException
{
    public ElevationCancelledException(string message) : base(message) { }

    public ElevationCancelledException() : base("A solicitação de elevação administrativa foi cancelada pelo usuário.") { }

    public ElevationCancelledException(string message, Exception innerException) : base(message, innerException) { }
}
