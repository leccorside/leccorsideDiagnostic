using LeccorWindowsHealthCenter.Application.Commands;

namespace LeccorWindowsHealthCenter.Infrastructure.Commands;

public sealed class CommandExecutionOptions
{
    public const string SectionName = "CommandExecution";

    /// <summary>Executáveis fora do diretório do Windows explicitamente permitidos (caminho completo).</summary>
    public IList<string> AdditionalTrustedExecutables { get; } = [];
}

/// <summary>
/// Valida executável e argumentos antes de qualquer execução (seções 23 e 63):
/// caminho absoluto e normalizado, arquivo existente, em diretório confiável, nunca um
/// interpretador de shell, e argumentos sem caracteres de controle.
/// </summary>
public sealed class ExecutablePolicy
{
    /// <summary>
    /// Interpretadores bloqueados: eles reinterpretam argumentos, o que reabriria a porta para
    /// command injection mesmo usando ArgumentList. Ferramentas são chamadas diretamente (dism.exe, sfc.exe...).
    /// </summary>
    private static readonly HashSet<string> BlockedInterpreters = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd.exe", "powershell.exe", "powershell_ise.exe", "pwsh.exe", "wscript.exe", "cscript.exe", "mshta.exe", "rundll32.exe", "bash.exe",
    };

    private readonly string[] _trustedDirectories;
    private readonly HashSet<string> _trustedExecutables;

    public ExecutablePolicy(IEnumerable<string>? additionalTrustedExecutables = null, IEnumerable<string>? trustedDirectories = null)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        _trustedDirectories = (trustedDirectories ??
        [
            Path.Combine(windows, "System32"),
            Path.Combine(windows, "SysWOW64"),
            Path.Combine(windows, "Sysnative"),
            windows,
        ]).Select(NormalizeDirectory).ToArray();

        _trustedExecutables = new HashSet<string>(
            (additionalTrustedExecutables ?? []).Select(Path.GetFullPath),
            StringComparer.OrdinalIgnoreCase);
    }

    public void Validate(CommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var path = request.ExecutablePath;

        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new CommandRejectedException($"O executável deve ser informado por caminho absoluto: '{path}'.");
        }

        if (path.Contains("..", StringComparison.Ordinal) || !string.Equals(Path.GetFullPath(path), path, StringComparison.OrdinalIgnoreCase))
        {
            throw new CommandRejectedException($"Caminho de executável não normalizado: '{path}'.");
        }

        if (!string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new CommandRejectedException($"Somente arquivos .exe podem ser executados: '{path}'.");
        }

        if (BlockedInterpreters.Contains(Path.GetFileName(path)))
        {
            throw new CommandRejectedException($"Interpretadores de shell não são permitidos: '{Path.GetFileName(path)}'.");
        }

        var directory = NormalizeDirectory(Path.GetDirectoryName(path)!);
        var trusted = _trustedExecutables.Contains(path) ||
                      _trustedDirectories.Any(d => string.Equals(d, directory, StringComparison.OrdinalIgnoreCase));
        if (!trusted)
        {
            throw new CommandRejectedException($"Executável fora dos diretórios confiáveis: '{path}'.");
        }

        if (!File.Exists(path))
        {
            throw new CommandRejectedException($"Executável não encontrado: '{path}'.");
        }

        foreach (var argument in request.Arguments)
        {
            if (argument is null || argument.Any(c => c is '\0' or '\r' or '\n'))
            {
                throw new CommandRejectedException("Argumento contém caracteres de controle não permitidos.");
            }
        }

        if (request.Timeout <= TimeSpan.Zero)
        {
            throw new CommandRejectedException("O timeout do comando precisa ser positivo.");
        }
    }

    private static string NormalizeDirectory(string directory) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
}
