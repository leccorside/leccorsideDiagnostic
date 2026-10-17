namespace LeccorWindowsHealthCenter.Domain;

/// <summary>
/// Prova técnica que sustenta um finding (seção 48 do PROMPT). Todo finding precisa
/// apontar para pelo menos uma evidência — "como sabemos disso?".
/// </summary>
public sealed record Evidence
{
    public required EvidenceSource Source { get; init; }

    /// <summary>Descrição legível, ex.: "Startup médio: 11.8s".</summary>
    public required string Description { get; init; }

    /// <summary>Valor numérico medido, quando aplicável.</summary>
    public double? Value { get; init; }

    public string? Unit { get; init; }

    /// <summary>Referência técnica (Event ID, caminho de registro, comando, nome do contador...). Exibida no modo avançado.</summary>
    public string? Reference { get; init; }

    /// <summary>Quantidade de amostras/ocorrências que suportam esta evidência.</summary>
    public int SampleCount { get; init; } = 1;

    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.UtcNow;
}
