using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Application.Scanning;

/// <summary>
/// Scanner de diagnóstico (seção 10). Um scanner apenas COLETA métricas e observações —
/// nunca altera o sistema e nunca decide sozinho o que é problema (isso é papel do RuleEngine).
/// </summary>
public interface IDiagnosticScanner
{
    /// <summary>Identificador estável, ex.: "cpu", "eventlog". Usado por regras, banco e logs.</summary>
    string Id { get; }

    /// <summary>Nome exibido na UI, ex.: "CPU".</summary>
    string Name { get; }

    ScanModule Module { get; }

    /// <summary>Participa da varredura rápida (seção 9).</summary>
    bool IncludedInQuickScan { get; }

    /// <summary>Ordem de execução/exibição (seção 73: Hardware → CPU → RAM → ...).</summary>
    int Order { get; }

    Task<DiagnosticResult> ScanAsync(CancellationToken cancellationToken);
}
