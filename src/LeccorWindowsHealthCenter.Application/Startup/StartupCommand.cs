namespace LeccorWindowsHealthCenter.Application.Startup;

/// <summary>
/// Interpreta linhas de comando de inicialização como o Windows as grava — com aspas, sem aspas e com espaços no
/// caminho ("C:\Program Files\OpenVPN Connect\OpenVPNConnect.exe --opened-at-login"), com variáveis de ambiente, só
/// o nome do executável ("ctfmon.exe") ou via rundll32 (o que precisa existir é a DLL).
/// </summary>
public static partial class StartupCommand
{
    private static readonly string[] Executables = [".exe", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msc", ".lnk", ".dll", ".scr"];

    /// <summary>
    /// Caminho do arquivo que precisa existir para a entrada funcionar, ou null quando não dá para interpretar.
    /// <paramref name="expand"/> e <paramref name="systemDirectory"/> existem para os testes; o padrão usa o ambiente real.
    /// </summary>
    public static string? ExecutablePath(string? command, Func<string, string>? expand = null, string? systemDirectory = null)
    {
        systemDirectory ??= Environment.SystemDirectory;
        if (Parse(command, expand, systemDirectory) is not var (path, rest))
        {
            return null;
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

    /// <summary>Programas do Windows que só executam o que vem nos argumentos (o risco está no argumento, não no .exe).</summary>
    private static readonly HashSet<string> ScriptHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "powershell.exe", "pwsh.exe", "cmd.exe", "wscript.exe", "cscript.exe", "mshta.exe", "regsvr32.exe", "rundll32.exe",
    };

    /// <summary>Nome do hospedeiro de script que a linha inicia ("powershell.exe"), ou null quando é um programa comum.</summary>
    public static string? ScriptHostName(string? command, Func<string, string>? expand = null, string? systemDirectory = null) =>
        Parse(command, expand, systemDirectory ?? Environment.SystemDirectory) is var (host, _) && ScriptHosts.Contains(Path.GetFileName(host))
            ? Path.GetFileName(host).ToLowerInvariant()
            : null;

    /// <summary>
    /// Entrada do próprio Windows pelo executável: dentro da pasta do Windows, mas NÃO quando o executável é um
    /// hospedeiro de script (powershell, cmd, mshta…: quem decide o que roda é o argumento) nem quando está no
    /// DriverStore (drivers de terceiros, como NVIDIA e Realtek).
    /// </summary>
    public static bool IsWindowsOwned(string? command, string? windowsDirectory = null, Func<string, string>? expand = null, string? systemDirectory = null)
    {
        windowsDirectory ??= Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        systemDirectory ??= Environment.SystemDirectory;
        if (Parse(command, expand, systemDirectory) is not var (host, _) || ScriptHosts.Contains(Path.GetFileName(host)))
        {
            return false;
        }

        var exe = ExecutablePath(command, expand, systemDirectory);
        return exe is not null
            && exe.StartsWith(windowsDirectory.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)
            && !exe.Contains(@"\DriverStore\", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Motivo para desconfiar de um hospedeiro de script na inicialização, ou null. Só padrões que programas legítimos
    /// quase nunca usam no logon: PowerShell com comando codificado ou oculto baixando da internet, mshta/rundll32 com
    /// script embutido, regsvr32 com scriptlet, hospedeiro abrindo endereço da internet, script em pasta temporária.
    /// </summary>
    public static string? ScriptConcern(string? command, Func<string, string>? expand = null, string? systemDirectory = null)
    {
        systemDirectory ??= Environment.SystemDirectory;
        if (Parse(command, expand, systemDirectory) is not var (host, rest) || !ScriptHosts.Contains(Path.GetFileName(host)))
        {
            return null;
        }

        var name = Path.GetFileName(host).ToLowerInvariant();
        var args = rest.ToLowerInvariant();
        var powershell = name is "powershell.exe" or "pwsh.exe" || args.Contains("powershell", StringComparison.Ordinal) || args.Contains("pwsh", StringComparison.Ordinal);
        if (powershell && EncodedFlag().IsMatch(args))
        {
            return "Roda um comando do PowerShell codificado (-EncodedCommand): o que ele faz não aparece na linha de comando.";
        }

        var internet = args.Contains("http://", StringComparison.Ordinal) || args.Contains("https://", StringComparison.Ordinal);
        if (powershell && HiddenFlag().IsMatch(args) && (internet || args.Contains("downloadstring", StringComparison.Ordinal) || args.Contains("iex", StringComparison.Ordinal)))
        {
            return "Roda o PowerShell oculto baixando ou executando código da internet.";
        }

        if (name is "mshta.exe" or "rundll32.exe" && (args.Contains("javascript:", StringComparison.Ordinal) || args.Contains("vbscript:", StringComparison.Ordinal)))
        {
            return $"{Path.GetFileNameWithoutExtension(name)} executa um script embutido na própria linha de comando.";
        }

        if (name == "regsvr32.exe" && args.Contains("scrobj.dll", StringComparison.Ordinal))
        {
            return "regsvr32 carrega um scriptlet (scrobj.dll): técnica conhecida para rodar script sem deixar arquivo.";
        }

        if (internet)
        {
            return $"{Path.GetFileNameWithoutExtension(name)} abre um endereço da internet a cada logon.";
        }

        return name is "wscript.exe" or "cscript.exe" or "cmd.exe" && rest.Split('"', ' ').Any(part => IsTemporaryLocation(part))
            ? "Roda um script que está numa pasta temporária."
            : null;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"(?:^|\s)[-/](?:e|ec|en|enc|enco|encod|encode|encoded|encodedc\w*)\s+[a-z0-9+/=]{16,}")]
    private static partial System.Text.RegularExpressions.Regex EncodedFlag();

    [System.Text.RegularExpressions.GeneratedRegex(@"(?:^|\s)[-/]w(?:i|in|ind|indo|indow|indows|indowst\w*)?\s+(?:h|hi|hid|hidd|hidde|hidden|1)\b")]
    private static partial System.Text.RegularExpressions.Regex HiddenFlag();

    /// <summary>Executável que o Windows inicia (sem resolver o alvo do rundll32) e o resto da linha.</summary>
    private static (string Host, string Args)? Parse(string? command, Func<string, string>? expand, string systemDirectory)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        expand ??= Environment.ExpandEnvironmentVariables;
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
        else if (text.IndexOf(' ') is var firstSpace and > 0 && text[..firstSpace].IndexOfAny(['\\', '/', ':']) < 0)
        {
            // Só o nome ("regsvr32 /s … scrobj.dll", "powershell -w hidden …"): o programa vai até o primeiro espaço.
            // Sem isto, a busca pela extensão abaixo achava o ".dll" do fim e tratava a linha inteira como caminho.
            path = text[..firstSpace];
            rest = text[firstSpace..];
        }
        else
        {
            // Caminho sem aspas: vai até a primeira extensão executável (o caminho pode ter espaços), senão até o primeiro espaço.
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

        return (path, rest);
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
