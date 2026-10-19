using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using static LeccorWindowsHealthCenter.Infrastructure.SystemInfo.ProcessNativeMethods;

namespace LeccorWindowsHealthCenter.Infrastructure.SystemInfo;

/// <summary>
/// Gerenciador de processos (seção 18) sem administrador: lista e pai pelo Toolhelp, memória/threads/handles pela lista do
/// sistema, caminho/tempos/E-S/usuário abrindo o processo só para consulta limitada. Campo inacessível = null.
/// </summary>
public sealed class ProcessInventoryProvider : IProcessInventoryProvider
{
    private readonly ILogger<ProcessInventoryProvider> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, string?> _accounts = new(StringComparer.Ordinal);

    public ProcessInventoryProvider(ILogger<ProcessInventoryProvider> logger, TimeProvider timeProvider)
    {
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public ProcessInventorySnapshot Capture()
    {
        var capturedAt = _timeProvider.GetUtcNow();
        var parents = ReadParents();
        var result = new List<ProcessDetails>(parents.Count + 16);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == 0)
                    {
                        continue; // Idle não é um processo real.
                    }

                    result.Add(Describe(process, parents));
                }
                catch (InvalidOperationException)
                {
                    // Terminou durante a leitura.
                }
            }
        }

        return new ProcessInventorySnapshot(capturedAt, result);
    }

    public TerminateOutcome Terminate(int pid, string name, DateTimeOffset? expectedStart)
    {
        if (CriticalProcesses.IsCritical(pid, name))
        {
            return TerminateOutcome.Protected;
        }

        using var handle = OpenProcess(ProcessTerminate | ProcessQueryLimitedInformation, false, pid);
        if (handle.IsInvalid)
        {
            return Marshal.GetLastWin32Error() switch
            {
                ErrorAccessDenied => TerminateOutcome.AccessDenied,
                ErrorInvalidParameter => TerminateOutcome.NotFound,
                _ => TerminateOutcome.Failed,
            };
        }

        // O PID pode ter sido reaproveitado entre a fotografia e o clique: só encerra se o início confere.
        if (expectedStart is { } expected && StartOf(handle) is { } actual && Math.Abs((actual - expected).TotalSeconds) > 1)
        {
            return TerminateOutcome.NotFound;
        }

        if (!TerminateProcess(handle, 1))
        {
            return Marshal.GetLastWin32Error() == ErrorAccessDenied ? TerminateOutcome.AccessDenied : TerminateOutcome.Failed;
        }

        _logger.LogInformation("Processo {Name} (PID {Pid}) encerrado a pedido do usuário", name, pid);
        return TerminateOutcome.Ended;
    }

    private ProcessDetails Describe(Process process, Dictionary<int, int> parents)
    {
        var pid = process.Id;
        string? path = null;
        DateTimeOffset? start = null;
        TimeSpan? cpu = null;
        long? io = null;
        string? user = null;

        using (var handle = OpenProcess(ProcessQueryLimitedInformation, false, pid))
        {
            if (!handle.IsInvalid)
            {
                path = ImagePath(handle);
                start = StartOf(handle);
                if (GetProcessTimes(handle, out _, out _, out var kernel, out var userTime))
                {
                    cpu = TimeSpan.FromTicks(kernel.ToInt64() + userTime.ToInt64());
                }

                if (GetProcessIoCounters(handle, out var counters))
                {
                    io = (long)(counters.ReadTransferCount + counters.WriteTransferCount + counters.OtherTransferCount);
                }

                user = Owner(handle);
            }
        }

        return new ProcessDetails
        {
            Pid = pid,
            ParentPid = parents.TryGetValue(pid, out var parent) ? parent : 0,
            Name = process.ProcessName,
            ExecutablePath = path,
            User = user,
            SessionId = ProcessIdToSessionId((uint)pid, out var session) ? (int)session : null,
            PrivateBytes = process.PrivateMemorySize64,
            WorkingSetBytes = process.WorkingSet64,
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount,
            TotalProcessorTime = cpu,
            StartTime = start,
            IoBytes = io,
        };
    }

    private Dictionary<int, int> ReadParents()
    {
        var parents = new Dictionary<int, int>();
        using var snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snapshot.IsInvalid)
        {
            _logger.LogWarning("Toolhelp indisponível: árvore de processos sem pais (erro {Error})", Marshal.GetLastWin32Error());
            return parents;
        }

        var entry = new ProcessEntry32 { Size = (uint)Marshal.SizeOf<ProcessEntry32>() };
        for (var ok = Process32First(snapshot, ref entry); ok; ok = Process32Next(snapshot, ref entry))
        {
            parents[(int)entry.ProcessId] = (int)entry.ParentProcessId;
        }

        return parents;
    }

    private static string? ImagePath(SafeProcessHandle handle)
    {
        var buffer = new char[1024];
        var size = (uint)buffer.Length;
        return QueryFullProcessImageName(handle, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : null;
    }

    private static DateTimeOffset? StartOf(SafeProcessHandle handle) =>
        GetProcessTimes(handle, out var creation, out _, out _, out _) && creation.ToInt64() > 0
            ? new DateTimeOffset(DateTime.FromFileTimeUtc(creation.ToInt64()), TimeSpan.Zero)
            : null;

    /// <summary>Dono pelo token (TokenUser → SID → DOMÍNIO\usuário), com cache por SID.</summary>
    private string? Owner(SafeProcessHandle handle)
    {
        if (!OpenProcessToken(handle, TokenQuery, out var token))
        {
            return null;
        }

        using (token)
        {
            GetTokenInformation(token, TokenUserClass, IntPtr.Zero, 0, out var length);
            if (length <= 0)
            {
                return null;
            }

            var buffer = Marshal.AllocHGlobal(length);
            try
            {
                if (!GetTokenInformation(token, TokenUserClass, buffer, length, out _))
                {
                    return null;
                }

                // TOKEN_USER começa com SID_AND_ATTRIBUTES: o primeiro campo é o ponteiro para o SID.
                var sid = new SecurityIdentifier(Marshal.ReadIntPtr(buffer));
                return _accounts.GetOrAdd(sid.Value, _ => Translate(sid));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private static string? Translate(SecurityIdentifier sid)
    {
        try
        {
            return sid.Translate(typeof(NTAccount)).Value;
        }
        catch (IdentityNotMappedException)
        {
            return sid.Value;
        }
    }
}
