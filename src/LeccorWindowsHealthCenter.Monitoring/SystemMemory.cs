using System.Runtime.InteropServices;
using LeccorWindowsHealthCenter.Monitoring.Native;

namespace LeccorWindowsHealthCenter.Monitoring;

/// <summary>Estado de memória do sistema (GlobalMemoryStatusEx + GetPerformanceInfo), em bytes.</summary>
public sealed record SystemMemoryStatus(
    long TotalPhysical,
    long AvailablePhysical,
    long CommitTotal,
    long CommitLimit,
    long SystemCache,
    long PagedPool,
    long NonPagedPool)
{
    public double UsedPercent => TotalPhysical == 0 ? 0 : (TotalPhysical - AvailablePhysical) * 100.0 / TotalPhysical;

    public double CommitPercent => CommitLimit == 0 ? 0 : CommitTotal * 100.0 / CommitLimit;
}

public static class SystemMemory
{
    /// <summary>Lê o estado atual; null se as APIs falharem (nunca devolve zeros inventados).</summary>
    public static SystemMemoryStatus? Read()
    {
        var status = new NativeMethods.MemoryStatusEx { Length = (uint)Marshal.SizeOf<NativeMethods.MemoryStatusEx>() };
        if (!NativeMethods.GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
        {
            return null;
        }

        if (!NativeMethods.GetPerformanceInfo(out var perf, (uint)Marshal.SizeOf<NativeMethods.PerformanceInformation>()))
        {
            return null;
        }

        var page = (long)perf.PageSize.ToUInt64();
        return new SystemMemoryStatus(
            (long)status.TotalPhys,
            (long)status.AvailPhys,
            (long)perf.CommitTotal.ToUInt64() * page,
            (long)perf.CommitLimit.ToUInt64() * page,
            (long)perf.SystemCache.ToUInt64() * page,
            (long)perf.KernelPaged.ToUInt64() * page,
            (long)perf.KernelNonpaged.ToUInt64() * page);
    }
}

/// <summary>
/// Uso total de CPU por diferença de GetSystemTimes entre duas chamadas. Cada consumidor tem a sua instância
/// (dashboard e histórico não compartilham a linha de base). A primeira leitura devolve null.
/// </summary>
public sealed class CpuUsageMeter
{
    private readonly object _gate = new();
    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private bool _hasBaseline;

    public double? Sample()
    {
        if (!NativeMethods.GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            return null;
        }

        var idle = idleTime.ToUInt64();
        var kernel = kernelTime.ToUInt64();
        var user = userTime.ToUInt64();

        lock (_gate)
        {
            if (!_hasBaseline)
            {
                (_lastIdle, _lastKernel, _lastUser, _hasBaseline) = (idle, kernel, user, true);
                return null;
            }

            var idleDelta = idle - _lastIdle;
            var totalDelta = (kernel - _lastKernel) + (user - _lastUser); // kernel inclui idle
            (_lastIdle, _lastKernel, _lastUser) = (idle, kernel, user);
            return totalDelta == 0 ? null : Math.Clamp((1.0 - (idleDelta / (double)totalDelta)) * 100.0, 0, 100);
        }
    }
}
