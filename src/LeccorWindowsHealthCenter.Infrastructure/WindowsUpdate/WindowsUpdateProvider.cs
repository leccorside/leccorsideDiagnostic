using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Management;
using LeccorWindowsHealthCenter.Application.WindowsUpdate;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LeccorWindowsHealthCenter.Infrastructure.WindowsUpdate;

public sealed class WindowsUpdateProvider : IWindowsUpdateProvider
{
    private readonly ILogger<WindowsUpdateProvider> _logger;

    public WindowsUpdateProvider(ILogger<WindowsUpdateProvider> logger)
    {
        _logger = logger;
    }

    public async Task<WindowsUpdateStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var serviceStatus = ReadServiceStatus();
            var rebootInfo = CheckRebootPending();
            var scanInfo = ReadLastScanAndInstallTimes();
            var recentErrors = ReadRecentUpdateErrors();

            return new WindowsUpdateStatus
            {
                IsRebootPending = rebootInfo.IsPending,
                RebootReason = rebootInfo.Reason,
                LastSuccessfulScanTime = scanInfo.LastScan,
                LastSuccessfulInstallTime = scanInfo.LastInstall,
                PendingUpdatesCount = rebootInfo.IsPending ? 1 : 0,
                PendingUpdateTitles = rebootInfo.IsPending ? ["Atualizações do Windows aguardando reinicialização para conclusão"] : [],
                RecentErrorCount24h = recentErrors.Count,
                RecentErrorMessages = recentErrors,
                ServiceStatus = serviceStatus,
            };
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<UpdateHistoryItem>> GetHistoryAsync(CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var history = new List<UpdateHistoryItem>();

            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\cimv2", "SELECT HotFixID, Description, InstalledOn, InstalledBy FROM Win32_QuickFixEngineering");
                using var collection = searcher.Get();

                foreach (ManagementObject mo in collection)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var hotFixId = mo["HotFixID"]?.ToString();
                    if (string.IsNullOrWhiteSpace(hotFixId))
                    {
                        continue;
                    }

                    var description = mo["Description"]?.ToString();
                    var installedBy = mo["InstalledBy"]?.ToString();
                    DateTimeOffset? installedOn = null;

                    var rawDate = mo["InstalledOn"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(rawDate))
                    {
                        if (DateTime.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ||
                            DateTime.TryParse(rawDate, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt))
                        {
                            installedOn = new DateTimeOffset(dt, TimeSpan.Zero);
                        }
                    }

                    history.Add(new UpdateHistoryItem
                    {
                        HotFixId = hotFixId,
                        Description = description,
                        InstalledOn = installedOn,
                        InstalledBy = installedBy,
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao consultar histórico de atualizações via Win32_QuickFixEngineering");
            }

            return history.OrderByDescending(h => h.InstalledOn ?? DateTimeOffset.MinValue).ToList();
        }, cancellationToken);
    }

    private static WindowsUpdateServiceStatus ReadServiceStatus()
    {
        var wuauserv = ReadServiceStartMode("wuauserv");
        var bits = ReadServiceStartMode("bits");
        var dosvc = ReadServiceStartMode("dosvc");

        return new WindowsUpdateServiceStatus
        {
            WuauservState = wuauserv,
            BitsState = bits,
            DosvcState = dosvc,
        };
    }

    private static string ReadServiceStartMode(string serviceName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            if (key == null)
            {
                return "NotInstalled";
            }

            var start = key.GetValue("Start");
            return start switch
            {
                2 => "Automatic",
                3 => "Manual",
                4 => "Disabled",
                _ => "Unknown",
            };
        }
        catch
        {
            return "Unknown";
        }
    }

    private static (bool IsPending, string? Reason) CheckRebootPending()
    {
        try
        {
            // 1. CBS RebootPending
            using var cbsKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            if (cbsKey != null)
            {
                return (true, "Component Based Servicing (CBS) possui pacotes pendentes de reinicialização.");
            }

            // 2. WindowsUpdate RebootRequired
            using var wuKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            if (wuKey != null)
            {
                return (true, "O Windows Update requer reinicialização para concluir a instalação de atualizações.");
            }

            // 3. PendingFileRenameOperations
            using var smKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            var pendingOps = smKey?.GetValue("PendingFileRenameOperations");
            if (pendingOps is string[] arr && arr.Length > 0)
            {
                return (true, "Operações de renomeação ou substituição de arquivos do sistema estão pendentes no boot.");
            }
        }
        catch
        {
        }

        return (false, null);
    }

    private static (DateTimeOffset? LastScan, DateTimeOffset? LastInstall) ReadLastScanAndInstallTimes()
    {
        DateTimeOffset? lastScan = null;
        DateTimeOffset? lastInstall = null;

        try
        {
            using var detectKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\Results\Detect");
            var scanRaw = detectKey?.GetValue("LastSuccessTime")?.ToString();
            if (!string.IsNullOrWhiteSpace(scanRaw) && DateTime.TryParse(scanRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtScan))
            {
                lastScan = new DateTimeOffset(dtScan, TimeSpan.Zero);
            }

            using var installKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\Results\Install");
            var installRaw = installKey?.GetValue("LastSuccessTime")?.ToString();
            if (!string.IsNullOrWhiteSpace(installRaw) && DateTime.TryParse(installRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtInstall))
            {
                lastInstall = new DateTimeOffset(dtInstall, TimeSpan.Zero);
            }
        }
        catch
        {
        }

        return (lastScan, lastInstall);
    }

    private List<string> ReadRecentUpdateErrors()
    {
        var errors = new List<string>();

        try
        {
            var query = "*[System[(Level=2 or Level=3) and TimeCreated[timediff(@SystemTime) <= 86400000]]]";
            var logQuery = new EventLogQuery("Microsoft-Windows-WindowsUpdateClient/Operational", PathType.LogName, query);
            using var reader = new EventLogReader(logQuery);

            for (var record = reader.ReadEvent(); record != null; record = reader.ReadEvent())
            {
                using (record)
                {
                    var msg = record.FormatDescription();
                    if (!string.IsNullOrWhiteSpace(msg) && !errors.Contains(msg))
                    {
                        errors.Add(msg);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Canal de log WindowsUpdateClient/Operational não disponível ou sem eventos recentes");
        }

        return errors;
    }
}

