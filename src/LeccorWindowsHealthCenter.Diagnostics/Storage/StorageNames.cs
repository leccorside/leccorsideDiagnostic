using System.Globalization;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Storage;

/// <summary>Nomes de métricas, observações e atributos produzidos pelos scanners de disco e armazenamento.</summary>
public static class StorageNames
{
    public const string ObservationDisk = "disk.physical";
    public const string ObservationVolume = "volume";
    public const string ObservationLocation = "storage.location";

    public const string VolumeFreePercent = "volume.free.percent";
    public const string VolumeFreeBytes = "volume.free.bytes";
    public const string DiskTemperature = "disk.temperature";
    public const string TempCacheTotal = "storage.temp_cache.bytes";

    public const string DriveTag = "drive";
    public const string DiskTag = "disk";

    /// <summary>Atributo numérico gravado em InvariantCulture; ausente ou vazio vira null (nunca 0).</summary>
    public static double? Number(this Observation observation, string attribute) =>
        double.TryParse(observation.GetAttribute(attribute), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    public static bool? Flag(this Observation observation, string attribute) =>
        bool.TryParse(observation.GetAttribute(attribute), out var value) ? value : null;

    public static string Invariant(double? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public static string Invariant(bool? value) => value?.ToString() ?? string.Empty;
}
