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
            ? string.Create(provider, $"{bytes} B")
            : string.Create(provider, $"{value:0.#} {Units[unit]}");
    }
}
