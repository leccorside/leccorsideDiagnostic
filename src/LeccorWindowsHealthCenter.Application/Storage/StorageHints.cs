namespace LeccorWindowsHealthCenter.Application.Storage;

/// <summary>Explicação de um item grande conhecido: o que é e o jeito seguro de liberar espaço.</summary>
/// <param name="Text">O que é e como liberar espaço, em uma frase.</param>
/// <param name="DoNotDelete">Apagar o arquivo/pasta diretamente quebra algo (disco virtual, modelo registrado, sistema).</param>
/// <param name="Command">Comando seguro (só consulta) para ver o uso antes de decidir; copiável pelo menu da linha.</param>
public sealed record StorageHint(string Text, bool DoNotDelete, string? Command = null);

/// <summary>
/// Reconhece arquivos e pastas grandes comuns numa máquina de desenvolvimento (PRODUCT.md: Docker, WSL, Node, .NET,
/// Java, Python, modelos de IA) e do Windows. Só explica — nada é apagado. Sem reconhecimento, devolve null (nunca
/// um palpite).
/// </summary>
public static class StorageHints
{
    private static readonly Dictionary<string, StorageHint> SystemFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pagefile.sys"] = new("Arquivo de paginação do Windows (memória virtual). O Windows gerencia o tamanho.", true),
        ["swapfile.sys"] = new("Arquivo de troca dos aplicativos da Store. O Windows gerencia o tamanho.", true),
        ["hiberfil.sys"] = new("Arquivo de hibernação e inicialização rápida. Some se a hibernação for desativada (powercfg /hibernate off).", true),
        ["MEMORY.DMP"] = new("Despejo de memória de um travamento. Útil para investigar a falha; depois disso pode ser apagado.", false),
    };

    private static readonly Dictionary<string, StorageHint> Folders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["node_modules"] = new("Dependências de um projeto Node. Recriadas com npm install; apagar só com o projeto fechado.", false),
        [".venv"] = new("Ambiente virtual Python de um projeto. Recriado com pip install -r requirements.txt.", false),
        ["venv"] = new("Ambiente virtual Python de um projeto. Recriado com pip install -r requirements.txt.", false),
        ["__pycache__"] = new("Bytecode Python em cache. Recriado automaticamente.", false),
        [".gradle"] = new("Cache do Gradle. Recriado no próximo build.", false),
        [".m2"] = new("Repositório local do Maven. Dependências baixadas de novo quando necessário.", false),
        [".nuget"] = new("Pacotes NuGet baixados. Limpe com dotnet nuget locals all --clear.", false),
        [".ollama"] = new("Modelos do Ollama. Remova modelos com ollama rm <modelo>, não apagando arquivos.", true, "ollama list"),
        ["WinSxS"] = new("Componentes do Windows. Nunca apagar; reduza com dism /online /cleanup-image /startcomponentcleanup.", true, "dism /online /cleanup-image /analyzecomponentstore"),
        ["Installer"] = new("Instaladores de programas usados para reparar e desinstalar. Nunca apagar à mão.", true),
    };

    /// <summary>Explicação de um arquivo pelo caminho completo, ou null quando não é reconhecido.</summary>
    public static StorageHint? ForFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var name = Path.GetFileName(path);
        if (SystemFiles.TryGetValue(name, out var system))
        {
            return system;
        }

        var extension = Path.GetExtension(name);
        if (extension.Equals(".vhdx", StringComparison.OrdinalIgnoreCase) || extension.Equals(".vhd", StringComparison.OrdinalIgnoreCase))
        {
            if (Contains(path, @"\Docker\"))
            {
                // prune apaga dados: a dica diz o quê, e o comando copiável só consulta (docker system df).
                return new("Disco virtual do Docker Desktop (imagens, contêineres, volumes e cache de build). Não apague o arquivo. Veja o uso com docker system df; docker system prune remove contêineres parados, imagens sem uso e o cache de build. Para devolver o espaço ao Windows, use Troubleshoot › Clean / Purge data no Docker Desktop.", true, "docker system df");
            }

            if (Contains(path, @"\Packages\") || Contains(path, @"\wsl\") || name.Equals("ext4.vhdx", StringComparison.OrdinalIgnoreCase))
            {
                return new("Disco virtual de uma distribuição WSL. Não apague: perde a distribuição inteira. Libere espaço dentro dela; depois, wsl --manage <distro> --set-sparse true devolve o espaço ao Windows.", true, "wsl --list --verbose");
            }

            return new("Disco virtual (Hyper-V ou outra ferramenta). Não apague sem saber qual máquina virtual o usa.", true);
        }

        if (Contains(path, @"\.ollama\models\"))
        {
            return new("Modelo de IA baixado pelo Ollama. Remova com ollama rm <modelo>, não apagando o arquivo.", true, "ollama list");
        }

        if (Contains(path, @"\.docker\models\"))
        {
            return new("Modelo de IA do Docker Model Runner. Remova com docker model rm <modelo>.", true, "docker model ls");
        }

        return extension.ToUpperInvariant() switch
        {
            ".GGUF" or ".SAFETENSORS" or ".CKPT" => new("Arquivo de modelo de IA. Confira qual ferramenta o usa antes de apagar.", false),
            ".ISO" or ".IMG" => new("Imagem de disco (instalador). Depois de usada, costuma poder ser apagada; confira se não é seu único instalador.", false),
            ".DMP" => new("Despejo de memória de um travamento. Útil para investigar a falha; depois disso pode ser apagado.", false),
            _ => null,
        };
    }

    /// <summary>Explicação de uma pasta pelo nome, ou null quando não é reconhecida.</summary>
    public static StorageHint? ForFolder(string name) =>
        Folders.TryGetValue(name ?? string.Empty, out var hint) ? hint : null;

    private static bool Contains(string path, string fragment) => path.Contains(fragment, StringComparison.OrdinalIgnoreCase);
}
