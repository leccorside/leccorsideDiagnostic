using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Monitoring;

/// <summary>
/// Amostragem leve para o dashboard: CPU por GetSystemTimes, memória por GlobalMemoryStatusEx, disco do sistema
/// por DriveInfo e GPU pelos contadores "GPU Engine" (PDH, criados só na primeira leitura).
/// Chamado apenas com o Dashboard visível — custo medido &lt; 1% de CPU (seção 58).
/// </summary>
public sealed class LiveMetricsProvider : ILiveMetricsProvider, IDisposable
{
    private readonly CpuUsageMeter _cpu = new();
    private readonly Lazy<PdhPerformanceCounterSession> _gpu;

    public LiveMetricsProvider(ILogger<PdhPerformanceCounterSession> logger)
    {
        _gpu = new Lazy<PdhPerformanceCounterSession>(() => new PdhPerformanceCounterSession(logger, includeCpuAndMemory: false, includeGpu: true));
    }

    public LiveSystemMetrics Sample()
    {
        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\') ?? "C:";
        var memory = SystemMemory.Read();

        return new LiveSystemMetrics
        {
            CpuPercent = _cpu.Sample(),
            MemoryPercent = memory?.UsedPercent,
            MemoryTotalBytes = memory?.TotalPhysical,
            MemoryAvailableBytes = memory?.AvailablePhysical,
            SystemDrive = systemDrive,
            SystemDrivePercent = ReadDrive(systemDrive),
            GpuPercent = _gpu.Value.Read().GpuPercent,
        };
    }

    public void Dispose()
    {
        if (_gpu.IsValueCreated)
        {
            _gpu.Value.Dispose();
        }
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
}
