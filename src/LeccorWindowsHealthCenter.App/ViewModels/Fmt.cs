using System.Globalization;
using LeccorWindowsHealthCenter.Shared.Formatting;

namespace LeccorWindowsHealthCenter.App.ViewModels;

/// <summary>Formatação de leituras na cultura da interface. Null sempre vira "não disponível" — nunca 0.</summary>
public static class Fmt
{
    public const string NotAvailable = "não disponível";

    private static CultureInfo Culture => CultureInfo.CurrentCulture;

    public static string Percent(double? value) =>
        value is { } v ? string.Create(Culture, $"{v:0}%") : NotAvailable;

    public static string Bytes(long? value) => value is { } v ? ByteSize.Format(v, Culture) : NotAvailable;

    public static string Bytes(double? value) => Bytes(value is { } v ? (long?)v : null);

    public static string GHz(double? megahertz) =>
        megahertz is { } mhz and > 0 ? string.Create(Culture, $"{mhz / 1000:0.00} GHz") : NotAvailable;

    public static string Number(double? value, string format = "0") =>
        value is { } v ? v.ToString(format, Culture) : NotAvailable;

    public static string Date(DateTimeOffset? value) =>
        value is { } v ? v.ToLocalTime().ToString("dd/MM/yyyy", Culture) : NotAvailable;

    public static string DateTime(DateTimeOffset? value) =>
        value is { } v ? v.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Culture) : NotAvailable;

    public static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? NotAvailable : value.Trim();

    /// <summary>Valor grande de leitura ao vivo: ausente vira "—" (cabe em qualquer largura; a legenda explica).</summary>
    public static string Reading(double? percent) => percent is { } v ? string.Create(Culture, $"{v:0}%") : "—";

    /// <summary>"1 núcleo" / "8 núcleos" — nunca "1 núcleos".</summary>
    public static string Count(double? value, string singular, string plural) =>
        value is { } v ? string.Create(Culture, $"{v:#,##0} {(Math.Abs(v - 1) < 0.5 ? singular : plural)}") : NotAvailable;

    /// <summary>"8 s", "3 min 12 s", "1 h 05 min".</summary>
    public static string Duration(TimeSpan value) => value.TotalHours >= 1
        ? string.Create(Culture, $"{(int)value.TotalHours} h {value.Minutes:00} min")
        : value.TotalMinutes >= 1
            ? string.Create(Culture, $"{(int)value.TotalMinutes} min {value.Seconds} s")
            : string.Create(Culture, $"{Math.Max(1, (int)Math.Round(value.TotalSeconds))} s");

    /// <summary>"x de y" na mesma unidade (ver <see cref="ByteSize.FormatPair"/>).</summary>
    public static string BytesOf(long? part, long? total)
    {
        if (part is not { } p || total is not { } t)
        {
            return NotAvailable;
        }

        var (partText, totalText) = ByteSize.FormatPair(p, t, Culture);
        return $"{partText} de {totalText}";
    }
}

/// <summary>Linha rótulo → valor dos cartões de informação. <see cref="Tone"/> colore valores de status.</summary>
public sealed record InfoRow(string Label, string Value, Application.SystemInfo.StatusTone Tone = Application.SystemInfo.StatusTone.Normal)
{
    public bool IsUnavailable => Value == Fmt.NotAvailable;

    public bool IsWarning => Tone == Application.SystemInfo.StatusTone.Warning;

    public bool IsCritical => Tone == Application.SystemInfo.StatusTone.Critical;
}

/// <summary>Página que precisa saber quando está visível (amostragem só com a tela aberta — seção 58).</summary>
public interface IPageLifecycle
{
    void Activate();

    void Deactivate();
}
