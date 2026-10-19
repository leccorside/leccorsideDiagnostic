using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.SystemInfo;

/// <summary>
/// Fabricante (recurso de versão) e assinatura Authenticode (WinVerifyTrust) de um executável. Sem consulta de
/// revogação na rede (o app é 100% local). Arquivos do Windows costumam ser assinados por catálogo, não no próprio
/// arquivo: sem a busca no catálogo, notepad.exe apareceria como "sem assinatura".
/// </summary>
public sealed class FileIdentityProvider : IFileIdentityProvider, IDisposable
{
    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private readonly ILogger<FileIdentityProvider> _logger;
    private readonly ConcurrentDictionary<(string Path, DateTime Modified), FileIdentity> _cache = new();
    private readonly SemaphoreSlim _gate = new(2, 2);

    public FileIdentityProvider(ILogger<FileIdentityProvider> logger)
    {
        _logger = logger;
    }

    public void Dispose() => _gate.Dispose();

    public async Task<FileIdentity> GetAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            return new FileIdentity { Path = path, Exists = false };
        }

        var key = (path.ToUpperInvariant(), File.GetLastWriteTimeUtc(path));
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var identity = await Task.Run(() => Read(path), cancellationToken).ConfigureAwait(false);
            _cache[key] = identity;
            return identity;
        }
        finally
        {
            _gate.Release();
        }
    }

    private FileIdentity Read(string path)
    {
        string? publisher = null, description = null, version = null;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            publisher = Clean(info.CompanyName);
            description = Clean(info.FileDescription);
            version = Clean(info.FileVersion);
        }
        catch (FileNotFoundException)
        {
        }

        var status = VerifyEmbedded(path);
        if (status == SignatureStatus.NoSignature && IsInCatalog(path))
        {
            status = SignatureStatus.SignedByCatalog;
        }

        return new FileIdentity
        {
            Path = path,
            Exists = true,
            Publisher = publisher,
            Description = description,
            Version = version,
            Signature = status,
            Signer = status is SignatureStatus.Valid or SignatureStatus.Invalid ? SignerName(path) : null,
        };
    }

    private SignatureStatus VerifyEmbedded(string path)
    {
        var fileInfo = new WinTrustFileInfo { Size = Marshal.SizeOf<WinTrustFileInfo>(), FilePath = path };
        var filePointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        Marshal.StructureToPtr(fileInfo, filePointer, false);
        var data = new WinTrustData
        {
            Size = Marshal.SizeOf<WinTrustData>(),
            UiChoice = 2,               // WTD_UI_NONE
            RevocationChecks = 0,       // WTD_REVOKE_NONE: sem ir à rede
            UnionChoice = 1,            // WTD_CHOICE_FILE
            FileInfo = filePointer,
            StateAction = 1,            // WTD_STATEACTION_VERIFY
            ProvFlags = 0x00001000 | 0x00000010, // WTD_CACHE_ONLY_URL_RETRIEVAL | WTD_REVOCATION_CHECK_NONE
        };

        try
        {
            var action = GenericVerifyV2;
            var result = (uint)WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            data.StateAction = 2; // WTD_STATEACTION_CLOSE
            _ = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            return result switch
            {
                0 => SignatureStatus.Valid,
                0x800B0100 or 0x800B0003 or 0x800B0001 => SignatureStatus.NoSignature, // sem assinatura / formato sem suporte
                _ => SignatureStatus.Invalid,
            };
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or SEHException)
        {
            _logger.LogWarning(ex, "WinVerifyTrust indisponível para {Path}", path);
            return SignatureStatus.Unknown;
        }
        finally
        {
            Marshal.DestroyStructure<WinTrustFileInfo>(filePointer);
            Marshal.FreeHGlobal(filePointer);
        }
    }

    /// <summary>Procura o hash do arquivo nos catálogos do Windows (CryptCATAdmin, SHA-256).</summary>
    private static bool IsInCatalog(string path)
    {
        if (!CryptCATAdminAcquireContext2(out var admin, IntPtr.Zero, "SHA256", IntPtr.Zero, 0))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var size = 0;
            CryptCATAdminCalcHashFromFileHandle2(admin, stream.SafeFileHandle.DangerousGetHandle(), ref size, null, 0);
            if (size <= 0)
            {
                return false;
            }

            var hash = new byte[size];
            if (!CryptCATAdminCalcHashFromFileHandle2(admin, stream.SafeFileHandle.DangerousGetHandle(), ref size, hash, 0))
            {
                return false;
            }

            var previous = IntPtr.Zero;
            var catalog = CryptCATAdminEnumCatalogFromHash(admin, hash, size, 0, ref previous);
            if (catalog == IntPtr.Zero)
            {
                return false;
            }

            CryptCATAdminReleaseCatalogContext(admin, catalog, 0);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            CryptCATAdminReleaseContext(admin, 0);
        }
    }

    private static string? SignerName(string path)
    {
        try
        {
#pragma warning disable SYSLIB0057 // A assinatura Authenticode só é lida por este método; X509CertificateLoader não lê PE.
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            return Clean(certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false));
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or IOException)
        {
            return null;
        }
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public int Size;
        public string FilePath;
        public IntPtr File;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public int Size;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProvFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref WinTrustData data);

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminAcquireContext2(out IntPtr admin, IntPtr subsystem, string hashAlgorithm, IntPtr strongHashPolicy, uint flags);

    [DllImport("wintrust.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminCalcHashFromFileHandle2(IntPtr admin, IntPtr file, ref int hashSize, byte[]? hash, uint flags);

    [DllImport("wintrust.dll", SetLastError = true)]
    private static extern IntPtr CryptCATAdminEnumCatalogFromHash(IntPtr admin, byte[] hash, int hashSize, uint flags, ref IntPtr previousCatalog);

    [DllImport("wintrust.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminReleaseCatalogContext(IntPtr admin, IntPtr catalog, uint flags);

    [DllImport("wintrust.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminReleaseContext(IntPtr admin, uint flags);
}
