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

    public static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? NotAvailable : value;
}

/// <summary>Linha rótulo → valor dos cartões de informação.</summary>
public sealed record InfoRow(string Label, string Value)
{
    public bool IsUnavailable => Value == Fmt.NotAvailable;
}

/// <summary>Página que precisa saber quando está visível (amostragem só com a tela aberta — seção 58).</summary>
public interface IPageLifecycle
{
    void Activate();

    void Deactivate();
}
