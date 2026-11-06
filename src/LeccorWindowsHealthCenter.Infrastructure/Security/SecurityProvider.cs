using System.Globalization;
using System.Management;
using System.Runtime.Versioning;
using LeccorWindowsHealthCenter.Application.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LeccorWindowsHealthCenter.Infrastructure.Security;

[SupportedOSPlatform("windows")]
public sealed class SecurityProvider : ISecurityProvider
{
    private const string SecurityCenterScope = @"\\.\root\SecurityCenter2";
    private readonly ILogger<SecurityProvider> _logger;

    public SecurityProvider(ILogger<SecurityProvider> logger)
    {
        _logger = logger;
    }

    public Task<SecurityStatusSummary> GetSecurityStatusAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var avList = CollectAntivirusProducts();
            var fwList = CollectFirewallProducts();
            var uacEnabled = CheckUacStatus();
            var (rtpActive, tamperActive) = CheckWindowsDefenderFlags();
            var persistences = ScanSuspiciousPersistences(cancellationToken);

            return new SecurityStatusSummary
            {
                AntivirusProducts = avList,
                FirewallProducts = fwList,
                IsUacEnabled = uacEnabled,
                IsRealTimeProtectionActive = rtpActive,
                IsTamperProtectionActive = tamperActive,
                SuspiciousPersistences = persistences,
                CapturedAt = DateTimeOffset.UtcNow
            };
        }, cancellationToken);
    }

    private List<AntivirusProductInfo> CollectAntivirusProducts()
    {
        var list = new List<AntivirusProductInfo>();
        try
        {
            var scope = new ManagementScope(SecurityCenterScope);
            scope.Connect();
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM AntiVirusProduct"));
            using var results = searcher.Get();

            foreach (var obj in results.Cast<ManagementObject>())
            {
                var name = obj["displayName"] as string ?? "Antivírus Desconhecido";
                var guid = obj["instanceGuid"] as string ?? Guid.NewGuid().ToString();
                var exePath = obj["pathToSignedProductExe"] as string ?? string.Empty;
                var state = Convert.ToUInt32(obj["productState"] ?? 0, CultureInfo.InvariantCulture);

                // No SecurityCenter2 o productState é decodificado em hex:
                // byte central (bits 12..15): 0x10 = Ativo, 0x00/0x01 = Desativado
                // byte inferior: 0x00 = Atualizado, 0x10 = Desatualizado
                var isEnabled = (state & 0x00001000) != 0 || (state & 0x00010000) != 0;
                var isUpToDate = (state & 0x00000010) == 0;
                var isDefender = name.Contains("Windows Defender", StringComparison.OrdinalIgnoreCase) ||
                                 name.Contains("Microsoft Defender", StringComparison.OrdinalIgnoreCase);

                list.Add(new AntivirusProductInfo
                {
                    Name = name,
                    InstanceGuid = guid,
                    PathToSignedProductExe = exePath,
                    IsEnabled = isEnabled,
                    IsUpToDate = isUpToDate,
                    IsWindowsDefender = isDefender
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível consultar WMI AntiVirusProduct");
        }

        if (list.Count == 0)
        {
            // Fallback caso SecurityCenter2 WMI não responda (ex: Windows Server)
            list.Add(new AntivirusProductInfo
            {
                Name = "Segurança do Windows",
                InstanceGuid = "{DEFENDER-FALLBACK}",
                PathToSignedProductExe = string.Empty,
                IsEnabled = true,
                IsUpToDate = true,
                IsWindowsDefender = true
            });
        }

        return list;
    }

    private List<FirewallProductInfo> CollectFirewallProducts()
    {
        var list = new List<FirewallProductInfo>();
        try
        {
            var scope = new ManagementScope(SecurityCenterScope);
            scope.Connect();
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM FirewallProduct"));
            using var results = searcher.Get();

            foreach (var obj in results.Cast<ManagementObject>())
            {
                var name = obj["displayName"] as string ?? "Firewall";
                var guid = obj["instanceGuid"] as string ?? Guid.NewGuid().ToString();
                var state = Convert.ToUInt32(obj["productState"] ?? 0, CultureInfo.InvariantCulture);
                var isEnabled = (state & 0x00001000) != 0 || (state & 0x00010000) != 0;

                list.Add(new FirewallProductInfo
                {
                    Name = name,
                    InstanceGuid = guid,
                    IsEnabled = isEnabled
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível consultar WMI FirewallProduct");
        }

        if (list.Count == 0)
        {
            list.Add(new FirewallProductInfo
            {
                Name = "Windows Defender Firewall",
                InstanceGuid = "{FIREWALL-FALLBACK}",
                IsEnabled = true
            });
        }

        return list;
    }

    private static bool CheckUacStatus()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            var val = key?.GetValue("EnableLUA");
            return val is int intVal && intVal == 1;
        }
        catch
        {
            return true;
        }
    }

    private static (bool RealTimeActive, bool TamperActive) CheckWindowsDefenderFlags()
    {
        var rtp = true;
        var tamper = true;

        try
        {
            using var rtpKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection");
            if (rtpKey != null)
            {
                var disableRtp = rtpKey.GetValue("DisableRealtimeMonitoring");
                if (disableRtp is int intRtp && intRtp == 1)
                {
                    rtp = false;
                }
            }

            using var featKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Features");
            if (featKey != null)
            {
                var tamperVal = featKey.GetValue("TamperProtection");
                if (tamperVal is int intTamper && intTamper == 0)
                {
                    tamper = false;
                }
            }
        }
        catch
        {
            // Chaves protegidas por token de integridade alta
        }

        return (rtp, tamper);
    }

    private static List<SuspiciousPersistenceItem> ScanSuspiciousPersistences(CancellationToken cancellationToken)
    {
        var items = new List<SuspiciousPersistenceItem>();

        // 1. IFEO (Image File Execution Options) Debugger Hijack
        string[] criticalSystemBinaries = ["sethc.exe", "utilman.exe", "osk.exe", "magnify.exe", "taskmgr.exe", "cmd.exe"];
        try
        {
            using var ifeoKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options");
            if (ifeoKey != null)
            {
                foreach (var binary in criticalSystemBinaries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var sub = ifeoKey.OpenSubKey(binary);
                    var debugger = sub?.GetValue("Debugger") as string;
                    if (!string.IsNullOrWhiteSpace(debugger))
                    {
                        items.Add(new SuspiciousPersistenceItem
                        {
                            Target = $@"IFEO\{binary}",
                            Type = "IFEO Debugger Hijack",
                            Description = $"O executável do sistema '{binary}' está configurado para executar '{debugger}' através da chave de Debugger do IFEO.",
                            Details = $"Debugger: {debugger}",
                            IsSuspicious = true
                        });
                    }
                }
            }
        }
        catch
        {
        }

        // 2. AppInit_DLLs
        try
        {
            using var winKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows");
            if (winKey != null)
            {
                var appInit = winKey.GetValue("AppInit_DLLs") as string;
                var loadAppInit = winKey.GetValue("LoadAppInit_DLLs") as int? ?? 0;
                if (!string.IsNullOrWhiteSpace(appInit) && loadAppInit == 1)
                {
                    items.Add(new SuspiciousPersistenceItem
                    {
                        Target = "AppInit_DLLs",
                        Type = "AppInit DLL Injection",
                        Description = "Bibliotecas AppInit_DLLs ativas sendo injetadas em todos os processos carregados pelo subsistema do Windows.",
                        Details = $"DLLs: {appInit}",
                        IsSuspicious = true
                    });
                }
            }
        }
        catch
        {
        }

        return items;
    }
}
