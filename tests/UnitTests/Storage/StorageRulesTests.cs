using System.Globalization;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Storage;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeccorWindowsHealthCenter.UnitTests.Storage;

/// <summary>Cada regra de disco/armazenamento com um caso que NÃO deve disparar e um que deve (seção 49).</summary>
public class StorageRulesTests
{
    private const long Gb = 1024L * 1024 * 1024;

    private static List<Finding> Run(IRule rule, string scannerId, IEnumerable<Observation> observations, IEnumerable<Metric>? metrics = null)
    {
        var engine = new RuleEngine([rule], new FakeRuleSettings(), TimeProvider.System, NullLogger<RuleEngine>.Instance);
        var result = new DiagnosticResult
        {
            ScannerId = scannerId,
            ScannerName = scannerId,
            Module = ScanModule.Disks,
            Status = ScannerRunStatus.Completed,
            Metrics = metrics?.ToList() ?? [],
            Observations = observations.ToList(),
        };
        return engine.Evaluate([result]).SelectMany(r => r.Findings).ToList();
    }

    private static Observation Volume(string root, long size, long free, bool system = false, bool removable = false) => new()
    {
        Kind = StorageNames.ObservationVolume,
        Subject = root,
        ScannerId = DiskScanner.ScannerId,
        Attributes = new Dictionary<string, string>
        {
            ["root"] = root,
            ["sizeBytes"] = size.ToString(CultureInfo.InvariantCulture),
            ["freeBytes"] = free.ToString(CultureInfo.InvariantCulture),
            ["system"] = system.ToString(),
            ["removable"] = removable.ToString(),
        },
    };

    private static Observation Disk(
        string health = "Healthy",
        bool predicted = false,
        bool solidState = true,
        double? temperature = null,
        double? warning = null,
        double? critical = null,
        bool? trim = true,
        bool? ntfsTrim = true) => new()
    {
        Kind = StorageNames.ObservationDisk,
        Subject = "Samsung SSD 990 PRO 2TB",
        ScannerId = DiskScanner.ScannerId,
        Attributes = new Dictionary<string, string>
        {
            ["name"] = "Samsung SSD 990 PRO 2TB",
            ["health"] = health,
            ["predictedFailure"] = predicted.ToString(),
            ["operationalStatus"] = predicted ? "Falha prevista" : "OK",
            ["solidState"] = solidState.ToString(),
            ["temperature"] = StorageNames.Invariant(temperature),
            ["warningTemperature"] = StorageNames.Invariant(warning),
            ["criticalTemperature"] = StorageNames.Invariant(critical),
            ["trim"] = StorageNames.Invariant(trim),
            ["ntfsTrim"] = StorageNames.Invariant(ntfsTrim),
        },
    };

    // ---------- Pouco espaço ----------

    [Fact]
    public void System_volume_with_enough_space_is_fine()
    {
        // C: desta máquina: 64,9 GB livres de 894 GB (7%) — percentual baixo, mas espaço absoluto suficiente.
        Assert.Empty(Run(new LowFreeSpaceRule(), DiskScanner.ScannerId, [Volume(@"C:\", 894 * Gb, 65 * Gb, system: true)]));
    }

    [Theory]
    [InlineData(10, Severity.Medium)]
    [InlineData(3, Severity.High)]
    public void System_volume_below_limits(long freeGb, Severity expected)
    {
        var finding = Assert.Single(Run(new LowFreeSpaceRule(), DiskScanner.ScannerId, [Volume(@"C:\", 256 * Gb, freeGb * Gb, system: true)]));

        Assert.Equal(expected, finding.Severity);
        Assert.Equal(FindingCategory.Storage, finding.Category);
        Assert.Contains("C:", finding.Title, StringComparison.Ordinal);
        Assert.Contains(finding.Evidence, e => e.Value == freeGb * Gb);
    }

    [Fact]
    public void System_volume_under_1_gb_is_critical() =>
        Assert.Equal(Severity.Critical, Assert.Single(Run(new LowFreeSpaceRule(), DiskScanner.ScannerId, [Volume(@"C:\", 256 * Gb, Gb / 2, system: true)])).Severity);

    [Fact]
    public void Big_data_volume_at_4_percent_still_has_plenty_of_space()
    {
        // 4% de 2 TB = 82 GB livres: sem alerta.
        Assert.Empty(Run(new LowFreeSpaceRule(), DiskScanner.ScannerId, [Volume(@"D:\", 2048 * Gb, 82 * Gb)]));
    }

    [Fact]
    public void Small_data_volume_nearly_full_is_low_severity() =>
        Assert.Equal(Severity.Low, Assert.Single(Run(new LowFreeSpaceRule(), DiskScanner.ScannerId, [Volume(@"E:\", 120 * Gb, 3 * Gb)])).Severity);

    [Fact]
    public void Removable_drives_are_ignored() =>
        Assert.Empty(Run(new LowFreeSpaceRule(), DiskScanner.ScannerId, [Volume(@"F:\", 32 * Gb, 0, removable: true)]));

    // ---------- Saúde ----------

    [Fact]
    public void Healthy_disk_has_no_finding() => Assert.Empty(Run(new DiskHealthRule(), DiskScanner.ScannerId, [Disk()]));

    [Fact]
    public void Warning_status_is_high() =>
        Assert.Equal(Severity.High, Assert.Single(Run(new DiskHealthRule(), DiskScanner.ScannerId, [Disk(health: "Warning")])).Severity);

    [Fact]
    public void Predicted_failure_is_critical_with_both_evidences()
    {
        var finding = Assert.Single(Run(new DiskHealthRule(), DiskScanner.ScannerId, [Disk(predicted: true)]));

        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Equal(2, finding.Evidence.Count);
        Assert.Contains("backup", finding.Recommendation, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Temperatura ----------

    [Fact]
    public void Without_temperature_nothing_is_concluded() =>
        Assert.Empty(Run(new DiskTemperatureRule(), DiskScanner.ScannerId, [Disk(temperature: null)]));

    [Fact]
    public void Ssd_above_typical_limit_is_medium()
    {
        var finding = Assert.Single(Run(new DiskTemperatureRule(), DiskScanner.ScannerId, [Disk(temperature: 72)]));

        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.Contains(finding.Evidence, e => e.Source == EvidenceSource.Configuration);
    }

    [Fact]
    public void Device_declared_limit_wins_over_typical_limit() =>
        Assert.Empty(Run(new DiskTemperatureRule(), DiskScanner.ScannerId, [Disk(temperature: 75, warning: 82, critical: 85)]));

    [Fact]
    public void At_device_critical_limit_is_high() =>
        Assert.Equal(Severity.High, Assert.Single(Run(new DiskTemperatureRule(), DiskScanner.ScannerId, [Disk(temperature: 86, warning: 82, critical: 85)])).Severity);

    [Fact]
    public void Hdd_uses_its_own_lower_limit() =>
        Assert.Single(Run(new DiskTemperatureRule(), DiskScanner.ScannerId, [Disk(solidState: false, temperature: 57)]));

    // ---------- TRIM ----------

    [Fact]
    public void Ssd_with_trim_is_fine() => Assert.Empty(Run(new TrimDisabledRule(), DiskScanner.ScannerId, [Disk()]));

    [Fact]
    public void Unknown_trim_is_not_a_problem() =>
        Assert.Empty(Run(new TrimDisabledRule(), DiskScanner.ScannerId, [Disk(trim: null, ntfsTrim: null)]));

    [Fact]
    public void Trim_disabled_in_windows_on_ssd_is_low()
    {
        var finding = Assert.Single(Run(new TrimDisabledRule(), DiskScanner.ScannerId, [Disk(ntfsTrim: false)]));

        Assert.Equal(Severity.Low, finding.Severity);
        Assert.Contains("DisableDeleteNotify 0", finding.Recommendation, StringComparison.Ordinal);
    }

    [Fact]
    public void Hdd_without_trim_is_expected() =>
        Assert.Empty(Run(new TrimDisabledRule(), DiskScanner.ScannerId, [Disk(solidState: false, trim: false, ntfsTrim: false)]));

    // ---------- Temporários e caches ----------

    private static Metric TempCache(long bytes) => new()
    {
        Name = StorageNames.TempCacheTotal,
        Value = bytes,
        Unit = "bytes",
        ScannerId = StorageScanner.ScannerId,
    };

    [Fact]
    public void Small_temp_and_cache_is_not_mentioned() =>
        Assert.Empty(Run(new TempCacheLargeRule(), StorageScanner.ScannerId, [], [TempCache(4 * Gb)]));

    [Fact]
    public void Large_temp_and_cache_is_a_recommendation_not_a_problem()
    {
        var location = new Observation
        {
            Kind = StorageNames.ObservationLocation,
            Subject = "chrome-cache",
            ScannerId = StorageScanner.ScannerId,
            Attributes = new Dictionary<string, string>
            {
                ["title"] = "Cache do Google Chrome",
                ["kind"] = "Cache",
                ["sizeBytes"] = (6 * Gb).ToString(CultureInfo.InvariantCulture),
                ["files"] = "12345",
                ["inaccessible"] = "0",
            },
        };

        var finding = Assert.Single(Run(new TempCacheLargeRule(), StorageScanner.ScannerId, [location], [TempCache(6 * Gb)]));

        Assert.True(finding.IsRecommendation);
        Assert.Contains(finding.Evidence, e => e.Description.Contains("Chrome", StringComparison.Ordinal));
    }
}
