namespace LeccorWindowsHealthCenter.Application.WindowsHealth;

/// <summary>
/// Executa verificações não destrutivas de integridade do Windows (seção 22 do PROMPT):
/// DISM (/CheckHealth ou /ScanHealth), SFC (/VERIFYONLY) e CHKDSK (somente leitura).
/// </summary>
public interface IWindowsIntegrityProvider
{
    Task<ComponentStoreCheck> CheckComponentStoreAsync(bool deepScan, CancellationToken cancellationToken);

    Task<SystemFilesCheck> CheckSystemFilesAsync(CancellationToken cancellationToken);

    Task<FileSystemCheck> CheckFileSystemAsync(string driveLetter, CancellationToken cancellationToken);

    Task<WindowsIntegrityResult> CheckAllAsync(bool deepScan, CancellationToken cancellationToken);
}

