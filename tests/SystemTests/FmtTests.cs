using System.Globalization;
using LeccorWindowsHealthCenter.App.ViewModels;

namespace LeccorWindowsHealthCenter.SystemTests;

/// <summary>Regressões do break-ui: plural, separador de milhar e leitura ausente.</summary>
public sealed class FmtTests : IDisposable
{
    private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

    public FmtTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
    }

    public void Dispose() => CultureInfo.CurrentCulture = _previous;

    [Theory]
    [InlineData(1, "1 núcleo")]
    [InlineData(8, "8 núcleos")]
    [InlineData(0, "0 núcleos")]
    public void Count_uses_singular_only_for_one(double value, string expected) =>
        Assert.Equal(expected, Fmt.Count(value, "núcleo", "núcleos"));

    [Fact]
    public void Count_has_thousands_separator() =>
        Assert.Equal("123.457 páginas/s", Fmt.Count(123_457, "página/s", "páginas/s"));

    [Fact]
    public void Whole_numbers_have_thousands_separator() => Assert.Equal("1.284.337", Fmt.Whole(1_284_337));

    [Theory]
    [InlineData(45, "45 s")]
    [InlineData(192, "3 min 12 s")]
    [InlineData(3_900, "1 h 05 min")]
    [InlineData(13_521_000, "156 d 11 h")] // 3.755 h 50 min: horas soltas não sobem para dias
    public void Duration_rolls_up_to_days(double seconds, string expected) =>
        Assert.Equal(expected, Fmt.Duration(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Missing_reading_is_a_dash_that_fits_any_width()
    {
        Assert.Equal("—", Fmt.Reading(null));
        Assert.Equal("47%", Fmt.Reading(47.4));
        Assert.Equal(Fmt.NotAvailable, Fmt.Count(null, "a", "b"));
    }
}
