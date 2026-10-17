namespace LeccorWindowsHealthCenter.Domain;

/// <summary>
/// Amostra numérica coletada por um scanner. Várias amostras com o mesmo <see cref="Name"/>
/// (e mesmas tags) formam uma série temporal usada pelo RuleEngine para evitar falsos
/// positivos (duração, recorrência, quantidade de amostras).
/// </summary>
public sealed record Metric
{
    public required string Name { get; init; }

    public required double Value { get; init; }

    public required string Unit { get; init; }

    public required string ScannerId { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Dimensões opcionais, ex.: { "drive": "C:" } ou { "process": "chrome.exe" }.</summary>
    public IReadOnlyDictionary<string, string> Tags { get; init; } = EmptyTags;

    private static readonly IReadOnlyDictionary<string, string> EmptyTags = new Dictionary<string, string>();

    public bool HasTag(string key, string value) =>
        Tags.TryGetValue(key, out var v) && string.Equals(v, value, StringComparison.OrdinalIgnoreCase);
}
