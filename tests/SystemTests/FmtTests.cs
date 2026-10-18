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
    public void Missing_reading_is_a_dash_that_fits_any_width()
    {
        Assert.Equal("—", Fmt.Reading(null));
        Assert.Equal("47%", Fmt.Reading(47.4));
        Assert.Equal(Fmt.NotAvailable, Fmt.Count(null, "a", "b"));
    }
}
