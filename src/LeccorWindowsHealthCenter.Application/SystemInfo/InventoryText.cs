using System.Text.RegularExpressions;

namespace LeccorWindowsHealthCenter.Application.SystemInfo;

/// <summary>Severidade visual de um status de dispositivo.</summary>
public enum StatusTone
{
    Normal,
    Warning,
    Critical,
}

/// <summary>
/// Interpretação de textos que o firmware/WMI devolve "crus". Puro e testável; usado na exibição — o snapshot
/// salvo no banco guarda o valor original como evidência.
/// </summary>
public static partial class InventoryText
{
    /// <summary>
    /// Textos de preenchimento que placas genéricas gravam no SMBIOS. Exibi-los como dado real afirmaria algo
    /// que ninguém mediu.
    /// </summary>
    private static readonly HashSet<string> Placeholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "To Be Filled By O.E.M.", "To be filled by O.E.M.", "System manufacturer", "System Product Name",
        "System Version", "Default string", "O.E.M.", "OEM", "Not Applicable", "Not Specified", "None",
        "Base Board Manufacturer", "Base Board Product Name", "Unknown", "Undefined", "INVALID", "Type2 - Board Vendor Name1",
    };

    /// <summary>Códigos JEDEC (banco + fabricante) que o Win32_PhysicalMemory devolve no lugar do nome.</summary>
    private static readonly (string[] Codes, string Name)[] JedecVendors =
    [
        (["00AD", "80AD", "AD00"], "SK hynix"),
        (["00CE", "80CE", "CE00"], "Samsung"),
        (["002C", "802C", "2C00"], "Micron"),
        (["0198", "9801", "8198"], "Kingston"),
        (["859B", "9B85", "059B"], "Crucial"),
        (["04CD", "CD04"], "G.Skill"),
        (["029E", "9E02"], "Corsair"),
        (["04CB", "CB04"], "A-DATA"),
        (["0215", "1502"], "Transcend"),
        (["06C1", "C106"], "Asint"),
        (["8551", "5185"], "Qimonda"),
        (["80AE", "AE80"], "Nanya"),
    ];

    public static bool IsPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) || Placeholders.Contains(value.Trim());

    /// <summary>Texto limpo: espaços aparados; placeholders e vazios viram null.</summary>
    public static string? Clean(string? value) => IsPlaceholder(value) ? null : value!.Trim();

    /// <summary>
    /// Nome do fabricante de memória. Código JEDEC conhecido vira o nome; código desconhecido é sinalizado
    /// como tal; nomes legíveis passam inalterados.
    /// </summary>
    public static string? MemoryVendor(string? raw)
    {
        var value = Clean(raw);
        if (value is null || !HexCode().IsMatch(value))
        {
            return value;
        }

        var upper = value.ToUpperInvariant();
        foreach (var (codes, name) in JedecVendors)
        {
            if (codes.Any(c => upper.StartsWith(c, StringComparison.Ordinal)))
            {
                return name;
            }
        }

        return $"não identificado (código JEDEC {upper})";
    }

    /// <summary>Status de dispositivo do WMI (CIM_ManagedSystemElement.Status) em pt-BR, com a severidade visual.</summary>
    public static (string Text, StatusTone Tone)? DeviceStatus(string? raw) => Clean(raw)?.ToUpperInvariant() switch
    {
        null => null,
        "OK" => ("Funcionando", StatusTone.Normal),
        "DEGRADED" => ("Degradado", StatusTone.Warning),
        "STRESSED" => ("Sobrecarregado", StatusTone.Warning),
        "PRED FAIL" => ("Falha prevista", StatusTone.Critical),
        "ERROR" => ("Com erro", StatusTone.Critical),
        "NONRECOVER" => ("Erro irrecuperável", StatusTone.Critical),
        "NO CONTACT" or "LOST COMM" => ("Sem comunicação", StatusTone.Critical),
        "STARTING" => ("Iniciando", StatusTone.Normal),
        "STOPPING" => ("Parando", StatusTone.Normal),
        "SERVICE" => ("Em manutenção", StatusTone.Warning),
        "UNKNOWN" => ("Desconhecido", StatusTone.Normal),
        _ => (raw!.Trim(), StatusTone.Normal),
    };

    [GeneratedRegex("^[0-9A-Fa-f]{4,16}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexCode();
}
