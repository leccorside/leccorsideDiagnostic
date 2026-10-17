using LeccorWindowsHealthCenter.Application.Rules;

namespace LeccorWindowsHealthCenter.UnitTests.Rules;

public class MetricSeriesTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static MetricSeries Series(params double[] values) =>
        new("m", values.Select((v, i) => Findings.Sample("m", v, Start.AddSeconds(i * 10))).ToList());

    [Fact]
    public void Empty_series_has_safe_defaults()
    {
        var series = Series();

        Assert.True(series.IsEmpty);
        Assert.Equal(0, series.Mean);
        Assert.Null(series.Latest);
        Assert.Equal(TimeSpan.Zero, series.Duration);
        Assert.Equal(0, series.FractionAtOrAbove(50));
    }

    [Fact]
    public void Computes_basic_statistics()
    {
        var series = Series(10, 20, 30, 40);

        Assert.Equal(25, series.Mean);
        Assert.Equal(40, series.Max);
        Assert.Equal(10, series.Min);
        Assert.Equal(40, series.Latest);
        Assert.Equal(TimeSpan.FromSeconds(30), series.Duration);
        Assert.Equal(0.5, series.FractionAtOrAbove(30));
    }

    [Fact]
    public void Percentile_interpolates()
    {
        var series = Series(1, 2, 3, 4, 5);

        Assert.Equal(3, series.Percentile(50));
        Assert.Equal(4.6, series.Percentile(90), precision: 6);
        Assert.Equal(5, series.Percentile(100));
    }

    [Fact]
    public void Longest_run_measures_continuous_time_above_threshold()
    {
        // 95 de 10s a 40s (30s contínuos), depois cai e volta por 10s.
        var series = Series(10, 95, 95, 95, 95, 10, 95, 95);

        Assert.Equal(TimeSpan.FromSeconds(30), series.LongestRunAtOrAbove(90));
        Assert.Equal(2, series.EpisodesAtOrAbove(90));
    }
}
