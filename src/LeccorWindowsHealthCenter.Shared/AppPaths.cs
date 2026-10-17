namespace LeccorWindowsHealthCenter.Shared;

/// <summary>
/// Diretórios de dados do aplicativo. Modo instalado usa %ProgramData%\LeccorWindowsHealthCenter;
/// modo portable (arquivo <c>portable.flag</c> ao lado do executável) usa a pasta <c>Data</c> local.
/// </summary>
public sealed class AppPaths
{
    public const string ProductFolderName = "LeccorWindowsHealthCenter";
    public const string PortableFlagFileName = "portable.flag";

    public AppPaths(string rootDirectory, bool isPortable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (!Path.IsPathFullyQualified(rootDirectory))
        {
            throw new ArgumentException("O diretório raiz precisa ser um caminho absoluto.", nameof(rootDirectory));
        }

        Root = rootDirectory;
        IsPortable = isPortable;
    }

    public string Root { get; }

    public bool IsPortable { get; }

    public string Logs => Path.Combine(Root, "Logs");

    public string Backups => Path.Combine(Root, "Backups");

    public string Reports => Path.Combine(Root, "Reports");

    public string Database => Path.Combine(Root, "Data", "health.db");

    public static AppPaths Resolve(string baseDirectory)
    {
        if (File.Exists(Path.Combine(baseDirectory, PortableFlagFileName)))
        {
            return new AppPaths(Path.Combine(baseDirectory, "Data"), isPortable: true);
        }

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return new AppPaths(Path.Combine(programData, ProductFolderName), isPortable: false);
    }

    /// <summary>
    /// Resolve os diretórios e garante que são graváveis. Se %ProgramData% não for gravável
    /// (ex.: pasta criada por outro usuário com ACL restrita), usa %LocalAppData%.
    /// </summary>
    public static AppPaths ResolveWritable(string baseDirectory)
    {
        var preferred = Resolve(baseDirectory);
        try
        {
            preferred.EnsureCreated();
            return preferred;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var fallback = new AppPaths(Path.Combine(local, ProductFolderName), isPortable: false);
            fallback.EnsureCreated();
            return fallback;
        }
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Backups);
        Directory.CreateDirectory(Reports);
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
    }
}
