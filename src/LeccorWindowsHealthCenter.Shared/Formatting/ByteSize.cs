using System.Globalization;

namespace LeccorWindowsHealthCenter.Shared.Formatting;

public static class ByteSize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    /// <summary>A partir de 1.000 a unidade sobe (base 1024): 1.000 MB viram "0,98 GB".</summary>
    private const double UnitThreshold = 1000;

    /// <summary>Abaixo de 1, duas casas ("0,98"); senão uma, com separador de milhar ("1.023,7").</summary>
    private static string Number(double value, IFormatProvider provider) =>
        value < 1 ? value.ToString("0.##", provider) : value.ToString("#,##0.#", provider);

    /// <summary>
    /// Formata bytes em unidade legível (base 1024), ex.: "1,3 TB" em pt-BR. Muda de unidade a partir de 1.000
    /// ("0,99 GB", não "1.012,8 MB"): quatro dígitos antes da vírgula atrapalham a comparação entre linhas.
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
        while (value >= UnitThreshold && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? string.Create(provider, $"{bytes:#,##0} B")
            : $"{Number(value, provider)} {Units[unit]}";
    }

    /// <summary>
    /// "parte de total" na MESMA unidade — a do menor valor — para a diferença continuar visível
    /// ("1.023,7 GB de 1.024 GB" em vez de "1023,7 GB de 1 TB" ou "1 TB de 1 TB").
    /// </summary>
    public static (string Part, string Total) FormatPair(long part, long total, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        var unit = 0;

        // Zero não escolhe a unidade: "0 GB livres de 380 GB", nunca "0 B livres de 408.021.893.120 B".
        double reference = part <= 0 || total <= 0 ? Math.Max(0, Math.Max(part, total)) : Math.Min(part, total);
        while (reference >= UnitThreshold && unit < Units.Length - 1)
        {
            reference /= 1024;
            unit++;
        }

        string Fmt(long v, int u) => u == 0
            ? string.Create(provider, $"{v:#,##0} B")
            : $"{Number(v / Math.Pow(1024, u), provider)} {Units[u]}";

        // Valores diferentes que arredondam para o mesmo texto ("2 TB de 2 TB"): desce UMA unidade se isso mostrar a
        // diferença. Mais que isso vira ruído ("2.097.151,5 KB de 2.097.152 KB" por 512 bytes) — fica "2 GB de 2 GB".
        if (unit > 0 && part != total && Fmt(part, unit) == Fmt(total, unit) && Fmt(part, unit - 1) != Fmt(total, unit - 1))
        {
            unit--;
        }

        return (Fmt(part, unit), Fmt(total, unit));
    }
}
