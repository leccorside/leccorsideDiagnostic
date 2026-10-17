namespace LeccorWindowsHealthCenter.Domain;

/// <summary>
/// Score de saúde 0–100. <see cref="Overall"/> é null quando não há dados suficientes —
/// nunca exibir "100/100" só porque nada foi medido.
/// </summary>
public sealed record HealthScore
{
    public int? Overall { get; init; }

    public HealthRating Rating { get; init; } = HealthRating.Unknown;

    /// <summary>Score por categoria. Categorias sem cobertura (nenhum scanner executou) ficam ausentes.</summary>
    public IReadOnlyDictionary<ScoreCategory, int> Categories { get; init; } = new Dictionary<ScoreCategory, int>();

    public static HealthScore NoData { get; } = new();

    public static HealthRating RatingFor(int score) => score switch
    {
        >= 90 => HealthRating.Excellent,
        >= 75 => HealthRating.Good,
        >= 50 => HealthRating.Attention,
        _ => HealthRating.Critical,
    };
}
