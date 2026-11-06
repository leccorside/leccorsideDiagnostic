using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using LeccorWindowsHealthCenter.Application.Apps;
using LeccorWindowsHealthCenter.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LeccorWindowsHealthCenter.Infrastructure.Apps;

public sealed partial class InstalledAppsProvider : IAppsProvider
{
    private readonly ILogger<InstalledAppsProvider> _logger;

    public InstalledAppsProvider(ILogger<InstalledAppsProvider> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<InstalledAppItem>> GetInstalledAppsAsync(CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var apps = new List<InstalledAppItem>();

            // 1. HKCU Uninstall (Instalações por usuário)
            ReadRegistryUninstallKeys(RegistryHive.CurrentUser, RegistryView.Default, apps, is64Bit: Environment.Is64BitOperatingSystem);

            // 2. HKLM 64-bit Uninstall
            if (Environment.Is64BitOperatingSystem)
            {
                ReadRegistryUninstallKeys(RegistryHive.LocalMachine, RegistryView.Registry64, apps, is64Bit: true);
            }

            // 3. HKLM 32-bit (WOW6432Node) Uninstall
            ReadRegistryUninstallKeys(RegistryHive.LocalMachine, RegistryView.Registry32, apps, is64Bit: false);

            // 4. UWP / Windows Store Apps (Registro Local Settings)
            ReadUwpStoreApps(apps);

            // 5. Detectar duplicatas de versão (agrupadas por nome base normalizado)
            FlagDuplicateVersions(apps);

            _logger.LogDebug("Inventário de aplicativos concluído: {Count} programas detectados", apps.Count);

            return apps.OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }, cancellationToken);
    }

    private void ReadRegistryUninstallKeys(RegistryHive hive, RegistryView view, List<InstalledAppItem> list, bool is64Bit)
    {
        const string uninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstallKey = baseKey.OpenSubKey(uninstallKeyPath);
            if (uninstallKey == null)
            {
                return;
            }

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                try
                {
                    using var subKey = uninstallKey.OpenSubKey(subKeyName);
                    if (subKey == null)
                    {
                        continue;
                    }

                    var displayName = subKey.GetValue("DisplayName")?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        continue;
                    }

                    // Ignorar atualizações do Windows como KBxxxxx (são tratadas no Windows Update)
                    if (displayName.StartsWith("Security Update for", StringComparison.OrdinalIgnoreCase) ||
                        displayName.StartsWith("Update for", StringComparison.OrdinalIgnoreCase) ||
                        displayName.StartsWith("Hotfix", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var systemComponent = subKey.GetValue("SystemComponent");
                    var isSystem = systemComponent is int sc && sc == 1;

                    var displayVersion = subKey.GetValue("DisplayVersion")?.ToString()?.Trim();
                    var publisher = subKey.GetValue("Publisher")?.ToString()?.Trim();
                    var installLocation = subKey.GetValue("InstallLocation")?.ToString()?.Trim();
                    var uninstallString = subKey.GetValue("UninstallString")?.ToString()?.Trim();
                    var quietUninstallString = subKey.GetValue("QuietUninstallString")?.ToString()?.Trim();

                    // Tamanho estimado
                    long? estimatedSize = null;
                    var sizeVal = subKey.GetValue("EstimatedSize");
                    if (sizeVal is int sizeKb && sizeKb > 0)
                    {
                        estimatedSize = (long)sizeKb * 1024;
                    }
                    else if (sizeVal is long sizeKbLong && sizeKbLong > 0)
                    {
                        estimatedSize = sizeKbLong * 1024;
                    }

                    // Data de instalação
                    DateTimeOffset? installDate = ParseInstallDate(subKey.GetValue("InstallDate")?.ToString());

                    // Verificar se o desinstalador está quebrado / órfão
                    var isOrphaned = CheckIfOrphaned(uninstallString, quietUninstallString);

                    list.Add(new InstalledAppItem
                    {
                        DisplayName = displayName,
                        DisplayVersion = displayVersion,
                        Publisher = publisher,
                        InstallDate = installDate,
                        EstimatedSizeBytes = estimatedSize,
                        SourceType = isSystem ? AppSourceType.SystemComponent : AppSourceType.Win32,
                        InstallLocation = installLocation,
                        UninstallString = uninstallString,
                        QuietUninstallString = quietUninstallString,
                        RegistryKeyPath = $@"{hive}\{uninstallKeyPath}\{subKeyName}",
                        Is64Bit = is64Bit,
                        IsOrphaned = isOrphaned,
                        HasDuplicateVersion = false,
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Falha ao ler entrada de aplicativo no registro: {SubKey}", subKeyName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao acessar chave de desinstalação no hive {Hive}", hive);
        }
    }

    private void ReadUwpStoreApps(List<InstalledAppItem> list)
    {
        const string uwpPath = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(uwpPath);
            if (key == null)
            {
                return;
            }

            foreach (var pkgName in key.GetSubKeyNames())
            {
                try
                {
                    using var subKey = key.OpenSubKey(pkgName);
                    if (subKey == null)
                    {
                        continue;
                    }

                    var displayName = subKey.GetValue("DisplayName")?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(displayName) || displayName.StartsWith("@{", StringComparison.Ordinal))
                    {
                        // Em alguns pacotes UWP o DisplayName é um recurso de string não resolvido (ex: @{Microsoft.Paint...})
                        // Nesses casos extraímos o nome amigável do pacote
                        displayName = CleanPackageName(pkgName);
                    }

                    var publisher = subKey.GetValue("PublisherDisplayName")?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(publisher))
                    {
                        publisher = "Microsoft Corporation";
                    }

                    var packageId = subKey.GetValue("PackageID")?.ToString()?.Trim() ?? pkgName;
                    var version = ExtractPackageVersion(packageId);

                    list.Add(new InstalledAppItem
                    {
                        DisplayName = displayName,
                        DisplayVersion = version,
                        Publisher = publisher,
                        InstallDate = null,
                        EstimatedSizeBytes = null,
                        SourceType = AppSourceType.WindowsStore,
                        InstallLocation = null,
                        UninstallString = null,
                        QuietUninstallString = null,
                        RegistryKeyPath = $@"{RegistryHive.CurrentUser}\{uwpPath}\{pkgName}",
                        Is64Bit = Environment.Is64BitOperatingSystem,
                        IsOrphaned = false,
                        HasDuplicateVersion = false,
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Erro ao ler pacote UWP {Pkg}", pkgName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Não foi possível enumerar pacotes da Windows Store no registro");
        }
    }

    private static bool CheckIfOrphaned(string? uninstallString, string? quietUninstallString)
    {
        var raw = !string.IsNullOrWhiteSpace(quietUninstallString) ? quietUninstallString : uninstallString;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        // MSIExec é do Windows, não valida arquivo de instalador de terceiros
        if (raw.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var exePath = WindowsServicesProvider.ExtractExecutablePath(raw);
        if (string.IsNullOrWhiteSpace(exePath) || !Path.IsPathRooted(exePath))
        {
            return false;
        }

        try
        {
            return !File.Exists(exePath);
        }
        catch
        {
            return false;
        }
    }

    private static void FlagDuplicateVersions(List<InstalledAppItem> apps)
    {
        var groups = apps
            .Where(a => a.SourceType != AppSourceType.SystemComponent && !string.IsNullOrWhiteSpace(a.DisplayName))
            .GroupBy(a => NormalizeAppName(a.DisplayName), StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var distinctVersions = group.Select(a => a.DisplayVersion ?? "Unknown").Distinct().ToList();
            if (group.Count() > 1 && distinctVersions.Count > 1)
            {
                foreach (var item in group)
                {
                    // Altera a flag HasDuplicateVersion
                    var idx = apps.IndexOf(item);
                    if (idx >= 0)
                    {
                        apps[idx] = item with { HasDuplicateVersion = true };
                    }
                }
            }
        }
    }

    private static string NormalizeAppName(string name)
    {
        var clean = StripVersionAndArchRegex().Replace(name, "").Trim();
        return clean.Length > 0 ? clean : name;
    }

    private static string CleanPackageName(string packageName)
    {
        var parts = packageName.Split('_');
        return parts.Length > 0 ? parts[0] : packageName;
    }

    private static string? ExtractPackageVersion(string packageId)
    {
        var parts = packageId.Split('_');
        return parts.Length >= 2 ? parts[1] : null;
    }

    private static DateTimeOffset? ParseInstallDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (raw.Length == 8 && DateTime.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtExact))
        {
            return new DateTimeOffset(dtExact, TimeSpan.Zero);
        }

        if (DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.None, out var dtParsed))
        {
            return new DateTimeOffset(dtParsed, TimeSpan.Zero);
        }

        return null;
    }

    [GeneratedRegex(@"\s+(\(?(64-bit|32-bit|x64|x86|arm64)\)?|v?\d+(\.\d+)+.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StripVersionAndArchRegex();
}

