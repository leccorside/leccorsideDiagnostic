using System.Globalization;
using System.Management;
using System.Text.RegularExpressions;
using LeccorWindowsHealthCenter.Application.Services;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LeccorWindowsHealthCenter.Infrastructure.Services;

public sealed class WindowsServicesProvider : IServicesProvider
{
    private readonly IFileIdentityProvider _fileIdentity;
    private readonly ILogger<WindowsServicesProvider> _logger;

    public WindowsServicesProvider(IFileIdentityProvider fileIdentity, ILogger<WindowsServicesProvider> logger)
    {
        _fileIdentity = fileIdentity;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ServiceItem>> GetServicesAsync(CancellationToken cancellationToken)
    {
        return await Task.Run(async () =>
        {
            var runningStates = ReadServiceStatesViaWmi();
            var serviceList = new List<ServiceItem>();

            using var servicesKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (servicesKey is null)
            {
                return serviceList;
            }

            var subKeyNames = servicesKey.GetSubKeyNames();

            foreach (var name in subKeyNames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var subKey = servicesKey.OpenSubKey(name);
                    if (subKey is null)
                    {
                        continue;
                    }

                    // Ignora drivers de kernel (Type 1 e 2); serviços são Type 16 (0x10) ou 32 (0x20)
                    var typeVal = subKey.GetValue("Type") as int?;
                    if (typeVal.HasValue && (typeVal.Value & 0x30) == 0 && typeVal.Value <= 2)
                    {
                        continue;
                    }

                    var rawImagePath = subKey.GetValue("ImagePath")?.ToString();
                    if (string.IsNullOrWhiteSpace(rawImagePath))
                    {
                        continue;
                    }

                    var displayName = subKey.GetValue("DisplayName")?.ToString();
                    var description = subKey.GetValue("Description")?.ToString();
                    var objectName = subKey.GetValue("ObjectName")?.ToString();
                    var startVal = subKey.GetValue("Start") as int?;
                    var delayedAuto = subKey.GetValue("DelayedAutostart") as int? == 1;

                    var startupType = MapStartupType(startVal, delayedAuto);
                    var (state, pid) = runningStates.TryGetValue(name, out var runInfo)
                        ? runInfo
                        : (WindowsServiceState.Stopped, null);

                    var exePath = ExtractExecutablePath(rawImagePath);
                    var fileExists = !string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath);

                    var dependencies = (subKey.GetValue("DependOnService") as string[]) ?? [];

                    serviceList.Add(new ServiceItem
                    {
                        ServiceName = name,
                        DisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName : name,
                        Description = description,
                        State = state,
                        StartupType = startupType,
                        ServiceAccount = objectName,
                        RawImagePath = rawImagePath,
                        ExecutablePath = exePath,
                        FileExists = fileExists,
                        ProcessId = pid,
                        Dependencies = dependencies,
                        IsWindowsService = IsWindowsOwned(exePath),
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Falha ao ler serviço {Name}", name);
                }
            }

            // Enriquecer identidade de arquivos (fabricante e assinatura) para os executáveis
            var results = new List<ServiceItem>(serviceList.Count);
            foreach (var svc in serviceList)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (svc.FileExists && !string.IsNullOrWhiteSpace(svc.ExecutablePath))
                {
                    var id = await _fileIdentity.GetAsync(svc.ExecutablePath, cancellationToken).ConfigureAwait(false);
                    results.Add(svc with
                    {
                        Publisher = id.Publisher,
                        IsSigned = id.Signature is SignatureStatus.Valid or SignatureStatus.SignedByCatalog,
                    });
                }
                else
                {
                    results.Add(svc);
                }
            }

            return (IReadOnlyList<ServiceItem>)results;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, (WindowsServiceState State, int? Pid)> ReadServiceStatesViaWmi()
    {
        var dict = new Dictionary<string, (WindowsServiceState State, int? Pid)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\cimv2",
                "SELECT Name, State, ProcessId FROM Win32_Service");

            using var results = searcher.Get();
            foreach (var item in results.Cast<ManagementObject>())
            {
                var name = item["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var stateStr = item["State"]?.ToString();
                var state = stateStr switch
                {
                    "Running" => WindowsServiceState.Running,
                    "Stopped" => WindowsServiceState.Stopped,
                    "Paused" => WindowsServiceState.Paused,
                    "Start Pending" => WindowsServiceState.StartPending,
                    "Stop Pending" => WindowsServiceState.StopPending,
                    _ => WindowsServiceState.Unknown,
                };

                int? pid = null;
                if (item["ProcessId"] is { } pidObj && Convert.ToInt32(pidObj, CultureInfo.InvariantCulture) is var p and > 0)
                {
                    pid = p;
                }

                dict[name] = (state, pid);
            }
        }
        catch
        {
        }
        return dict;
    }

    private static WindowsServiceStartupType MapStartupType(int? startVal, bool delayed) => startVal switch
    {
        0 => WindowsServiceStartupType.Boot,
        1 => WindowsServiceStartupType.System,
        2 => delayed ? WindowsServiceStartupType.AutomaticDelayed : WindowsServiceStartupType.Automatic,
        3 => WindowsServiceStartupType.Manual,
        4 => WindowsServiceStartupType.Disabled,
        _ => WindowsServiceStartupType.Unknown,
    };

    public static string? ExtractExecutablePath(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var expanded = Environment.ExpandEnvironmentVariables(raw.Trim());

        if (expanded.StartsWith('\"'))
        {
            var closing = expanded.IndexOf('\"', 1);
            if (closing > 0)
            {
                return expanded[1..closing];
            }
        }

        var exeIndex = expanded.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex > 0)
        {
            return expanded[..(exeIndex + 4)].Trim();
        }

        var space = expanded.IndexOf(' ');
        return space > 0 ? expanded[..space] : expanded;
    }

    private static bool IsWindowsOwned(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return path.StartsWith(windir, StringComparison.OrdinalIgnoreCase);
    }
}

