using LeccorWindowsHealthCenter.App.ViewModels;
using LeccorWindowsHealthCenter.Diagnostics.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.SystemTests;

/// <summary>Regressões do break-ui da tela Armazenamento (19/10/2026).</summary>
public sealed class StorageScreenTests
{
    private const double Gb = 1024d * 1024 * 1024;

    [Fact]
    public void Long_paths_are_shortened_in_the_middle_keeping_the_last_folder()
    {
        const string path = @"C:\Users\Aleksandra Wiśniewska-Kowalczyk\OneDrive - Northwind Industries Holdings Comércio e Participações Ltda\Projetos";

        var shortened = StorageViewModel.ShortPath(path);

        Assert.StartsWith(@"C:\Users\…\", shortened, StringComparison.Ordinal);
        Assert.EndsWith(@"\Projetos", shortened, StringComparison.Ordinal);
        Assert.True(shortened.Length <= 64, shortened);
    }

    [Theory]
    [InlineData(1L, "menos de 1 MB")]
    [InlineData(512L, "menos de 1 MB")]
    [InlineData(0L, "0 B")]
    public void Tiny_totals_are_approximated(long bytes, string expected)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        try
        {
            Assert.Equal(expected, StorageViewModel.ApproxBytes(bytes));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Short_paths_are_untouched() => Assert.Equal(@"C:\Users\Jo\Downloads", StorageViewModel.ShortPath(@"C:\Users\Jo\Downloads"));

    [Theory]
    [InlineData(true, 3_998_000_000_000d, 2_100_000_000d, Severity.High)]   // C: de 4 TB com 2 GB livres
    [InlineData(true, 894 * Gb, 45 * Gb, null)]                              // C: real desta máquina: sem alerta
    [InlineData(false, 2048 * Gb, 82 * Gb, null)]                            // dados: 4% de 2 TB ainda é muito
    [InlineData(false, 500 * Gb, 0.5 * Gb, Severity.Medium)]
    public void Volume_bar_uses_the_same_decision_as_the_scan(bool system, double size, double free, Severity? expected)
    {
        var rule = new LowFreeSpaceRule();

        var severity = LowFreeSpaceRule.Assess(system, size, free, key => rule.Parameters.Single(p => p.Key == key).DefaultValue);

        Assert.Equal(expected, severity);
    }
}
