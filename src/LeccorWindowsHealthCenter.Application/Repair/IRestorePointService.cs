namespace LeccorWindowsHealthCenter.Application.Repair;

/// <summary>
/// Resultado da tentativa de criação de um Ponto de Restauração nativo do Windows (seção 38 do PROMPT).
/// </summary>
public sealed record RestorePointResult(bool Success, string Message, long? SequenceNumber = null);

/// <summary>
/// Serviço de gerenciamento de Ponto de Restauração nativo do Windows.
/// Permite criar checkpoints antes de alterações no sistema ou a pedido do usuário.
/// </summary>
public interface IRestorePointService
{
    /// <summary>
    /// Verifica se o serviço de Restauração do Sistema está habilitado e acessível na unidade do sistema.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Cria um Ponto de Restauração nativo do Windows com a descrição fornecida.
    /// </summary>
    Task<RestorePointResult> CreateRestorePointAsync(string description, CancellationToken cancellationToken);
}

