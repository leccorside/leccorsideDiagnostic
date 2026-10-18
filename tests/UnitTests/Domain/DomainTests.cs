using System.Text;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;
using LeccorWindowsHealthCenter.Reporting;
using LeccorWindowsHealthCenter.Shared.Formatting;

namespace LeccorWindowsHealthCenter.UnitTests.Domain;

public class DomainTests
{
    [Theory]
    [InlineData(100, ConfidenceLevel.Confirmed)]
    [InlineData(90, ConfidenceLevel.Confirmed)]
    [InlineData(89, ConfidenceLevel.Likely)]
    [InlineData(60, ConfidenceLevel.Likely)]
    [InlineData(59, ConfidenceLevel.Possible)]
    [InlineData(0, ConfidenceLevel.Possible)]
    public void Confidence_levels(int percent, ConfidenceLevel expected) =>
        Assert.Equal(expected, new Confidence(percent).Level);

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Confidence_rejects_out_of_range(int percent) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Confidence(percent));

    [Theory]
    [InlineData(95, HealthRating.Excellent)]
    [InlineData(84, HealthRating.Good)]
    [InlineData(60, HealthRating.Attention)]
    [InlineData(10, HealthRating.Critical)]
    public void Health_rating_bands(int score, HealthRating expected) =>
        Assert.Equal(expected, HealthScore.RatingFor(score));

    [Fact]
    public void Measurement_comparison_reports_improvement_only_with_both_values()
    {
        var faster = new MeasurementComparison { Name = "Explorer", Unit = "s", Before = 12.4, After = 1.7 };
        var zeroBefore = new MeasurementComparison { Name = "x", Unit = "s", Before = 0, After = 1 };
        var higherIsBetter = new MeasurementComparison { Name = "Livre", Unit = "GB", Before = 10, After = 15, LowerIsBetter = false };

        Assert.Equal(86.3, faster.ImprovementPercent);
        Assert.Null(zeroBefore.ImprovementPercent);
        Assert.Equal(50, higherIsBetter.ImprovementPercent);
    }

    [Fact]
    public void Every_finding_category_maps_to_a_score_category()
    {
        foreach (var category in Enum.GetValues<FindingCategory>())
        {
            _ = CategoryMapping.ToScoreCategory(category);
        }
    }

    [Fact]
    public void Every_scan_module_covers_at_least_one_score_category()
    {
        foreach (var module in Enum.GetValues<ScanModule>())
        {
            Assert.NotEmpty(CategoryMapping.CoveredBy(module));
        }
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1_300_000_000_000, "1.2 TB")]
    public void Byte_size_formatting(long bytes, string expected) =>
        Assert.Equal(expected, ByteSize.Format(bytes, System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public void Byte_size_uses_brazilian_decimal_comma_in_pt_br() =>
        Assert.Equal("1,5 KB", ByteSize.Format(1536, System.Globalization.CultureInfo.GetCultureInfo("pt-BR")));

    [Fact]
    public async Task Json_report_contains_findings_and_evidence()
    {
        var report = new ScanReport
        {
            Mode = ScanMode.Full,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Score = new HealthScore { Overall = 82, Rating = HealthRating.Good },
            Results =
            [
                new DiagnosticResult
                {
                    ScannerId = "eventlog",
                    ScannerName = "Event Viewer",
                    Module = ScanModule.Events,
                    Status = ScannerRunStatus.Completed,
                    Findings = [Findings.Make("eventlog") with { Title = "Disco D: com erros de I/O" }],
                },
            ],
        };

        using var stream = new MemoryStream();
        await new JsonReportExporter().ExportAsync(report, stream, CancellationToken.None);
        var json = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("Disco D: com erros de I/O", json, StringComparison.Ordinal);
        Assert.Contains("\"Overall\": 82", json, StringComparison.Ordinal);
        Assert.Contains("\"Severity\": \"High\"", json, StringComparison.Ordinal);
    }
}
