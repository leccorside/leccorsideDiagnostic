using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Domain.Repairs;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.Backup;

/// <summary>
/// Backup Engine (seção 36): um arquivo JSON por backup em %ProgramData%\LeccorWindowsHealthCenter\Backups.
/// O diretório recebe ACL restrita (SYSTEM, Administradores e o usuário atual) — seção 63.
/// </summary>
public sealed class FileBackupStore : IBackupStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _directory;
    private readonly ILogger<FileBackupStore> _logger;
    private int _secured;

    public FileBackupStore(string directory, ILogger<FileBackupStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _logger = logger;
    }

    public async Task SaveAsync(BackupRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        EnsureDirectory();

        var path = PathFor(record.Id);
        var temp = path + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, record, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        // Escrita atômica: nunca deixar um backup pela metade.
        File.Move(temp, path, overwrite: true);
        _logger.LogInformation("Backup {BackupId} salvo para a ação {ActionId}", record.Id, record.ActionId);
    }

    public async Task<BackupRecord?> GetAsync(Guid backupId, CancellationToken cancellationToken)
    {
        var path = PathFor(backupId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<BackupRecord>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private string PathFor(Guid id) => Path.Combine(_directory, $"{id:N}.json");

    private void EnsureDirectory()
    {
        var info = Directory.CreateDirectory(_directory);
        if (Interlocked.Exchange(ref _secured, 1) == 1)
        {
            return;
        }

        try
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            foreach (var sid in new[]
                     {
                         new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                         new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                         WindowsIdentity.GetCurrent().User!,
                     })
            {
                security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            }

            info.SetAccessControl(security);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or IOException or PrivilegeNotHeldException)
        {
            _logger.LogWarning(ex, "Não foi possível restringir a ACL do diretório de backups {Directory}", _directory);
        }
    }
}
