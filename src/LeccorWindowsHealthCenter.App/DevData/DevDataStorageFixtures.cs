#if DEBUG
using LeccorWindowsHealthCenter.Application.Storage;
using LeccorWindowsHealthCenter.Application.SystemInfo;

namespace LeccorWindowsHealthCenter.App.DevData;

/// <summary>
/// Fixtures da tela Armazenamento (break-ui, Passo 3). SÓ EM DEBUG. Valores que máquinas reais produzem: nomes de
/// disco como o firmware os devolve, rótulo de volume no limite de 32 caracteres do NTFS, VHDs do Docker/WSL sem letra,
/// leitor de cartão sem mídia, pastas do OneDrive corporativo, node_modules aninhado, nomes com acento, CJK e emoji.
/// </summary>
public static class DevDataStorageFixtures
{
    private const long Gb = 1024L * 1024 * 1024;
    private const long Mb = 1024L * 1024;

    // ---------------- Inventário ----------------

    public static StorageInventory Inventory(DevDataMode mode) => mode switch
    {
        DevDataMode.Empty => new StorageInventory { CapturedAt = DateTimeOffset.UtcNow },
        DevDataMode.One => new StorageInventory
        {
            Disks = [new PhysicalDiskInfo { Number = 0, Name = "Msft Virtual Disk", Bus = DiskBusKind.Virtual, Media = DiskMediaKind.Unknown, SizeBytes = 64 * Gb, Health = DiskHealth.Healthy, OperationalStatus = ["OK"], PartitionStyle = "GPT", IsSystemDisk = true, Partitions = [new PartitionInfo { Number = 1, DriveLetter = 'C', SizeBytes = 64 * Gb }] }],
            Volumes = [new VolumeInfo { Root = @"C:\", FileSystem = "NTFS", SizeBytes = 64 * Gb, FreeBytes = 1 * Gb, DiskNumber = 0, IsSystem = true, Health = "Healthy" }],
            TrimEnabledForNtfs = true,
            CapturedAt = DateTimeOffset.UtcNow,
        },
        DevDataMode.Many => ManyDisks(),
        _ => Worst(),
    };

    private static StorageInventory Worst() => new()
    {
        ReadWithElevation = true,
        TrimEnabledForNtfs = false,
        Disks =
        [
            new PhysicalDiskInfo
            {
                Number = 0, Name = "Samsung SSD 990 PRO with Heatsink 4TB", Firmware = "4B2QJXD7", Bus = DiskBusKind.Nvme, Media = DiskMediaKind.Ssd,
                SizeBytes = 4_000_787_030_016, Health = DiskHealth.Healthy, OperationalStatus = ["OK"], PartitionStyle = "GPT", IsSystemDisk = true,
                TrimEnabled = true, TemperatureCelsius = 74, WarningTemperatureCelsius = 82, CriticalTemperatureCelsius = 85, WearPercent = 3, PowerOnHours = 6_214,
                Partitions =
                [
                    new PartitionInfo { Number = 1, SizeBytes = 100 * Mb, Kind = PartitionKind.Efi },
                    new PartitionInfo { Number = 2, SizeBytes = 16 * Mb, Kind = PartitionKind.Msr },
                    new PartitionInfo { Number = 3, DriveLetter = 'C', SizeBytes = 3_998_000_000_000, Kind = PartitionKind.Data },
                    new PartitionInfo { Number = 4, SizeBytes = 750 * Mb, Kind = PartitionKind.Recovery },
                ],
            },
            new PhysicalDiskInfo
            {
                // HDD velho de NAS reaproveitado: falha prevista, quente, horas altíssimas.
                Number = 1, Name = "WDC WD40EFRX-68N32N0", Firmware = "82.00A82", Bus = DiskBusKind.Sata, Media = DiskMediaKind.Hdd,
                SizeBytes = 4_000_787_030_016, Health = DiskHealth.Warning, OperationalStatus = ["Falha prevista", "Degradado"], PredictedFailure = true,
                PartitionStyle = "GPT", TemperatureCelsius = 58, WearPercent = null, PowerOnHours = 61_337,
                Partitions = [new PartitionInfo { Number = 1, DriveLetter = 'E', SizeBytes = 4_000_000_000_000, Kind = PartitionKind.Data }],
            },
            new PhysicalDiskInfo
            {
                // VHDX do Docker/WSL montado: partições sem letra.
                Number = 2, Name = "Msft Virtual Disk", Bus = DiskBusKind.Virtual, Media = DiskMediaKind.Unknown, SizeBytes = 1_099_511_627_776,
                Health = DiskHealth.Healthy, OperationalStatus = ["OK"], PartitionStyle = "GPT",
                Partitions = [new PartitionInfo { Number = 1, SizeBytes = 1_099_000_000_000, Kind = PartitionKind.Other }],
            },
            new PhysicalDiskInfo
            {
                // Leitor de cartão sem cartão: sem tamanho, sem partições, sem estilo.
                Number = 3, Name = "Generic- SD/MMC/MS PRO USB Device", Bus = DiskBusKind.Usb, Media = DiskMediaKind.Unknown, Health = DiskHealth.Unknown,
            },
            new PhysicalDiskInfo
            {
                // SSD SATA com TRIM desligado pelo controlador RAID: muitas partições de recuperação de fabricante.
                Number = 4, Name = "KINGSTON SA400S37960G", Firmware = "SEU20105", Bus = DiskBusKind.Raid, Media = DiskMediaKind.Ssd, SizeBytes = 960_197_124_096,
                Health = DiskHealth.Unhealthy, OperationalStatus = ["Código 53261"], PartitionStyle = "MBR", TrimEnabled = false,
                Partitions =
                [
                    new PartitionInfo { Number = 1, DriveLetter = 'D', SizeBytes = 500 * Gb, Kind = PartitionKind.Data },
                    new PartitionInfo { Number = 2, SizeBytes = 1 * Gb, Kind = PartitionKind.Recovery },
                    new PartitionInfo { Number = 3, SizeBytes = 1 * Gb, Kind = PartitionKind.Recovery },
                    new PartitionInfo { Number = 4, SizeBytes = 20 * Gb, Kind = PartitionKind.Other },
                    new PartitionInfo { Number = 5, DriveLetter = 'F', SizeBytes = 380 * Gb, Kind = PartitionKind.Data },
                ],
            },
        ],
        Volumes =
        [
            new VolumeInfo { Root = @"C:\", Label = "Windows", FileSystem = "NTFS", SizeBytes = 3_998_000_000_000, FreeBytes = 2_100_000_000, DiskNumber = 0, IsSystem = true, Health = "Healthy" },
            new VolumeInfo { Root = @"D:\", Label = "Backup Fotos e Documentos 2019-2", FileSystem = "NTFS", SizeBytes = 500 * Gb, FreeBytes = 499 * Gb, DiskNumber = 4, Health = "Warning" },
            new VolumeInfo { Root = @"E:\", Label = "Mídia — Séries & Filmes (NAS)", FileSystem = "NTFS", SizeBytes = 4_000_000_000_000, FreeBytes = 120 * Gb, DiskNumber = 1, Health = "Healthy" },
            new VolumeInfo { Root = @"F:\", Label = "写真", FileSystem = "exFAT", SizeBytes = 380 * Gb, FreeBytes = 0, DiskNumber = 4 },
            new VolumeInfo { Root = @"G:\", FileSystem = "NTFS", SizeBytes = 2 * Gb, FreeBytes = 2 * Gb - 512, IsRemovable = true },
            new VolumeInfo { Root = @"V:\", Label = "VeraCrypt", FileSystem = "NTFS", SizeBytes = 64 * Gb, FreeBytes = 63 * Gb },
        ],
        Warnings = ["MSFT_Partition: Tempo limite excedido", "MSFT_StorageReliabilityCounter: Acesso negado"],
        CapturedAt = DateTimeOffset.UtcNow,
    };

    /// <summary>Servidor/estação de mídia: 24 baias + 26 letras de volume (o máximo do Windows).</summary>
    private static StorageInventory ManyDisks()
    {
        var disks = Enumerable.Range(0, 24).Select(i => new PhysicalDiskInfo
        {
            Number = i,
            Name = i == 0 ? "Samsung SSD 980 1TB" : $"ST18000NM000J-2TV103 #{i:00}",
            Bus = i == 0 ? DiskBusKind.Nvme : DiskBusKind.Sas,
            Media = i == 0 ? DiskMediaKind.Ssd : DiskMediaKind.Hdd,
            SizeBytes = i == 0 ? 1_000_204_886_016 : 18_000_207_937_536,
            Health = DiskHealth.Healthy,
            OperationalStatus = ["OK"],
            PartitionStyle = "GPT",
            IsSystemDisk = i == 0,
            TrimEnabled = i == 0 ? true : null,
            Partitions = [new PartitionInfo { Number = 1, DriveLetter = (char)('C' + Math.Min(i, 23)), SizeBytes = 18_000_000_000_000 }],
        }).ToList();

        var volumes = Enumerable.Range(0, 24).Select(i => new VolumeInfo
        {
            Root = $@"{(char)('C' + i)}:\",
            Label = i == 0 ? "Sistema" : $"Arquivo {2001 + i}",
            FileSystem = "NTFS",
            SizeBytes = i == 0 ? 1_000_000_000_000 : 18_000_000_000_000,
            FreeBytes = i == 0 ? 400 * Gb : 3_000_000_000_000 - (i * 100 * Gb),
            DiskNumber = i,
            IsSystem = i == 0,
        }).ToList();

        return new StorageInventory { Disks = disks, Volumes = volumes, TrimEnabledForNtfs = true, CapturedAt = DateTimeOffset.UtcNow };
    }

    // ---------------- Temporários e caches ----------------

    public static IReadOnlyList<LocationMeasure> Locations(DevDataMode mode)
    {
        var all = StorageLocations.All;
        LocationMeasure M(string id, long bytes, long files = 0, int inaccessible = 0, bool found = true, params string[] paths) =>
            new() { Location = all.Single(l => l.Id == id), SizeBytes = bytes, FileCount = files, InaccessibleCount = inaccessible, Found = found, ResolvedPaths = paths };

        return mode switch
        {
            DevDataMode.Empty => all.Select(l => new LocationMeasure { Location = l, Found = false }).ToList(),
            DevDataMode.One => all.Select(l => l.Id == "user-temp" ? M(l.Id, 1, 1, 0, true, @"C:\Users\Jo\AppData\Local\Temp") : new LocationMeasure { Location = l, Found = false }).ToList(),
            _ =>
            [
                M("windows-update", 38_700 * Mb, 1_284, 0, true, @"C:\Windows\SoftwareDistribution\Download"),
                M("user-temp", 21_400 * Mb, 412_337, 3, true, @"C:\Users\Aleksandra Wiśniewska-Kowalczyk\AppData\Local\Temp"),
                M("chrome-cache", 9_800 * Mb, 88_402, 0, true,
                    @"C:\Users\Aleksandra Wiśniewska-Kowalczyk\AppData\Local\Google\Chrome\User Data\Default\Cache",
                    @"C:\Users\Aleksandra Wiśniewska-Kowalczyk\AppData\Local\Google\Chrome\User Data\Profile 1\Cache",
                    @"C:\Users\Aleksandra Wiśniewska-Kowalczyk\AppData\Local\Google\Chrome\User Data\Profile 7\Code Cache"),
                M("crash-dumps", 0, 0, 2, true, @"C:\Windows\Minidump"),
                M("windows-temp", 0, 0, 17, true, @"C:\Windows\Temp"),
                M("edge-cache", 0, 0, 0, false),
                M("thumbnail-cache", 1, 1, 0, true, @"C:\Users\Jo\AppData\Local\Microsoft\Windows\Explorer"),
                M(StorageLocations.RecycleBinId, 0, 0, 0, false),
            ],
        };
    }

    // ---------------- Análise de espaço ----------------

    public static SpaceAnalysis Analysis(string root, DevDataMode mode)
    {
        var drive = root.TrimEnd('\\');
        return mode switch
        {
            DevDataMode.Empty => new SpaceAnalysis { Root = new FolderNode { Name = root, Path = root }, Duration = TimeSpan.FromMilliseconds(40) },
            DevDataMode.One => new SpaceAnalysis
            {
                Root = new FolderNode
                {
                    Name = root, Path = root, SizeBytes = 2 * Mb, FileCount = 1,
                    Children = [new FolderNode { Name = "Jo", Path = $@"{drive}\Jo", SizeBytes = 2 * Mb, FileCount = 1, FilesBytes = 2 * Mb }],
                },
                LargestFiles = [new FileEntry($@"{drive}\Jo\a.txt", 2 * Mb, DateTimeOffset.UtcNow.AddDays(-1))],
                Duration = TimeSpan.FromMilliseconds(900),
            },
            DevDataMode.Many => ManyAnalysis(root),
            _ => WorstAnalysis(root),
        };
    }

    private static SpaceAnalysis WorstAnalysis(string root)
    {
        var drive = root.TrimEnd('\\');
        var user = $@"{drive}\Users\Aleksandra Wiśniewska-Kowalczyk";
        var oneDrive = $@"{user}\OneDrive - Northwind Industries Holdings Comércio e Participações Ltda";
        var nested = $@"{user}\source\repos\northwind-industries-holdings-portal-do-cliente-frontend\node_modules\@northwind-industries-holdings\design-system-components-react\node_modules\@babel\plugin-transform-async-generator-functions";

        var deep = new FolderNode { Name = "plugin-transform-async-generator-functions", Path = nested, SizeBytes = 3 * Mb, FileCount = 41, FilesBytes = 3 * Mb };
        var repos = new FolderNode
        {
            Name = "northwind-industries-holdings-portal-do-cliente-frontend", Path = $@"{user}\source\repos\northwind-industries-holdings-portal-do-cliente-frontend",
            SizeBytes = 2_400 * Mb, FileCount = 184_233, Children = [deep], HiddenChildrenCount = 1_284, HiddenChildrenBytes = 2_397 * Mb, InaccessibleCount = 0,
        };
        var oneDriveNode = new FolderNode
        {
            Name = "OneDrive - Northwind Industries Holdings Comércio e Participações Ltda", Path = oneDrive,
            SizeBytes = 1, FileCount = 1, FilesBytes = 1, CloudOnlyBytes = 812 * Gb,
        };
        var users = new FolderNode
        {
            Name = "Aleksandra Wiśniewska-Kowalczyk", Path = user, SizeBytes = 1_190 * Gb, FileCount = 2_608_651, FilesBytes = 12 * Mb,
            Children =
            [
                new FolderNode { Name = "AppData", Path = $@"{user}\AppData", SizeBytes = 1_150 * Gb, FileCount = 1_900_112, InaccessibleCount = 658 },
                new FolderNode { Name = "📸 Fotos 2024 — Viagem Lençóis Maranhenses", Path = $@"{user}\Pictures\📸 Fotos 2024 — Viagem Lençóis Maranhenses", SizeBytes = 37 * Gb, FileCount = 12_480 },
                repos,
                new FolderNode { Name = "Área de Trabalho", Path = $@"{user}\Área de Trabalho", SizeBytes = 0, Inaccessible = true, InaccessibleCount = 1 },
                oneDriveNode,
            ],
            InaccessibleCount = 659, CloudOnlyBytes = 812 * Gb,
        };

        var rootNode = new FolderNode
        {
            Name = root, Path = root, SizeBytes = 4_100 * Gb, FileCount = 2_608_651, FilesBytes = 60 * Gb,
            Children =
            [
                new FolderNode { Name = "Users", Path = $@"{drive}\Users", SizeBytes = 1_190 * Gb, FileCount = 2_608_651, Children = [users], InaccessibleCount = 659, CloudOnlyBytes = 812 * Gb },
                new FolderNode { Name = "Windows", Path = $@"{drive}\Windows", SizeBytes = 2_800 * Gb, FileCount = 213_235 },
                new FolderNode { Name = "$Recycle.Bin", Path = $@"{drive}\$Recycle.Bin", SizeBytes = 1 * Gb, Inaccessible = true, InaccessibleCount = 1 },
            ],
            HiddenChildrenCount = 1, HiddenChildrenBytes = 512,
            InaccessibleCount = 660, CloudOnlyBytes = 812 * Gb,
        };

        DateTimeOffset Days(int days) => DateTimeOffset.UtcNow.AddDays(-days);
        return new SpaceAnalysis
        {
            Root = rootNode,
            LargestFiles =
            [
                new FileEntry($@"{user}\AppData\Local\Docker\wsl\disk\docker_data.vhdx", 1_024 * Gb, Days(0)),
                new FileEntry($@"{drive}\pagefile.sys", 64 * Gb, Days(0)),
                new FileEntry($@"{user}\.ollama\models\blobs\sha256-fb2cb9aa5407264aec6698faf41183f337809352abe0bd8f0a7c6c7e7b1a0c0d", 27 * Gb, Days(27)),
                new FileEntry($@"{oneDrive}\Projetos\Relatório Anual de Sustentabilidade 2024 - versão final revisada (2) - Cópia.pdf", 1_200 * Mb, Days(3)),
                new FileEntry($@"{drive}\a", 1_023 * Mb, Days(400)),
                new FileEntry($@"{nested}\lib\index.js.map", 999, Days(1)),
            ],
            OldFiles =
            [
                new FileEntry($@"{user}\Downloads\SW_DVD9_Win_Pro_10_22H2.15_64BIT_PtBr_Pro_Ent_EDU_N_MLF_X23-67223.ISO", 5_800 * Mb, Days(1_500)),
                new FileEntry($@"{drive}\MEMORY.DMP", 2_100 * Mb, Days(800)),
            ],
            Duration = TimeSpan.FromHours(1) + TimeSpan.FromMinutes(5),
        };
    }

    /// <summary>Pasta com 50 subpastas listadas (o máximo) e milhares escondidas; 50 maiores e 50 antigos.</summary>
    private static SpaceAnalysis ManyAnalysis(string root)
    {
        var drive = root.TrimEnd('\\');
        var children = Enumerable.Range(0, 50)
            .Select(i => new FolderNode { Name = $"Projeto {i + 1:000} — cliente {1284 - i}", Path = $@"{drive}\Projetos\Projeto {i + 1:000}", SizeBytes = (50 - i) * 10 * Gb, FileCount = 12_345 * (50 - i), Children = [new FolderNode { Name = "src", Path = $@"{drive}\Projetos\Projeto {i + 1:000}\src", SizeBytes = 1 * Gb }] })
            .ToList();
        var total = children.Sum(c => c.SizeBytes) + 4 * Gb;
        var files = Enumerable.Range(0, 50).Select(i => new FileEntry($@"{drive}\Projetos\Projeto {i + 1:000}\render-final-{i:000}.mov", (50 - i) * Gb, DateTimeOffset.UtcNow.AddDays(-(800 + i)))).ToList();
        return new SpaceAnalysis
        {
            Root = new FolderNode
            {
                Name = root, Path = root, SizeBytes = total, FileCount = 9_876_543, Children = children,
                HiddenChildrenCount = 48_211, HiddenChildrenBytes = 4 * Gb, InaccessibleCount = 1,
            },
            LargestFiles = files,
            OldFiles = files,
            Duration = TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(7),
        };
    }
}
#endif
