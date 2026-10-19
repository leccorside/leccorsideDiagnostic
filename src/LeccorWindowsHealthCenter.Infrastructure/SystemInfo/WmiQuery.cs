using System.Globalization;
using System.Management;

namespace LeccorWindowsHealthCenter.Infrastructure.SystemInfo;

/// <summary>
/// Consultas WMI somente leitura compartilhadas pelos provedores de inventário. Cada consulta é isolada:
/// falha vira aviso e devolve lista vazia, para as demais fontes seguirem.
/// </summary>
internal static class WmiQuery
{
    public const string Cimv2 = @"\\.\root\cimv2";
    public const string StorageScope = @"\\.\root\Microsoft\Windows\Storage";

    // Só o timeout é alterado. No System.Management do .NET 10, Rewindable = false + ReturnImmediately devolve
    // coleções vazias em silêncio (verificado neste projeto; no .NET Framework funciona) — não "otimizar" de novo.
    private static readonly System.Management.EnumerationOptions QueryOptions = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static List<ManagementBaseObject> Query(string scope, string wql, List<string> warnings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var searcher = new ManagementObjectSearcher(new ManagementScope(scope), new ObjectQuery(wql), QueryOptions);
            using var results = searcher.Get();
            return results.Cast<ManagementBaseObject>().ToList();
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            warnings.Add($"{ClassOf(wql)}: {ex.Message}");
            return [];
        }
    }

    public static ManagementBaseObject? QueryFirst(string scope, string wql, List<string> warnings, CancellationToken cancellationToken) =>
        Query(scope, wql, warnings, cancellationToken).FirstOrDefault();

    public static string? Text(ManagementBaseObject? obj, string property)
    {
        var value = obj?[property]?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static int? Int(ManagementBaseObject? obj, string property) =>
        obj?[property] is { } value && int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    public static long? Long(ManagementBaseObject? obj, string property) =>
        obj?[property] is { } value && long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    public static bool? Bool(ManagementBaseObject? obj, string property) => obj?[property] as bool?;

    public static DateTimeOffset? Date(ManagementBaseObject? obj, string property)
    {
        var raw = Text(obj, property);
        if (raw is null)
        {
            return null;
        }

        try
        {
            return new DateTimeOffset(ManagementDateTimeConverter.ToDateTime(raw).ToUniversalTime(), TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string ClassOf(string wql)
    {
        var index = wql.IndexOf(" FROM ", StringComparison.OrdinalIgnoreCase);
        return index < 0 ? wql : wql[(index + 6)..].Trim();
    }
}
