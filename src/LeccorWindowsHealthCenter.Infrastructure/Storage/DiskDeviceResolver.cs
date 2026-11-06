using System.Management;
using System.Text.RegularExpressions;

namespace LeccorWindowsHealthCenter.Infrastructure.Storage;

public sealed record DiskMapping(int Number, IReadOnlyList<string> DriveLetters, string? Model);

/// <summary>
/// Resolve números de discos físicos (\Device\HarddiskN, Disk N, Disco N) para as letras de unidade correspondentes
/// (C:, D:, E:, etc.) e modelo do dispositivo, proporcionando clareza e contexto direto ao usuário nas regras e telas.
/// </summary>
public static class DiskDeviceResolver
{
    private static readonly Lock Gate = new();
    private static DateTimeOffset _cacheExpiresAt = DateTimeOffset.MinValue;
    private static Dictionary<int, DiskMapping> _cachedMappings = [];

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Extrai o índice numérico do disco físico a partir de nomes de dispositivos do Windows.
    /// Ex.: "\\Device\\Harddisk3\\DR3" -> 3, "Disco 0" -> 0, "Disk 2" -> 2.
    /// </summary>
    public static int? ExtractDiskNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = Regex.Match(text, @"(?:\\Device\\Harddisk|Disk\s*|Disco\s*)(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var num))
        {
            return num;
        }

        return null;
    }

    /// <summary>
    /// Resolve uma identificação de recurso de disco bruta para uma descrição visual com a letra da unidade.
    /// Ex.: "Disco 3" -> "Disco 3 [F:]", "\Device\Harddisk0\DR0" -> "Disco 0 [C:]".
    /// </summary>
    public static string ResolveDisplay(string? rawTarget)
    {
        if (string.IsNullOrWhiteSpace(rawTarget))
        {
            return "Disco";
        }

        // Se já foi resolvido e possui letras entre colchetes, preserva
        if (rawTarget.Contains('[') && rawTarget.Contains(']'))
        {
            return rawTarget;
        }

        var diskNumber = ExtractDiskNumber(rawTarget);
        if (diskNumber is null)
        {
            return rawTarget;
        }

        var mappings = GetDiskMappings();
        if (mappings.TryGetValue(diskNumber.Value, out var mapping))
        {
            if (mapping.DriveLetters.Count > 0)
            {
                var letters = string.Join(", ", mapping.DriveLetters);
                return $"Disco {diskNumber.Value} [{letters}]";
            }

            if (!string.IsNullOrWhiteSpace(mapping.Model))
            {
                return $"Disco {diskNumber.Value} [{mapping.Model}]";
            }

            return $"Disco {diskNumber.Value} [sem letra atribuída]";
        }

        return $"Disco {diskNumber.Value}";
    }

    /// <summary>
    /// Formata uma descrição amigável a partir do número do disco, lista de letras de unidade e modelo.
    /// </summary>
    public static string FormatDisplay(int diskNumber, IReadOnlyList<string>? driveLetters, string? model = null)
    {
        if (driveLetters is { Count: > 0 })
        {
            var letters = string.Join(", ", driveLetters);
            return $"Disco {diskNumber} [{letters}]";
        }

        if (!string.IsNullOrWhiteSpace(model))
        {
            return $"Disco {diskNumber} [{model}]";
        }

        return $"Disco {diskNumber}";
    }

    /// <summary>
    /// Obtém o catálogo de mapeamento atual dos discos físicos para as partições com letra montada no Windows.
    /// Utiliza cache em memória de 30 segundos para evitar consultas repetidas ao WMI.
    /// </summary>
    public static IReadOnlyDictionary<int, DiskMapping> GetDiskMappings()
    {
        lock (Gate)
        {
            var now = DateTimeOffset.UtcNow;
            if (now < _cacheExpiresAt && _cachedMappings.Count > 0)
            {
                return _cachedMappings;
            }

            try
            {
                _cachedMappings = LoadMappingsFromWmi();
                _cacheExpiresAt = now + CacheDuration;
            }
            catch
            {
                // Em caso de falha de acesso WMI ou em testes em plataformas não-Windows, mantém cache anterior ou vazio
                if (_cachedMappings.Count == 0)
                {
                    _cacheExpiresAt = now + TimeSpan.FromSeconds(5);
                }
            }

            return _cachedMappings;
        }
    }

    /// <summary>
    /// Define manualmente os mapeamentos de disco em cache (útil para testes unitários).
    /// </summary>
    public static void SetMappingsForTesting(Dictionary<int, DiskMapping> mappings)
    {
        lock (Gate)
        {
            _cachedMappings = new Dictionary<int, DiskMapping>(mappings);
            _cacheExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
        }
    }

    /// <summary>
    /// Limpa o cache para forçar recarga na próxima consulta.
    /// </summary>
    public static void ClearCache()
    {
        lock (Gate)
        {
            _cachedMappings.Clear();
            _cacheExpiresAt = DateTimeOffset.MinValue;
        }
    }

    private static Dictionary<int, DiskMapping> LoadMappingsFromWmi()
    {
        var result = new Dictionary<int, DiskMapping>();

        if (!OperatingSystem.IsWindows())
        {
            return result;
        }

        var lettersByDisk = new Dictionary<int, List<string>>();

        // 1. Partições com letras atribuídas via namespace root/Microsoft/Windows/Storage
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage",
                "SELECT DiskNumber, DriveLetter FROM MSFT_Partition WHERE DriveLetter IS NOT NULL");

            foreach (var obj in searcher.Get())
            {
                using (obj)
                {
                    if (obj["DiskNumber"] is int diskNum || (obj["DiskNumber"] != null && int.TryParse(obj["DiskNumber"].ToString(), out diskNum)))
                    {
                        var letterChar = obj["DriveLetter"]?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(letterChar) && letterChar != "0" && letterChar != "\0")
                        {
                            var letterFormatted = $"{letterChar.ToUpperInvariant()}:";
                            if (!lettersByDisk.TryGetValue(diskNum, out var list))
                            {
                                list = [];
                                lettersByDisk[diskNum] = list;
                            }

                            if (!list.Contains(letterFormatted))
                            {
                                list.Add(letterFormatted);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback caso MSFT_Partition não esteja disponível: tenta Win32_LogicalDiskToPartition
            LoadMappingsFallback(lettersByDisk);
        }

        // 2. Modelos dos discos físicos
        var modelsByDisk = new Dictionary<int, string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage",
                "SELECT DeviceId, FriendlyName, Model FROM MSFT_PhysicalDisk");

            foreach (var obj in searcher.Get())
            {
                using (obj)
                {
                    if (int.TryParse(obj["DeviceId"]?.ToString(), out var diskNum))
                    {
                        var name = obj["FriendlyName"]?.ToString() ?? obj["Model"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            modelsByDisk[diskNum] = name.Trim();
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignora se não conseguir obter nomes detalhados
        }

        // Consolida todos os discos encontrados
        var allDisks = lettersByDisk.Keys.Union(modelsByDisk.Keys).Distinct();
        foreach (var diskNum in allDisks)
        {
            var letters = lettersByDisk.GetValueOrDefault(diskNum) ?? [];
            letters.Sort(StringComparer.OrdinalIgnoreCase);
            var model = modelsByDisk.GetValueOrDefault(diskNum);

            result[diskNum] = new DiskMapping(diskNum, letters, model);
        }

        return result;
    }

    private static void LoadMappingsFallback(Dictionary<int, List<string>> lettersByDisk)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Antecedent, Dependent FROM Win32_LogicalDiskToPartition");

            foreach (var obj in searcher.Get())
            {
                using (obj)
                {
                    var antecedent = obj["Antecedent"]?.ToString() ?? string.Empty;
                    var dependent = obj["Dependent"]?.ToString() ?? string.Empty;

                    var diskMatch = Regex.Match(antecedent, @"Disk\s*#(\d+)", RegexOptions.IgnoreCase);
                    var letterMatch = Regex.Match(dependent, @"DeviceID=""([A-Z]:)""", RegexOptions.IgnoreCase);

                    if (diskMatch.Success && letterMatch.Success &&
                        int.TryParse(diskMatch.Groups[1].Value, out var diskNum))
                    {
                        var letter = letterMatch.Groups[1].Value.ToUpperInvariant();
                        if (!lettersByDisk.TryGetValue(diskNum, out var list))
                        {
                            list = [];
                            lettersByDisk[diskNum] = list;
                        }

                        if (!list.Contains(letter))
                        {
                            list.Add(letter);
                        }
                    }
                }
            }
        }
        catch
        {
            // Sem acesso ao fallback
        }
    }
}

