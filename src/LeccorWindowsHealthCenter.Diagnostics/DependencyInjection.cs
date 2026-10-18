using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Diagnostics.Hardware;
using LeccorWindowsHealthCenter.Diagnostics.Performance;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeccorWindowsHealthCenter.Diagnostics;

public static class DependencyInjection
{
    /// <summary>
    /// Ponto único de registro de scanners (<see cref="IDiagnosticScanner"/>) e regras (<see cref="IRule"/>).
    /// Catálogo e documentação: docs/DIAGNOSTICS.md e docs/RULE_ENGINE.md.
    /// </summary>
    public static IServiceCollection AddHealthCenterDiagnostics(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SamplingOptions>(configuration.GetSection(SamplingOptions.SectionName));

        // Passo 2 — MVP 1 (a): Hardware, CPU, Memória e GPU
        services.AddSingleton<IDiagnosticScanner, HardwareScanner>();
        services.AddSingleton<IDiagnosticScanner, CpuScanner>();
        services.AddSingleton<IDiagnosticScanner, MemoryScanner>();
        services.AddSingleton<IDiagnosticScanner, GpuScanner>();

        services.AddSingleton<IRule, CpuSustainedHighRule>();
        services.AddSingleton<IRule, CpuThrottlingRule>();
        services.AddSingleton<IRule, MemoryPressureRule>();
        services.AddSingleton<IRule, CommitChargeHighRule>();
        services.AddSingleton<IRule, PossibleMemoryLeakRule>();
        return services;
    }
}
