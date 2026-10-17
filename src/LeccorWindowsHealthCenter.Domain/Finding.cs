namespace LeccorWindowsHealthCenter.Domain;

/// <summary>
/// Problema ou recomendação produzido pelo RuleEngine a partir de evidências.
/// Responde às perguntas da seção 72: o que aconteceu, como sabemos, qual impacto,
/// como corrigir, qual o risco da correção e se é possível desfazer.
/// </summary>
public sealed record Finding
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Regra que gerou o finding (rastreabilidade e configuração de thresholds).</summary>
    public required string RuleId { get; init; }

    public required string ScannerId { get; init; }

    public required FindingCategory Category { get; init; }

    public required string Title { get; init; }

    /// <summary>O que aconteceu?</summary>
    public required string Description { get; init; }

    public required Severity Severity { get; init; }

    public required Confidence Confidence { get; init; }

    /// <summary>Como sabemos disso?</summary>
    public required IReadOnlyList<Evidence> Evidence { get; init; }

    public string? ProbableCause { get; init; }

    /// <summary>Qual impacto?</summary>
    public required string Impact { get; init; }

    /// <summary>Como corrigir?</summary>
    public required string Recommendation { get; init; }

    /// <summary>Ação de reparo associada, se existir (null = apenas recomendação manual).</summary>
    public string? RepairActionId { get; init; }

    /// <summary>Qual o risco da correção? Null quando não há correção automática disponível.</summary>
    public RiskLevel? RepairRisk { get; init; }

    public bool RequiresElevation { get; init; }

    public bool RequiresRestart { get; init; }

    /// <summary>Posso desfazer?</summary>
    public bool SupportsRollback { get; init; }

    public FindingStatus Status { get; init; } = FindingStatus.Open;

    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool HasRepair => RepairActionId is not null;

    /// <summary>Recomendação/informação — não conta como "problema" na UI nem no score.</summary>
    public bool IsRecommendation => Severity == Severity.Info;
}
