using LeccorWindowsHealthCenter.Application.SystemInfo;

namespace LeccorWindowsHealthCenter.Application.Startup;

public enum StartupImpactLevel
{
    /// <summary>Não está em execução agora: sem medição, sem estimativa (nunca um palpite).</summary>
    NotMeasured,
    Disabled,
    Low,
    Medium,
    High,
}

/// <summary>
/// Impacto estimado (seção 19) pelo que o programa iniciado consome AGORA: memória privada e tempo de CPU dos
/// processos com o mesmo executável. O Gerenciador de Tarefas mede o boot (exige rastreamento com administrador);
/// aqui é o custo de manter o programa aberto desde o logon — a tela diz isso.
/// </summary>
public static class StartupImpact
{
    // Limites: 100 MB privados ≈ um aplicativo Electron leve; 300 MB, um pesado (Teams, Discord). CPU: 5 s ≈ alguns
    // segundos de inicialização; 30 s, trabalho contínuo em segundo plano.
    public const long MediumPrivateBytes = 100L * 1024 * 1024;
    public const long HighPrivateBytes = 300L * 1024 * 1024;
    public static readonly TimeSpan MediumCpu = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan HighCpu = TimeSpan.FromSeconds(30);

    public static StartupImpactLevel Estimate(StartupEntry entry, IEnumerable<ProcessDetails> processes)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Enabled == false)
        {
            return StartupImpactLevel.Disabled;
        }

        if (entry.ExecutablePath is not { } exe)
        {
            return StartupImpactLevel.NotMeasured;
        }

        var running = processes.Where(p => string.Equals(p.ExecutablePath, exe, StringComparison.OrdinalIgnoreCase)).ToList();
        if (running.Count == 0)
        {
            return StartupImpactLevel.NotMeasured;
        }

        var memory = running.Sum(p => p.PrivateBytes ?? 0);
        var cpu = running.Aggregate(TimeSpan.Zero, (sum, p) => sum + (p.TotalProcessorTime ?? TimeSpan.Zero));
        return memory >= HighPrivateBytes || cpu >= HighCpu ? StartupImpactLevel.High
            : memory >= MediumPrivateBytes || cpu >= MediumCpu ? StartupImpactLevel.Medium
            : StartupImpactLevel.Low;
    }

    public static string Text(StartupImpactLevel level) => level switch
    {
        StartupImpactLevel.High => "Alto",
        StartupImpactLevel.Medium => "Médio",
        StartupImpactLevel.Low => "Baixo",
        StartupImpactLevel.Disabled => "Desativado",
        _ => "não medido (não está em execução)",
    };
}
