using System.Globalization;
using System.Text.RegularExpressions;
using LeccorWindowsHealthCenter.Application.SystemInfo;

namespace LeccorWindowsHealthCenter.Monitoring.Pdh;

/// <summary>
/// Agrega os contadores "GPU Engine" (uma instância por processo × motor) no uso por adaptador, do mesmo jeito
/// que o Gerenciador de Tarefas: soma os processos de cada tipo de motor e usa o tipo de motor mais ocupado.
/// Exemplo de instância: <c>pid_1234_luid_0x00000000_0x0000D1E8_phys_0_eng_0_engtype_3D</c>.
/// </summary>
public static partial class GpuCounterParser
{
    public static IReadOnlyList<GpuAdapterUsage> Aggregate(
        IEnumerable<KeyValuePair<string, double>> engineUtilization,
        IEnumerable<KeyValuePair<string, double>>? dedicatedUsage = null,
        IEnumerable<KeyValuePair<string, double>>? sharedUsage = null)
    {
        var perAdapter = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (instance, value) in engineUtilization)
        {
            var match = EngineInstance().Match(instance);
            if (!match.Success)
            {
                continue;
            }

            var luid = match.Groups["luid"].Value.ToLower(CultureInfo.InvariantCulture);
            var engine = match.Groups["engtype"].Value;
            if (!perAdapter.TryGetValue(luid, out var engines))
            {
                perAdapter[luid] = engines = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            }

            engines[engine] = engines.GetValueOrDefault(engine) + value;
        }

        var dedicated = ByLuid(dedicatedUsage);
        var shared = ByLuid(sharedUsage);
        foreach (var luid in dedicated.Keys.Concat(shared.Keys))
        {
            perAdapter.TryAdd(luid, new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase));
        }

        return perAdapter
            .Select(kv => new GpuAdapterUsage
            {
                AdapterLuid = kv.Key,
                EngineUtilization = kv.Value.ToDictionary(e => e.Key, e => Math.Clamp(e.Value, 0, 100)),
                UtilizationPercent = kv.Value.Count == 0 ? 0 : Math.Clamp(kv.Value.Values.Max(), 0, 100),
                DedicatedUsageBytes = dedicated.TryGetValue(kv.Key, out var d) ? (long)d : null,
                SharedUsageBytes = shared.TryGetValue(kv.Key, out var s) ? (long)s : null,
            })
            .OrderBy(a => a.AdapterLuid, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Extrai o LUID de instâncias de "GPU Adapter Memory": <c>luid_0x00000000_0x0000D1E8_phys_0</c>.</summary>
    public static string? LuidOf(string instance)
    {
        var match = LuidPattern().Match(instance);
        return match.Success ? match.Groups["luid"].Value.ToLower(CultureInfo.InvariantCulture) : null;
    }

    private static Dictionary<string, double> ByLuid(IEnumerable<KeyValuePair<string, double>>? values)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (instance, value) in values ?? [])
        {
            var luid = LuidOf(instance);
            if (luid is not null)
            {
                result[luid] = result.GetValueOrDefault(luid) + value;
            }
        }

        return result;
    }

    [GeneratedRegex(@"luid_(?<luid>0x[0-9a-f]+_0x[0-9a-f]+).*engtype_(?<engtype>[A-Za-z0-9_ ]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EngineInstance();

    [GeneratedRegex(@"luid_(?<luid>0x[0-9a-f]+_0x[0-9a-f]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LuidPattern();
}
