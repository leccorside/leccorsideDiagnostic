using LeccorWindowsHealthCenter.Application.Storage;

namespace LeccorWindowsHealthCenter.UnitTests.Storage;

/// <summary>Arquivos e pastas grandes reais de uma máquina de desenvolvimento (crítica da tela Armazenamento, 19/10/2026).</summary>
public class StorageHintsTests
{
    [Theory]
    [InlineData(@"C:\Users\Jo\AppData\Local\Docker\wsl\disk\docker_data.vhdx", "Docker Desktop")]
    [InlineData(@"C:\Users\Jo\AppData\Local\Packages\CanonicalGroupLimited.Ubuntu_79rhkp1fndgsc\LocalState\ext4.vhdx", "WSL")]
    [InlineData(@"C:\Users\Jo\AppData\Local\wsl\{2b4c}\ext4.vhdx", "WSL")]
    [InlineData(@"D:\VMs\Win11-dev.vhdx", "Hyper-V")]
    [InlineData(@"C:\Users\Jo\.ollama\models\blobs\sha256-fb2cb9aa5407264aec6698faf41183f337809352abe0bd8f", "Ollama")]
    [InlineData(@"C:\Users\Jo\.docker\models\bundles\sha256\1ab863c770fb76\model-00001-of-00003.gguf", "Docker Model Runner")]
    public void Developer_disks_and_models_say_what_they_are_and_not_to_delete(string path, string expected)
    {
        var hint = StorageHints.ForFile(path);

        Assert.NotNull(hint);
        Assert.Contains(expected, hint.Text, StringComparison.Ordinal);
        Assert.True(hint.DoNotDelete);
    }

    [Fact]
    public void Docker_hint_says_what_prune_deletes_and_offers_only_a_read_only_command()
    {
        var hint = StorageHints.ForFile(@"C:\Users\Jo\AppData\Local\Docker\wsl\disk\docker_data.vhdx")!;

        Assert.Contains("remove contêineres parados, imagens sem uso e o cache de build", hint.Text, StringComparison.Ordinal);
        Assert.Equal("docker system df", hint.Command);
    }

    [Theory]
    [InlineData(@"C:\pagefile.sys", true)]
    [InlineData(@"C:\Users\Jo\Downloads\SW_DVD9_Win_Pro_10_22H2.ISO", false)]
    [InlineData(@"C:\Windows\MEMORY.DMP", false)]
    public void Known_system_and_installer_files(string path, bool doNotDelete) =>
        Assert.Equal(doNotDelete, StorageHints.ForFile(path)!.DoNotDelete);

    [Fact]
    public void Unknown_files_get_no_guess() => Assert.Null(StorageHints.ForFile(@"C:\Users\Jo\Videos\viagem.mp4"));

    [Theory]
    [InlineData("node_modules", "npm install")]
    [InlineData("WinSxS", "Nunca apagar")]
    public void Known_folders(string name, string expected) =>
        Assert.Contains(expected, StorageHints.ForFolder(name)!.Text, StringComparison.Ordinal);

    [Fact]
    public void Developer_caches_are_measured_with_their_official_cleanup_command()
    {
        var developer = StorageLocations.All.Where(l => l.Kind == StorageLocationKind.DeveloperCache).ToList();

        Assert.Contains(developer, l => l.Id == "npm-cache");
        Assert.Contains(developer, l => l.Id == "nuget-packages");
        Assert.Contains(developer, l => l.Id == "gradle-cache");
        Assert.Contains(developer, l => l.Id == "pip-cache");
        Assert.All(StorageLocations.All, l => Assert.DoesNotContain("chega na tela", l.Description, StringComparison.Ordinal));
    }
}
