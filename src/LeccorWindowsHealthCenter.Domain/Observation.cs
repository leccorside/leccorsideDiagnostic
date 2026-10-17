namespace LeccorWindowsHealthCenter.Domain;

/// <summary>
/// Fato não-numérico coletado por um scanner (ex.: "grupo de eventos Disk/153 com 47 ocorrências",
/// "entrada de startup X aponta para executável inexistente"). O scanner apenas OBSERVA;
/// quem decide se é problema é o RuleEngine.
/// </summary>
public sealed record Observation
{
    /// <summary>Tipo da observação, ex.: "eventlog.group", "startup.entry".</summary>
    public required string Kind { get; init; }

    /// <summary>Identificador do objeto observado, ex.: "System/Disk/153" ou o nome da entrada.</summary>
    public required string Subject { get; init; }

    public required string ScannerId { get; init; }

    public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<Evidence> Evidence { get; init; } = [];

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public string? GetAttribute(string key) => Attributes.TryGetValue(key, out var value) ? value : null;
}
