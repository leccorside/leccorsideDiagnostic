using Microsoft.Extensions.DependencyInjection;

namespace LeccorWindowsHealthCenter.Diagnostics;

public static class DependencyInjection
{
    /// <summary>
    /// Ponto único de registro de scanners (<c>IDiagnosticScanner</c>) e regras (<c>IRule</c>).
    /// Os scanners do MVP 1 (Hardware, CPU, RAM, Discos, Processos, Armazenamento, Event Viewer,
    /// Windows Health, Startup) entram a partir do Passo 2 — ver PASSOS.md.
    /// </summary>
    public static IServiceCollection AddHealthCenterDiagnostics(this IServiceCollection services)
    {
        return services;
    }
}
