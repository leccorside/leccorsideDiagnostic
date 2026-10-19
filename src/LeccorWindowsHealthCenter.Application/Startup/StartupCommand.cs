namespace LeccorWindowsHealthCenter.Application.Startup;

/// <summary>
/// Interpreta linhas de comando de inicialização como o Windows as grava — com aspas, sem aspas e com espaços no
/// caminho ("C:\Program Files\OpenVPN Connect\OpenVPNConnect.exe --opened-at-login"), com variáveis de ambiente, só
/// o nome do executável ("ctfmon.exe") ou via rundll32 (o que precisa existir é a DLL).
/// </summary>
public static class StartupCommand
{
    private static readonly string[] Executables = [".exe", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msc", ".lnk", ".dll", ".scr"];

    /// <summary>
    /// Caminho do arquivo que precisa existir para a entrada funcionar, ou null quando não dá para interpretar.
    /// <paramref name="expand"/> e <paramref name="systemDirectory"/> existem para os testes; o padrão usa o ambiente real.
    /// </summary>
    public static string? ExecutablePath(string? command, Func<string, string>? expand = null, string? systemDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        expand ??= Environment.ExpandEnvironmentVariables;
        systemDirectory ??= Environment.SystemDirectory;
        var text = expand(command.Trim());

        string path;
        string rest;
        if (text.StartsWith('"'))
        {
            var close = text.IndexOf('"', 1);
            if (close < 0)
            {
                return null;
            }

            path = text[1..close];
            rest = text[(close + 1)..];
        }
        else
        {
            // Sem aspas: vai até a primeira extensão executável (o caminho pode ter espaços), senão até o primeiro espaço.
            var end = Executables
                .Select(ext => (ext, index: text.IndexOf(ext, StringComparison.OrdinalIgnoreCase)))
                .Where(x => x.index > 0 && (x.index + x.ext.Length == text.Length || text[x.index + x.ext.Length] is ' ' or ','))
                .Select(x => x.index + x.ext.Length)
                .DefaultIfEmpty(text.IndexOf(' ') is var space and > 0 ? space : text.Length)
                .Min();
            path = text[..end];
            rest = text[end..];
        }

        path = path.Trim();
        if (path.Length == 0)
        {
            return null;
        }

        // \SystemRoot\System32\… (comum em serviços) e só o nome ("ctfmon.exe") resolvem para a pasta do sistema.
        if (path.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
        {
            path = Path.Combine(Path.GetDirectoryName(systemDirectory) ?? @"C:\Windows", path[@"\SystemRoot\".Length..]);
        }
        else if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(systemDirectory, path.Contains('.', StringComparison.Ordinal) ? path : path + ".exe");
        }

        // rundll32 só hospeda: o arquivo que pode estar faltando é a DLL do primeiro argumento.
        if (Path.GetFileName(path).Equals("rundll32.exe", StringComparison.OrdinalIgnoreCase))
        {
            var target = rest.Trim().Split(',')[0].Trim().Trim('"');
            if (target.Length > 0)
            {
                return Path.IsPathRooted(target) ? target : Path.Combine(systemDirectory, target);
            }
        }

        return path;
    }

    /// <summary>
    /// Pasta temporária (onde instaladores e downloads descompactam): entrada de inicialização apontando para lá é
    /// sinal clássico de persistência indevida — instaladores legítimos copiam para Program Files ou AppData.
    /// </summary>
    public static bool IsTemporaryLocation(string? path, string? tempDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        tempDirectory ??= Path.GetTempPath();
        var normalized = path.Replace('/', '\\');
        return normalized.StartsWith(tempDirectory.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\AppData\Local\Temp\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\Windows\Temp\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\Temporary Internet Files\", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(@"\INetCache\", StringComparison.OrdinalIgnoreCase);
    }
}
