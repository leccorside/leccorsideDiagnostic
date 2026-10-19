using System.Globalization;

namespace LeccorWindowsHealthCenter.Shared.Formatting;

public static class ByteSize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    /// <summary>
    /// Formata bytes em unidade legível (base 1024), ex.: "1,3 TB" em pt-BR.
    /// Sem <paramref name="provider"/>, usa a cultura atual (a da interface).
    /// </summary>
    public static string Format(long bytes, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        if (bytes < 0)
        {
            return "-" + Format(-bytes, provider);
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? string.Create(provider, $"{bytes:#,##0} B")
            : string.Create(provider, $"{value:#,##0.#} {Units[unit]}");
    }

    /// <summary>
    /// "parte de total" na MESMA unidade — a do menor valor — para a diferença continuar visível
    /// ("1.023,7 GB de 1.024 GB" em vez de "1023,7 GB de 1 TB" ou "1 TB de 1 TB").
    /// </summary>
    public static (string Part, string Total) FormatPair(long part, long total, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        var unit = 0;
        double reference = Math.Max(0, Math.Min(part, total));
        while (reference >= 1024 && unit < Units.Length - 1)
        {
            reference /= 1024;
            unit++;
        }

        string Fmt(long v, int u) => u == 0
            ? string.Create(provider, $"{v:#,##0} B")
            : string.Create(provider, $"{v / Math.Pow(1024, u):#,##0.#} {Units[u]}");

        // Valores diferentes que arredondam para o mesmo texto ("2 TB de 2 TB"): desce uma unidade até aparecer a diferença.
        while (unit > 0 && part != total && Fmt(part, unit) == Fmt(total, unit))
        {
            unit--;
        }

        return (Fmt(part, unit), Fmt(total, unit));
    }
}
