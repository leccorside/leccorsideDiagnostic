using System.ComponentModel;
using System.Diagnostics;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Monitoring.Native;

namespace LeccorWindowsHealthCenter.Monitoring;

/// <summary>
/// Fotografia dos processos. Memória e contagens vêm da lista do sistema (sem abrir o processo);
/// tempo de CPU, início e caminho exigem abrir o processo e ficam null em processos protegidos.
/// </summary>
public sealed class ProcessSnapshotProvider : IProcessSnapshotProvider
{
    private readonly TimeProvider _timeProvider;

    public ProcessSnapshotProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public ProcessSnapshot Capture(bool includePaths = false)
    {
        var capturedAt = _timeProvider.GetUtcNow();
        var processes = Process.GetProcesses();
        var samples = new List<ProcessSample>(processes.Length);
        foreach (var process in processes)
        {
            using (process)
            {
                try
                {
                    if (process.Id == 0)
                    {
                        continue; // Idle: não é um processo real
                    }

                    samples.Add(new ProcessSample
                    {
                        Pid = process.Id,
                        Name = process.ProcessName,
                        WorkingSetBytes = process.WorkingSet64,
                        PrivateBytes = process.PrivateMemorySize64,
                        ThreadCount = process.Threads.Count,
                        HandleCount = process.HandleCount,
                        // Tipos anuláveis explícitos: processo protegido = null ("desconhecido"), nunca 0.
                        TotalProcessorTime = Try<TimeSpan?>(() => process.TotalProcessorTime),
                        StartTime = Try<DateTimeOffset?>(() => new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero)),
                        ExecutablePath = includePaths ? TryGetPath(process.Id) : null,
                    });
                }
                catch (InvalidOperationException)
                {
                    // O processo terminou durante a leitura.
                }
            }
        }

        return new ProcessSnapshot(capturedAt, samples);
    }

    public string? TryGetExecutablePath(int pid) => TryGetPath(pid);

    public static string? TryGetPath(int pid)
    {
        using var handle = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, pid);
        if (handle.IsInvalid)
        {
            return null;
        }

        var buffer = new char[1024];
        var size = (uint)buffer.Length;
        return NativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : null;
    }

    private static T? Try<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return default;
        }
    }
}
