namespace LeccorWindowsHealthCenter.Application.SystemInfo;

/// <summary>
/// Processo com tudo o que dá para ler sem administrador (seção 18). Null = protegido ou já terminou — nunca 0
/// inventado. CPU e E/S por segundo são calculados pela tela entre duas fotografias.
/// </summary>
public sealed record ProcessDetails
{
    public required int Pid { get; init; }

    /// <summary>Processo pai (pode já ter terminado; o PID pode até ter sido reaproveitado — ver <see cref="StartTime"/>).</summary>
    public int ParentPid { get; init; }

    public required string Name { get; init; }

    public string? ExecutablePath { get; init; }

    /// <summary>DOMÍNIO\usuário dono do processo; null quando o token não pode ser lido (processo de outro usuário/protegido).</summary>
    public string? User { get; init; }

    public int? SessionId { get; init; }

    public long? PrivateBytes { get; init; }

    public long? WorkingSetBytes { get; init; }

    public int? ThreadCount { get; init; }

    public int? HandleCount { get; init; }

    public TimeSpan? TotalProcessorTime { get; init; }

    public DateTimeOffset? StartTime { get; init; }

    /// <summary>
    /// Bytes de E/S acumulados (leitura + escrita + outros: GetProcessIoCounters). Inclui disco, rede e dispositivos —
    /// o Windows não separa por tipo sem rastreamento ETW.
    /// </summary>
    public long? IoBytes { get; init; }
}

public sealed record ProcessInventorySnapshot(DateTimeOffset CapturedAt, IReadOnlyList<ProcessDetails> Processes);

/// <summary>Taxas de um processo entre duas fotografias. Null = sem leitura nas duas (protegido, recém-iniciado).</summary>
public readonly record struct ProcessRate(double? CpuPercent, double? IoBytesPerSecond);

/// <summary>Cálculo puro das taxas (testável): o mesmo processo é o mesmo PID com o mesmo horário de início.</summary>
public static class ProcessRates
{
    /// <summary>
    /// CPU em % da capacidade total da máquina (100% = todos os processadores lógicos), como o Gerenciador de Tarefas;
    /// E/S em bytes por segundo.
    /// </summary>
    public static Dictionary<int, ProcessRate> Compute(ProcessInventorySnapshot previous, ProcessInventorySnapshot current, int logicalProcessors)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        var result = new Dictionary<int, ProcessRate>();
        var seconds = (current.CapturedAt - previous.CapturedAt).TotalSeconds;
        if (seconds <= 0 || logicalProcessors <= 0)
        {
            return result;
        }

        var before = previous.Processes.GroupBy(p => p.Pid).ToDictionary(g => g.Key, g => g.First());
        foreach (var process in current.Processes)
        {
            if (!before.TryGetValue(process.Pid, out var old) || old.StartTime != process.StartTime)
            {
                continue; // PID novo ou reaproveitado: sem base para a taxa.
            }

            double? cpu = process.TotalProcessorTime is { } now && old.TotalProcessorTime is { } then
                ? Math.Clamp((now - then).TotalSeconds / (seconds * logicalProcessors) * 100, 0, 100)
                : null;
            double? io = process.IoBytes is { } ioNow && old.IoBytes is { } ioThen
                ? Math.Max(0, (ioNow - ioThen) / seconds)
                : null;
            result[process.Pid] = new ProcessRate(cpu, io);
        }

        return result;
    }
}

public enum TerminateOutcome
{
    Ended,

    /// <summary>Processo crítico do Windows: o app recusa encerrar (seção 18).</summary>
    Protected,

    /// <summary>Sem permissão (processo de outro usuário, elevado ou protegido pelo sistema).</summary>
    AccessDenied,

    /// <summary>Já terminou, ou o PID agora é de outro processo (início diferente do mostrado na tela).</summary>
    NotFound,

    Failed,
}

public interface IProcessInventoryProvider
{
    ProcessInventorySnapshot Capture();

    /// <summary>
    /// Encerra o processo. <paramref name="expectedStart"/> evita matar outro processo que reaproveitou o PID entre a
    /// fotografia e o clique. Processos críticos (<see cref="CriticalProcesses"/>) são recusados antes de qualquer chamada.
    /// </summary>
    TerminateOutcome Terminate(int pid, string name, DateTimeOffset? expectedStart);
}

/// <summary>
/// Processos do Windows cujo encerramento derruba a sessão, trava o sistema ou desliga a proteção. A tela recusa
/// encerrá-los (seção 18: "proteção contra encerramento acidental"). Explorer não está na lista: encerrá-lo é seguro
/// e às vezes útil, mas a tela avisa que a barra de tarefas some até ele voltar.
/// </summary>
public static class CriticalProcesses
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Registry", "Secure System", "Memory Compression", "MemCompression", "smss", "csrss", "wininit",
        "winlogon", "services", "lsass", "LsaIso", "svchost", "fontdrvhost", "dwm", "MsMpEng", "NisSrv",
        "SecurityHealthService", "sihost", "LogonUI",
    };

    /// <summary>Verdadeiro para PID 0/4 e para os nomes da lista (sem ".exe").</summary>
    public static bool IsCritical(int pid, string name) =>
        pid is 0 or 4 || Names.Contains(StripExe(name));

    /// <summary>Por que não pode ser encerrado — em linguagem de usuário.</summary>
    public static string Reason(string name) => StripExe(name).ToUpperInvariant() switch
    {
        "CSRSS" or "WININIT" or "SMSS" or "SERVICES" or "LSASS" or "LSAISO" => "É parte do núcleo do Windows: encerrá-lo trava ou reinicia o computador.",
        "WINLOGON" or "LOGONUI" => "Controla a sessão de logon: encerrá-lo desconecta você do Windows.",
        "SVCHOST" => "Hospeda serviços do Windows; encerrá-lo derruba vários serviços de uma vez (rede, áudio, atualizações).",
        "DWM" => "Desenha todas as janelas: encerrá-lo deixa a tela piscando ou preta até o Windows reiniciá-lo.",
        "MSMPENG" or "NISSRV" or "SECURITYHEALTHSERVICE" => "É a proteção do Windows Defender: o próprio Windows impede o encerramento.",
        _ => "É um processo essencial do Windows; encerrá-lo pode deixar o sistema instável.",
    };

    private static string StripExe(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
}
