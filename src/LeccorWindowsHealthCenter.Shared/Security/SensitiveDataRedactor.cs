using System.Text.RegularExpressions;

namespace LeccorWindowsHealthCenter.Shared.Security;

/// <summary>
/// Remove segredos (senhas, tokens, API keys, cookies, credenciais) de textos antes que
/// eles cheguem a logs, relatórios ou ao banco. Aplicado de forma centralizada pelo logging
/// e pelo executor de comandos — nunca confiar que cada chamador lembrou de mascarar.
/// </summary>
public static partial class SensitiveDataRedactor
{
    public const string Mask = "***REDACTED***";

    private static readonly string[] SensitiveKeyFragments =
    [
        "password", "passwd", "pwd", "senha", "secret", "token", "apikey", "api_key", "api-key",
        "cookie", "credential", "authorization", "connectionstring", "privatekey", "private_key",
    ];

    /// <summary>Indica se o nome de uma propriedade/chave sugere conteúdo sensível.</summary>
    public static bool IsSensitiveKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var normalized = key.Replace(" ", string.Empty, StringComparison.Ordinal);
        foreach (var fragment in SensitiveKeyFragments)
        {
            if (normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Mascara pares chave=valor sensíveis, cabeçalhos Bearer e prefixos de tokens conhecidos.</summary>
    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        // Authorization primeiro e com o esquema preservado ("Bearer", "Basic"...): mascarar só o esquema exporia a credencial.
        var result = AuthorizationRegex().Replace(text, m => $"{m.Groups["key"].Value}{m.Groups["sep"].Value}{m.Groups["scheme"].Value}{Mask}");
        result = BearerRegex().Replace(result, $"Bearer {Mask}");
        result = KeyValueRegex().Replace(result, m => $"{m.Groups["key"].Value}{m.Groups["sep"].Value}{Mask}");
        result = KnownTokenRegex().Replace(result, Mask);
        return result;
    }

    [GeneratedRegex(
        """(?<key>["']?authorization["']?)(?<sep>\s*[:=]\s*["']?)(?<scheme>[A-Za-z]+\s+)?(?<value>[^\s"';,&]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationRegex();

    // chave=valor | chave: valor | "chave":"valor" | --chave valor
    [GeneratedRegex(
        """(?<key>["']?(?:password|passwd|pwd|senha|secret|token|api[_-]?key|cookie|credential|access[_-]?key|client[_-]?secret)["']?)(?<sep>\s*[:=]\s*["']?|\s+)(?<value>[^\s"';,&]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KeyValueRegex();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerRegex();

    // GitHub, Slack, AWS access key id, chaves estilo sk-...
    [GeneratedRegex(@"\b(?:gh[pousr]_[A-Za-z0-9]{20,}|xox[abprs]-[A-Za-z0-9-]{10,}|AKIA[0-9A-Z]{16}|sk-[A-Za-z0-9_-]{20,})\b", RegexOptions.CultureInvariant)]
    private static partial Regex KnownTokenRegex();
}
