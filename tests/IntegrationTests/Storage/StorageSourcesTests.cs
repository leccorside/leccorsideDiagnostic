using LeccorWindowsHealthCenter.Application.Storage;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Infrastructure.Storage;
using LeccorWindowsHealthCenter.Infrastructure.SystemInfo;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.IntegrationTests.Storage;

/// <summary>
/// Fontes reais de armazenamento (somente leitura) e o analisador de espaço sobre uma árvore temporária conhecida.
/// Nenhum arquivo fora do diretório temporário do teste é criado ou alterado.
/// </summary>
public class StorageSourcesTests
{
    private const long Mb = 1024 * 1024;

    private sealed class NotElevated : IPrivilegeService
    {
        public bool IsElevated => false;

        public string CurrentUser => @"PC\teste";
    }

    private static FileSystemSpaceAnalyzer Analyzer(SpaceAnalysisOptions? options = null) =>
        new(Options.Create(options ?? new SpaceAnalysisOptions { MinFolderBytes = Mb, OldFileMinimumBytes = Mb }), NullLogger<FileSystemSpaceAnalyzer>.Instance, TimeProvider.System);

    private static void WriteFile(string path, long bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        stream.SetLength(bytes); // arquivo esparso no tamanho lógico: rápido e sem gastar disco
    }

    [Fact]
    public async Task Storage_inventory_describes_the_system_volume()
    {
        using var provider = new WmiStorageInventoryProvider(new NotElevated(), NullLogger<WmiStorageInventoryProvider>.Instance, TimeProvider.System);

        var inventory = await provider.GetAsync(CancellationToken.None);

        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory)!;
        var system = Assert.Single(inventory.Volumes, v => v.IsSystem);
        Assert.Equal(systemRoot, system.Root, ignoreCase: true);
        Assert.True(system.SizeBytes > 0 && system.FreeBytes > 0 && system.FreeBytes < system.SizeBytes);
        Assert.Equal(new DriveInfo(systemRoot).TotalFreeSpace / Mb, system.FreeBytes / Mb, tolerance: 512); // mesma ordem que o Explorer mostra

        Assert.True(inventory.Disks.Count > 0, "Nenhum disco físico. Avisos: " + string.Join(" | ", inventory.Warnings));
        var systemDisk = Assert.Single(inventory.Disks, d => d.Number == system.DiskNumber);
        Assert.True(systemDisk.IsSystemDisk);
        Assert.NotEmpty(systemDisk.Partitions);

        // Sem administrador, vida útil e horas ligadas não são lidas — e nunca estimadas.
        Assert.False(inventory.ReadWithElevation);
        Assert.All(inventory.Disks, d => Assert.Null(d.WearPercent));
        Assert.All(inventory.Disks.Where(d => d.TemperatureCelsius is not null), d => Assert.InRange(d.TemperatureCelsius!.Value, 1, 149));
    }

    [Fact]
    public async Task Volume_disk_comes_from_the_volume_itself_not_only_from_wmi()
    {
        // Regressão: sob carga, MSFT_Partition estourou o timeout de 20 s e o C: ficou sem disco físico.
        using var provider = new WmiStorageInventoryProvider(new NotElevated(), NullLogger<WmiStorageInventoryProvider>.Instance, TimeProvider.System);
        var inventory = await provider.GetAsync(CancellationToken.None);
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory)!;

        var fromIoctl = StorageDeviceQuery.VolumeDiskNumber(systemRoot[0]);

        Assert.NotNull(fromIoctl);
        Assert.Equal(inventory.Volumes.Single(v => v.IsSystem).DiskNumber, fromIoctl);
        Assert.Contains(inventory.Disks, d => d.Number == fromIoctl);
    }

    [Fact]
    public async Task Analysis_sums_sizes_keeps_big_folders_and_lists_largest_and_old_files()
    {
        using var temp = new TempDirectory();
        WriteFile(Path.Combine(temp.Path, "videos", "a.mp4"), 5 * Mb);
        WriteFile(Path.Combine(temp.Path, "videos", "b.mp4"), 3 * Mb);
        WriteFile(Path.Combine(temp.Path, "videos", "sub", "c.mp4"), 2 * Mb);
        WriteFile(Path.Combine(temp.Path, "docs", "tiny.txt"), 100);
        WriteFile(Path.Combine(temp.Path, "root.bin"), 4 * Mb);
        var old = Path.Combine(temp.Path, "videos", "b.mp4");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddYears(-3));

        // Carimbo zerado (aparecia como "sem alteração desde 31/12/1969"): data inválida, não arquivo antigo.
        WriteFile(Path.Combine(temp.Path, "videos", "sub", "epoch.bin"), 2 * Mb);
        File.SetLastWriteTimeUtc(Path.Combine(temp.Path, "videos", "sub", "epoch.bin"), new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var analysis = await Analyzer().AnalyzeAsync(temp.Path, solidState: true, null, CancellationToken.None);

        Assert.Equal(16 * Mb + 100, analysis.Root.SizeBytes);
        Assert.Equal(6, analysis.Root.FileCount);
        Assert.Equal(4 * Mb, analysis.Root.FilesBytes);

        // "docs" (100 bytes) fica abaixo do mínimo e é somada, não listada.
        var videos = Assert.Single(analysis.Root.Children);
        Assert.Equal("videos", videos.Name);
        Assert.Equal(12 * Mb, videos.SizeBytes);
        Assert.Equal(1, analysis.Root.HiddenChildrenCount);
        Assert.Equal(100, analysis.Root.HiddenChildrenBytes);

        Assert.Equal(["a.mp4", "root.bin", "b.mp4"], analysis.LargestFiles.Take(3).Select(f => Path.GetFileName(f.Path)));
        Assert.Equal(old, Assert.Single(analysis.OldFiles).Path, ignoreCase: true);
    }

    [Fact]
    public async Task Inaccessible_folders_stay_listed_even_with_zero_bytes()
    {
        // break-ui: com 0 B, a pasta sem permissão sumia em "N pastas menores" e ninguém sabia qual era.
        using var temp = new TempDirectory();
        WriteFile(Path.Combine(temp.Path, "aberta", "f.bin"), 3 * Mb);
        var locked = Directory.CreateDirectory(Path.Combine(temp.Path, "trancada"));
        var user = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
        var deny = new System.Security.AccessControl.FileSystemAccessRule(user, System.Security.AccessControl.FileSystemRights.ListDirectory, System.Security.AccessControl.AccessControlType.Deny);
        var security = locked.GetAccessControl();
        security.AddAccessRule(deny);
        locked.SetAccessControl(security);
        try
        {
            var analysis = await Analyzer().AnalyzeAsync(temp.Path, solidState: true, null, CancellationToken.None);

            var node = Assert.Single(analysis.Root.Children, c => c.Name == "trancada");
            Assert.True(node.Inaccessible);
            Assert.Equal(1, analysis.Root.InaccessibleCount);
        }
        finally
        {
            security.RemoveAccessRule(deny);
            locked.SetAccessControl(security);
        }
    }

    [Fact]
    public async Task Dominant_small_folders_are_listed_instead_of_summed()
    {
        // Crítica: "1.284 pastas menores" com quase todo o conteúdo e sem navegação (node_modules com muitos pacotes).
        using var temp = new TempDirectory();
        for (var i = 0; i < 20; i++)
        {
            WriteFile(Path.Combine(temp.Path, "node_modules", $"pacote-{i:00}", "index.js"), 200 * 1024); // cada pacote abaixo do mínimo de 1 MB
        }

        var options = new SpaceAnalysisOptions { MinFolderBytes = Mb, MaxChildrenPerFolder = 5, DominantChildrenPerFolder = 500 };
        var analysis = await Analyzer(options).AnalyzeAsync(temp.Path, solidState: true, null, CancellationToken.None);

        var modules = Assert.Single(analysis.Root.Children);
        Assert.Equal(20, modules.Children.Count);
        Assert.Equal(0, modules.HiddenChildrenCount);
    }

    [Fact]
    public void Reveal_of_a_missing_path_returns_false_without_opening_anything()
    {
        var shell = new ShellLauncher(NullLogger<ShellLauncher>.Instance);

        Assert.False(shell.Reveal(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "nao-existe.txt")));
        Assert.False(shell.Reveal(string.Empty));
    }

    [Fact]
    public async Task Analysis_can_be_cancelled()
    {
        using var temp = new TempDirectory();
        for (var i = 0; i < 30; i++)
        {
            WriteFile(Path.Combine(temp.Path, $"d{i}", "f.bin"), 10);
        }

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Analyzer().AnalyzeAsync(temp.Path, solidState: true, null, cts.Token));
    }

    [Fact]
    public async Task Junctions_are_not_followed()
    {
        using var temp = new TempDirectory();
        var target = Path.Combine(temp.Path, "real");
        WriteFile(Path.Combine(target, "big.bin"), 3 * Mb);
        var link = Path.Combine(temp.Path, "link");

        // Junção (não exige administrador). Sem o cmd disponível, o teste não tem o que verificar.
        using (var mklink = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", ["/c", "mklink", "/J", link, target]) { CreateNoWindow = true, UseShellExecute = false }))
        {
            await mklink!.WaitForExitAsync();
        }

        Assert.True(Directory.Exists(link), "Não foi possível criar a junção de teste.");

        var analysis = await Analyzer().AnalyzeAsync(temp.Path, solidState: true, null, CancellationToken.None);

        Assert.Equal(3 * Mb, analysis.Root.SizeBytes); // contado uma vez, não duas
    }

    [Fact]
    public void Patterns_expand_one_wildcard_level_and_skip_missing_paths()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Default", "Cache"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "Profile 1", "Cache"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "System Profile"));

        var resolved = FileSystemSpaceAnalyzer.Resolve(Path.Combine(temp.Path, "*", "Cache"));

        Assert.Equal(2, resolved.Count);
        Assert.Empty(FileSystemSpaceAnalyzer.Resolve(@"%VARIAVEL_QUE_NAO_EXISTE%\Cache"));
    }

    [Fact]
    public async Task Known_locations_are_measured_without_deleting_anything()
    {
        var measures = await Analyzer().MeasureLocationsAsync(StorageLocations.All, CancellationToken.None);

        Assert.Equal(StorageLocations.All.Count, measures.Count);
        Assert.True(measures.Single(m => m.Location.Id == "user-temp").Found);
        Assert.True(measures.Single(m => m.Location.Id == StorageLocations.RecycleBinId).Found);
        Assert.All(measures, m => Assert.True(m.SizeBytes >= 0 && m.FileCount >= 0));
        Assert.True(Directory.Exists(Path.GetTempPath()));
    }
}
