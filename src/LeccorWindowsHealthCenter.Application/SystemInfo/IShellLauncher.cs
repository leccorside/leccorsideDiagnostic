namespace LeccorWindowsHealthCenter.Application.SystemInfo;

/// <summary>
/// Mostra um arquivo ou pasta no Explorer pela API do shell (sem executar processo nem comando): pasta abre;
/// arquivo abre a pasta com ele selecionado. Nada é executado — um caminho para .exe nunca é aberto.
/// </summary>
public interface IShellLauncher
{
    /// <summary>Falso quando o caminho não existe mais ou o shell recusou.</summary>
    bool Reveal(string path);
}
