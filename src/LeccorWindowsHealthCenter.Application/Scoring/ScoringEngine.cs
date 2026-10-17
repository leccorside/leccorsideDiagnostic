using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Application.Scoring;

public interface IScoringEngine
{
    HealthScore Calculate(IReadOnlyList<DiagnosticResult> results);
}

/// <summary>
/// Scoring Engine (seção 46). Princípios:
/// <list type="bullet">
/// <item>só categorias efetivamente medidas entram no score — sem dados, sem nota;</item>
/// <item>findings Info (recomendações) nunca reduzem o score;</item>
/// <item>a dedução é proporcional à severidade e à confiança — "possível problema" pesa menos que "confirmado";</item>
/// <item>ocorrências múltiplas do mesmo problema (mesma regra) têm retorno decrescente — 50 eventos iguais não são 50 problemas.</item>
/// </list>
/// </summary>
public sealed class ScoringEngine : IScoringEngine
{
    /// <summary>Peso de dedução por severidade (pontos, em uma categoria de 100).</summary>
    public static double SeverityWeight(Severity severity) => severity switch
    {
        Severity.Critical => 45,
        Severity.High => 22,
        Severity.Medium => 9,
        Severity.Low => 3,
        _ => 0,
    };

    /// <summary>Peso relativo de cada categoria no score geral.</summary>
    public static double CategoryWeight(ScoreCategory category) => category switch
    {
        ScoreCategory.Hardware => 1.3,
        ScoreCategory.Stability => 1.2,
        ScoreCategory.Storage => 1.0,
        ScoreCategory.Windows => 1.0,
        ScoreCategory.Security => 1.0,
        ScoreCategory.Performance => 0.9,
        _ => 1.0,
    };

    public HealthScore Calculate(IReadOnlyList<DiagnosticResult> results)
    {
        var covered = results
            .Where(r => r.Status == ScannerRunStatus.Completed)
            .SelectMany(r => CategoryMapping.CoveredBy(r.Module))
            .ToHashSet();

        if (covered.Count == 0)
        {
            return HealthScore.NoData;
        }

        var findings = results.SelectMany(r => r.Findings).Where(f => !f.IsRecommendation).ToList();
        var categoryScores = new Dictionary<ScoreCategory, int>();

        foreach (var category in covered)
        {
            var deduction = findings
                .Where(f => CategoryMapping.ToScoreCategory(f.Category) == category)
                .GroupBy(f => f.RuleId)
                .Sum(group => RuleDeduction(group));

            categoryScores[category] = (int)Math.Round(Math.Clamp(100 - deduction, 0, 100));
        }

        var totalWeight = categoryScores.Keys.Sum(CategoryWeight);
        var overall = (int)Math.Round(categoryScores.Sum(kv => kv.Value * CategoryWeight(kv.Key)) / totalWeight);

        return new HealthScore
        {
            Overall = overall,
            Rating = HealthScore.RatingFor(overall),
            Categories = categoryScores,
        };
    }

    /// <summary>
    /// Dedução de um grupo de findings da mesma regra: o mais grave conta inteiro, os seguintes
    /// com peso decrescente (1/2, 1/3, ...), até no máximo 2x o peso do mais grave.
    /// </summary>
    private static double RuleDeduction(IEnumerable<Finding> sameRule)
    {
        var weights = sameRule
            .Select(f => SeverityWeight(f.Severity) * (f.Confidence.Percent / 100.0))
            .OrderDescending()
            .ToList();

        if (weights.Count == 0)
        {
            return 0;
        }

        var total = weights.Select((w, i) => w / (i + 1)).Sum();
        return Math.Min(total, weights[0] * 2);
    }
}
