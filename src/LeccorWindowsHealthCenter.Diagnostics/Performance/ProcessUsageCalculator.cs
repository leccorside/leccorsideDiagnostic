using LeccorWindowsHealthCenter.Application.SystemInfo;

namespace LeccorWindowsHealthCenter.Diagnostics.Performance;

public sealed record ProcessCpuUsage(int Pid, string Name, double CpuPercent, string? ExecutablePath);

/// <summary>Cálculos sobre fotografias de processos (puros e testáveis).</summary>
public static class ProcessUsageCalculator
{
    /// <summary>
    /// Uso de CPU de cada processo entre duas fotografias, em % da capacidade total da máquina
    /// (100% = todos os processadores lógicos ocupados). Só considera processos presentes nas duas
    /// fotografias com o mesmo horário de início (evita confundir PIDs reutilizados) e com tempo de CPU legível.
    /// </summary>
    public static IReadOnlyList<ProcessCpuUsage> TopByCpu(ProcessSnapshot start, ProcessSnapshot end, int logicalProcessors, int top)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);
        var elapsed = end.CapturedAt - start.CapturedAt;
        if (elapsed <= TimeSpan.Zero || logicalProcessors <= 0)
        {
            return [];
        }

        var before = start.Processes
            .Where(p => p.TotalProcessorTime is not null)
            .GroupBy(p => p.Pid)
            .ToDictionary(g => g.Key, g => g.First());

        var capacity = elapsed.TotalMilliseconds * logicalProcessors;
        return end.Processes
            .Where(p => p.TotalProcessorTime is not null && before.TryGetValue(p.Pid, out var b) && b.StartTime == p.StartTime)
            .Select(p =>
            {
                var delta = p.TotalProcessorTime!.Value - before[p.Pid].TotalProcessorTime!.Value;
                return new ProcessCpuUsage(p.Pid, p.Name, Math.Clamp(delta.TotalMilliseconds / capacity * 100, 0, 100), p.ExecutablePath);
            })
            .Where(u => u.CpuPercent > 0)
            .OrderByDescending(u => u.CpuPercent)
            .Take(top)
            .ToList();
    }

    public static IReadOnlyList<ProcessSample> TopByPrivateMemory(ProcessSnapshot snapshot, int top) =>
        snapshot.Processes
            .Where(p => p.PrivateBytes is > 0)
            .OrderByDescending(p => p.PrivateBytes)
            .Take(top)
            .ToList();
}
