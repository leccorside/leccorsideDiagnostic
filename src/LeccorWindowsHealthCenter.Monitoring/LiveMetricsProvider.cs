using System.Runtime.InteropServices;
using LeccorWindowsHealthCenter.Application.SystemInfo;

namespace LeccorWindowsHealthCenter.Monitoring;

/// <summary>
/// Amostragem leve para o dashboard usando apenas chamadas Win32 baratas (GetSystemTimes,
/// GlobalMemoryStatusEx) — sem WMI/PerformanceCounter no loop, para manter CPU ociosa &lt; 1% (seção 58).
/// GPU fica "não disponível" até o scanner de GPU (MVP 1) fornecer uma fonte confiável.
/// </summary>
public sealed class LiveMetricsProvider : ILiveMetricsProvider
{
    private readonly object _gate = new();
    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private bool _hasBaseline;

    public LiveSystemMetrics Sample()
    {
        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\') ?? "C:";
        var (memPercent, total, available) = ReadMemory();

        return new LiveSystemMetrics
        {
            CpuPercent = ReadCpu(),
            MemoryPercent = memPercent,
            MemoryTotalBytes = total,
            MemoryAvailableBytes = available,
            SystemDrive = systemDrive,
            SystemDrivePercent = ReadDrive(systemDrive),
            GpuPercent = null,
        };
    }

    private double? ReadCpu()
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
                return null; // primeira leitura não tem delta: "não disponível" é mais honesto que 0%.
            }

            var idleDelta = idle - _lastIdle;
            var totalDelta = (kernel - _lastKernel) + (user - _lastUser); // kernel inclui idle
            (_lastIdle, _lastKernel, _lastUser) = (idle, kernel, user);

            if (totalDelta == 0)
            {
                return null;
            }

            return Math.Clamp((1.0 - (idleDelta / (double)totalDelta)) * 100.0, 0, 100);
        }
    }

    private static (double? Percent, long? Total, long? Available) ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!NativeMethods.GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
        {
            return (null, null, null);
        }

        var used = status.TotalPhys - status.AvailPhys;
        return (used * 100.0 / status.TotalPhys, (long)status.TotalPhys, (long)status.AvailPhys);
    }

    private static double? ReadDrive(string drive)
    {
        try
        {
            var info = new DriveInfo(drive);
            if (!info.IsReady || info.TotalSize == 0)
            {
                return null;
            }

            return (info.TotalSize - info.TotalFreeSpace) * 100.0 / info.TotalSize;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;

        public readonly ulong ToUInt64() => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
    }
}
