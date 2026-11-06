namespace LeccorWindowsHealthCenter.Application.SystemInfo;

/// <summary>
/// Discriminação dos bytes de E/S acumulados por processo entre disco e rede (seção 18 do PROMPT e Pacote A).
/// </summary>
public sealed record ProcessIoBreakdown(long DiskIoBytes, long NetworkIoBytes);

/// <summary>
/// Rastreador ETW (Event Tracing for Windows) para separar E/S de disco e rede no nível de processo.
/// </summary>
public interface IProcessIoTracer : IDisposable
{
    /// <summary>Indica se a sessão ETW está ativa e coletando eventos do kernel.</summary>
    bool IsActive { get; }

    /// <summary>Tenta iniciar a sessão de rastreamento ETW de kernel.</summary>
    bool Start();

    /// <summary>Encerra a sessão de rastreamento ETW.</summary>
    void Stop();

    /// <summary>Obtém a discriminação de E/S acumulada para o processo informado.</summary>
    ProcessIoBreakdown? GetProcessIo(int processId);
}

