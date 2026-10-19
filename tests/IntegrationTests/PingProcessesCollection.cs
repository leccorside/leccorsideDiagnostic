namespace LeccorWindowsHealthCenter.IntegrationTests;

/// <summary>
/// Testes que iniciam PING.EXE não rodam em paralelo: <c>Timeout_kills_the_process_tree</c> confere que nenhum PING novo
/// sobrou, e o PING de outro teste rodando ao mesmo tempo o faria falhar.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PingProcessesCollection
{
    public const string Name = "Processos PING";
}
