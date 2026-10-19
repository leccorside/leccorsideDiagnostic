using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.SystemInfo;

/// <summary>
/// "Mostrar no Explorer" pela API do shell (SHOpenFolderAndSelectItems): abre a pasta-mãe com o item selecionado.
/// Não usa ShellExecute nem inicia processo — um caminho para .exe é só selecionado, nunca executado — por isso não
/// passa pelo <c>CommandExecutor</c>. Chamar na thread da interface (STA).
/// </summary>
public sealed class ShellLauncher : IShellLauncher
{
    private readonly ILogger<ShellLauncher> _logger;

    public ShellLauncher(ILogger<ShellLauncher> logger)
    {
        _logger = logger;
    }

    public bool Reveal(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            return false;
        }

        var parsed = NativeMethods.SHParseDisplayName(Path.GetFullPath(path), IntPtr.Zero, out var pidl, 0, out _);
        if (parsed != 0 || pidl == IntPtr.Zero)
        {
            _logger.LogWarning("O shell não reconheceu o caminho {Path} (HRESULT 0x{Result:X8})", path, parsed);
            return false;
        }

        try
        {
            var opened = NativeMethods.SHOpenFolderAndSelectItems(pidl, 0, IntPtr.Zero, 0);
            if (opened != 0)
            {
                _logger.LogWarning("O shell não abriu {Path} (HRESULT 0x{Result:X8})", path, opened);
            }

            return opened == 0;
        }
        finally
        {
            NativeMethods.CoTaskMemFree(pidl);
        }
    }
}
