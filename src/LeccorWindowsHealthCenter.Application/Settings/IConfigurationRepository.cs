namespace LeccorWindowsHealthCenter.Application.Settings;

/// <summary>
/// Repositório para persistência de preferências e configurações do usuário na tabela Configuration do SQLite (seção 66).
/// </summary>
public interface IConfigurationRepository
{
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);

    Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default);
}

