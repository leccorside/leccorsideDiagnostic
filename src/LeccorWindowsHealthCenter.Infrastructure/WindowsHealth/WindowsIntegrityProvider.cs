using System.Globalization;
using System.Text;
using LeccorWindowsHealthCenter.Application.Commands;
using LeccorWindowsHealthCenter.Application.WindowsHealth;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.WindowsHealth;

/// <summary>
/// Executa e analisa as verificações não destrutivas de integridade do Windows (seção 22 do PROMPT):
/// DISM (/CheckHealth ou /ScanHealth), SFC (/VERIFYONLY) e CHKDSK (somente leitura).
/// </summary>
public sealed class WindowsIntegrityProvider : IWindowsIntegrityProvider
{
    private readonly ICommandExecutor _executor;
    private readonly ILogger<WindowsIntegrityProvider> _logger;

    public WindowsIntegrityProvider(ICommandExecutor executor, ILogger<WindowsIntegrityProvider> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    public async Task<ComponentStoreCheck> CheckComponentStoreAsync(bool deepScan, CancellationToken cancellationToken)
    {
        var dismPath = Path.Combine(Environment.SystemDirectory, "dism.exe");
        var checkArg = deepScan ? "/ScanHealth" : "/CheckHealth";

        var request = new CommandRequest
        {
            ExecutablePath = dismPath,
            Arguments = ["/Online", "/Cleanup-Image", checkArg],
            Timeout = deepScan ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(3),
            RequiresElevation = true,
            Purpose = deepScan ? "Varredura profunda do Component Store (DISM)" : "Verificação rápida do Component Store (DISM)",
            OutputEncoding = GetOemEncoding(),
        };

        try
        {
            var result = await _executor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            var (status, message) = ParseDismOutput(result);
            return new ComponentStoreCheck(status, message, result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao executar verificação DISM");
            return new ComponentStoreCheck(ComponentStoreStatus.CheckFailed, $"Não foi possível verificar o Component Store: {ex.Message}", null);
        }
    }

    public async Task<SystemFilesCheck> CheckSystemFilesAsync(CancellationToken cancellationToken)
    {
        var sfcPath = Path.Combine(Environment.SystemDirectory, "sfc.exe");

        var request = new CommandRequest
        {
            ExecutablePath = sfcPath,
            Arguments = ["/VERIFYONLY"],
            Timeout = TimeSpan.FromMinutes(12),
            RequiresElevation = true,
            Purpose = "Verificação de integridade dos arquivos protegidos do sistema (SFC /VERIFYONLY)",
            OutputEncoding = Encoding.Unicode, // SFC no Windows gera saída estritamente em UTF-16 LE
        };

        try
        {
            var result = await _executor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            var (status, message) = ParseSfcOutput(result);
            return new SystemFilesCheck(status, message, result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao executar verificação SFC");
            return new SystemFilesCheck(SystemFilesStatus.CheckFailed, $"Não foi possível verificar arquivos de sistema: {ex.Message}", null);
        }
    }

    public async Task<FileSystemCheck> CheckFileSystemAsync(string driveLetter, CancellationToken cancellationToken)
    {
        var drive = NormalizeDriveLetter(driveLetter);
        var chkdskPath = Path.Combine(Environment.SystemDirectory, "chkdsk.exe");

        var request = new CommandRequest
        {
            ExecutablePath = chkdskPath,
            Arguments = [drive],
            Timeout = TimeSpan.FromMinutes(5),
            RequiresElevation = true,
            Purpose = $"Verificação somente leitura do sistema de arquivos em {drive} (CHKDSK)",
            OutputEncoding = GetOemEncoding(),
        };

        try
        {
            var result = await _executor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            var (status, message) = ParseChkdskOutput(result, drive);
            return new FileSystemCheck(drive, status, message, result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao executar CHKDSK no volume {Drive}", drive);
            return new FileSystemCheck(drive, FileSystemStatus.CheckFailed, $"Não foi possível verificar o sistema de arquivos em {drive}: {ex.Message}", null);
        }
    }

    public async Task<WindowsIntegrityResult> CheckAllAsync(bool deepScan, CancellationToken cancellationToken)
    {
        var componentStore = await CheckComponentStoreAsync(deepScan, cancellationToken).ConfigureAwait(false);
        var systemFiles = await CheckSystemFilesAsync(cancellationToken).ConfigureAwait(false);

        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:";
        var fileSystem = await CheckFileSystemAsync(systemDrive, cancellationToken).ConfigureAwait(false);

        return new WindowsIntegrityResult
        {
            ComponentStore = componentStore,
            SystemFiles = systemFiles,
            FileSystem = fileSystem,
            VerifiedAt = DateTimeOffset.UtcNow,
        };
    }

    public static (ComponentStoreStatus Status, string Message) ParseDismOutput(CommandResult result)
    {
        var output = (result.StandardOutput + " " + result.StandardError).Trim();

        if (output.Contains("Nenhuma corrupção no repositório de componentes foi detectada", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("No component store corruption detected", StringComparison.OrdinalIgnoreCase))
        {
            return (ComponentStoreStatus.Healthy, "Repositório de componentes íntegro (nenhuma corrupção detectada).");
        }

        if (output.Contains("O repositório de componentes é reparável", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("The component store is repairable", StringComparison.OrdinalIgnoreCase))
        {
            return (ComponentStoreStatus.Repairable, "O repositório de componentes contém arquivos danificados, mas é reparável.");
        }

        if (output.Contains("O repositório de componentes está corrompido", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("The component store is corrupted", StringComparison.OrdinalIgnoreCase) ||
            result.ExitCode == 14098)
        {
            return (ComponentStoreStatus.Corrupted, "O repositório de componentes está corrompido.");
        }

        if (result.ExitCode == 0 && output.Length > 0)
        {
            return (ComponentStoreStatus.Healthy, "Operação do DISM concluída sem erros reportados.");
        }

        return (ComponentStoreStatus.CheckFailed, $"Verificação do DISM reportou código de saída {result.ExitCode ?? -1}: {(output.Length > 200 ? output[..200] + "..." : output)}");
    }

    public static (SystemFilesStatus Status, string Message) ParseSfcOutput(CommandResult result)
    {
        var output = (result.StandardOutput + " " + result.StandardError).Trim();

        if (output.Contains("não encontrou nenhuma violação de integridade", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("did not find any integrity violations", StringComparison.OrdinalIgnoreCase))
        {
            return (SystemFilesStatus.IntegrityOk, "Arquivos protegidos do Windows estão íntegros (nenhuma violação encontrada).");
        }

        if (output.Contains("encontrou arquivos corrompidos e os reparou", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("found corrupt files and successfully repaired them", StringComparison.OrdinalIgnoreCase))
        {
            return (SystemFilesStatus.CorruptedRepaired, "Arquivos corrompidos foram encontrados e reparados.");
        }

        if (output.Contains("encontrou arquivos corrompidos, mas não pôde", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("encontrou arquivos corrompidos", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("found corrupt files but was unable", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("found corrupt files", StringComparison.OrdinalIgnoreCase))
        {
            return (SystemFilesStatus.CorruptedCannotRepair, "Arquivos de sistema corrompidos foram detectados no Windows.");
        }

        if (result.ExitCode == 0 && output.Length > 0)
        {
            return (SystemFilesStatus.IntegrityOk, "Verificação do SFC concluída com sucesso.");
        }

        return (SystemFilesStatus.CheckFailed, $"Verificação SFC não pôde ser concluída (código {result.ExitCode ?? -1}).");
    }

    public static (FileSystemStatus Status, string Message) ParseChkdskOutput(CommandResult result, string drive)
    {
        var output = (result.StandardOutput + " " + result.StandardError).Trim();

        if (output.Contains("não encontrou problemas", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("found no problems", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("nenhuma ação adicional é necessária", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("No further action is required", StringComparison.OrdinalIgnoreCase))
        {
            return (FileSystemStatus.VolumeOk, $"Sistema de arquivos em {drive} íntegro (nenhum erro encontrado).");
        }

        if (output.Contains("encontrou erros", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("has found errors", StringComparison.OrdinalIgnoreCase) ||
            result.ExitCode is 1 or 2)
        {
            return (FileSystemStatus.ErrorsFound, $"Foram encontrados erros no sistema de arquivos do volume {drive}.");
        }

        if (result.ExitCode == 0 && output.Length > 0)
        {
            return (FileSystemStatus.VolumeOk, $"CHKDSK em {drive} concluído sem erros.");
        }

        return (FileSystemStatus.CheckFailed, $"Não foi possível analisar o sistema de arquivos em {drive} (código {result.ExitCode ?? -1}).");
    }

    private static string NormalizeDriveLetter(string drive)
    {
        if (string.IsNullOrWhiteSpace(drive))
        {
            return "C:";
        }

        var letter = char.ToUpperInvariant(drive[0]);
        return $"{letter}:";
    }

    private static Encoding GetOemEncoding()
    {
        try
        {
            return Encoding.GetEncoding(CultureInfo.InstalledUICulture.TextInfo.OEMCodePage);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }
}
