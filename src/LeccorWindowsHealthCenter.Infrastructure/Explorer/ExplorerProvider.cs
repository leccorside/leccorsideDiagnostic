using System.Text.RegularExpressions;
using LeccorWindowsHealthCenter.Application.Explorer;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LeccorWindowsHealthCenter.Infrastructure.Explorer;

public sealed partial class ExplorerProvider : IExplorerProvider
{
    private readonly IFileIdentityProvider _fileIdentity;
    private readonly ILogger<ExplorerProvider> _logger;

    private static readonly (string KeyPath, ShellExtensionType Type)[] ShellExtensionKeys =
    [
        (@"*\shellex\ContextMenuHandlers", ShellExtensionType.ContextMenu),
        (@"Directory\shellex\ContextMenuHandlers", ShellExtensionType.ContextMenu),
        (@"Directory\Background\shellex\ContextMenuHandlers", ShellExtensionType.ContextMenu),
        (@"Drive\shellex\ContextMenuHandlers", ShellExtensionType.ContextMenu),
        (@"Folder\shellex\ContextMenuHandlers", ShellExtensionType.ContextMenu),
        (@"AllFilesystemObjects\shellex\ContextMenuHandlers", ShellExtensionType.ContextMenu),
        (@"*\shellex\PropertySheetHandlers", ShellExtensionType.PropertySheet),
        (@"Directory\shellex\PropertySheetHandlers", ShellExtensionType.PropertySheet),
        (@"Software\Microsoft\Windows\CurrentVersion\Explorer\ShellIconOverlayIdentifiers", ShellExtensionType.IconOverlay),
    ];

    private static readonly Regex GuidRegex = new(@"\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}", RegexOptions.Compiled);

    public ExplorerProvider(IFileIdentityProvider fileIdentity, ILogger<ExplorerProvider> logger)
    {
        _fileIdentity = fileIdentity;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ShellExtensionItem>> GetShellExtensionsAsync(CancellationToken cancellationToken)
    {
        var blockedClsids = ReadBlockedClsids();
        var items = new Dictionary<string, ShellExtensionItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var (keyPath, type) in ShellExtensionKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadExtensionsFromKey(keyPath, type, blockedClsids, items);
        }

        var results = new List<ShellExtensionItem>(items.Count);
        foreach (var item in items.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.FileExists && !string.IsNullOrWhiteSpace(item.DllPath))
            {
                var id = await _fileIdentity.GetAsync(item.DllPath, cancellationToken).ConfigureAwait(false);
                results.Add(item with
                {
                    Publisher = id.Publisher,
                    IsSigned = id.Signature is SignatureStatus.Valid or SignatureStatus.SignedByCatalog,
                    IsRevoked = id.Signature == SignatureStatus.Revoked,
                });
            }
            else
            {
                results.Add(item);
            }
        }

        return results;
    }

    public Task<ExplorerCacheStatus> GetCacheStatusAsync(CancellationToken cancellationToken)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var explorerCacheDir = Path.Combine(localAppData, "Microsoft", "Windows", "Explorer");

        long iconBytes = 0;
        long thumbBytes = 0;
        var count = 0;
        var inaccessible = new List<string>();

        if (Directory.Exists(explorerCacheDir))
        {
            try
            {
                var files = Directory.GetFiles(explorerCacheDir, "*cache_*.db", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var fi = new FileInfo(file);
                        var len = fi.Length;
                        count++;

                        if (fi.Name.StartsWith("iconcache", StringComparison.OrdinalIgnoreCase))
                        {
                            iconBytes += len;
                        }
                        else if (fi.Name.StartsWith("thumbcache", StringComparison.OrdinalIgnoreCase))
                        {
                            thumbBytes += len;
                        }
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                    {
                        inaccessible.Add(file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao enumerar arquivos de cache do Explorer em {Dir}", explorerCacheDir);
            }
        }

        return Task.FromResult(new ExplorerCacheStatus
        {
            IconCacheSizeBytes = iconBytes,
            ThumbnailCacheSizeBytes = thumbBytes,
            CacheFilesCount = count,
            InaccessibleFiles = inaccessible,
        });
    }

    private static HashSet<string> ReadBlockedClsids()
    {
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked");
            if (key is not null)
            {
                foreach (var name in key.GetValueNames())
                {
                    var match = GuidRegex.Match(name);
                    if (match.Success)
                    {
                        blocked.Add(match.Value);
                    }
                }
            }
        }
        catch
        {
            // Acesso protegido ou chave ausente
        }
        return blocked;
    }

    private void ReadExtensionsFromKey(
        string keyPath,
        ShellExtensionType type,
        HashSet<string> blockedClsids,
        Dictionary<string, ShellExtensionItem> results)
    {
        try
        {
            // Tenta abrir no HKCR (fusão de HKLM e HKCU)
            using var baseKey = Registry.ClassesRoot.OpenSubKey(keyPath);
            if (baseKey is null)
            {
                return;
            }

            foreach (var subName in baseKey.GetSubKeyNames())
            {
                try
                {
                    using var subKey = baseKey.OpenSubKey(subName);
                    if (subKey is null)
                    {
                        continue;
                    }

                    var clsid = ResolveClsid(subName, subKey);
                    if (string.IsNullOrWhiteSpace(clsid))
                    {
                        continue;
                    }

                    var uniqueKey = $"{clsid}_{type}";
                    if (results.ContainsKey(uniqueKey))
                    {
                        continue;
                    }

                    var (dllPath, friendlyName, threadingModel) = ResolveClsidDetails(clsid);
                    var exists = !string.IsNullOrWhiteSpace(dllPath) && File.Exists(dllPath);
                    var isBlocked = blockedClsids.Contains(clsid);
                    var isWindows = IsWindowsFile(dllPath);

                    results[uniqueKey] = new ShellExtensionItem
                    {
                        Clsid = clsid,
                        Name = !string.IsNullOrWhiteSpace(friendlyName) ? friendlyName : subName,
                        Type = type,
                        DllPath = dllPath ?? string.Empty,
                        FileExists = exists,
                        IsBlocked = isBlocked,
                        IsWindowsOwned = isWindows,
                        RegistryKey = $@"{keyPath}\{subName}",
                        ThreadingModel = threadingModel,
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Falha ao ler subchave de shell extension {Key}\\{Sub}", keyPath, subName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao abrir chave {Key}", keyPath);
        }
    }

    private static string? ResolveClsid(string keyName, RegistryKey key)
    {
        var match = GuidRegex.Match(keyName);
        if (match.Success)
        {
            return match.Value;
        }

        var val = key.GetValue(null)?.ToString();
        if (!string.IsNullOrWhiteSpace(val))
        {
            match = GuidRegex.Match(val);
            if (match.Success)
            {
                return match.Value;
            }
        }

        return null;
    }

    private static (string? DllPath, string? FriendlyName, string? ThreadingModel) ResolveClsidDetails(string clsid)
    {
        try
        {
            using var clsidKey = Registry.ClassesRoot.OpenSubKey($@"CLSID\{clsid}");
            if (clsidKey is null)
            {
                return (null, null, null);
            }

            var friendlyName = clsidKey.GetValue(null)?.ToString();

            using var inproc = clsidKey.OpenSubKey("InprocServer32");
            if (inproc is null)
            {
                return (null, friendlyName, null);
            }

            var rawPath = inproc.GetValue(null)?.ToString();
            var threadingModel = inproc.GetValue("ThreadingModel")?.ToString();

            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return (null, friendlyName, threadingModel);
            }

            var cleanPath = Environment.ExpandEnvironmentVariables(rawPath.Trim('\"', ' '));
            return (cleanPath, friendlyName, threadingModel);
        }
        catch
        {
            return (null, null, null);
        }
    }

    private static bool IsWindowsFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return path.StartsWith(windir, StringComparison.OrdinalIgnoreCase);
    }
}
