using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Application.Rules;

/// <summary>
/// Série de amostras de uma métrica com as estatísticas usadas na detecção de falsos positivos
/// (seção 49): quantidade de amostras, duração, recorrência e sustentação acima de um limite.
/// "CPU 100% por 2 segundos" não é problema; "CPU &gt; 90% sustentada e recorrente" é.
/// </summary>
public sealed class MetricSeries
{
    public MetricSeries(string name, IReadOnlyList<Metric> samples)
    {
        Name = name;
        Samples = samples;
    }

    public string Name { get; }

    public IReadOnlyList<Metric> Samples { get; }

    public int Count => Samples.Count;

    public bool IsEmpty => Samples.Count == 0;

    public double Mean => IsEmpty ? 0 : Samples.Average(s => s.Value);

    public double Max => IsEmpty ? 0 : Samples.Max(s => s.Value);

    public double Min => IsEmpty ? 0 : Samples.Min(s => s.Value);

    public double? Latest => IsEmpty ? null : Samples[^1].Value;

    public TimeSpan Duration => Count < 2 ? TimeSpan.Zero : Samples[^1].Timestamp - Samples[0].Timestamp;

    public double Percentile(double percentile)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(percentile, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percentile, 100);
        if (IsEmpty)
        {
            return 0;
        }

        var sorted = Samples.Select(s => s.Value).Order().ToArray();
        var rank = percentile / 100 * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        return sorted[lower] + ((sorted[upper] - sorted[lower]) * (rank - lower));
    }

    /// <summary>Fração (0–1) das amostras com valor ≥ threshold.</summary>
    public double FractionAtOrAbove(double threshold) =>
        IsEmpty ? 0 : Samples.Count(s => s.Value >= threshold) / (double)Count;

    /// <summary>
    /// Maior período contínuo (entre timestamps) em que todas as amostras ficaram ≥ threshold.
    /// <paramref name="maxGap"/> interrompe a sequência quando duas amostras estão mais distantes que isso
    /// (ex.: o computador suspendeu): sem medição no intervalo, não há como afirmar que a carga continuou.
    /// </summary>
    public TimeSpan LongestRunAtOrAbove(double threshold, TimeSpan? maxGap = null)
    {
        var longest = TimeSpan.Zero;
        DateTimeOffset? runStart = null;
        DateTimeOffset? previous = null;
        foreach (var sample in Samples)
        {
            if (maxGap is { } gap && previous is { } last && sample.Timestamp - last > gap)
            {
                runStart = null;
            }

            previous = sample.Timestamp;
            if (sample.Value >= threshold)
            {
                runStart ??= sample.Timestamp;
                var current = sample.Timestamp - runStart.Value;
                if (current > longest)
                {
                    longest = current;
                }
            }
            else
            {
                runStart = null;
            }
        }

        return longest;
    }

    /// <summary>Quantas vezes a série cruzou para cima do threshold (episódios distintos).</summary>
    public int EpisodesAtOrAbove(double threshold)
    {
        var episodes = 0;
        var above = false;
        foreach (var sample in Samples)
        {
            var isAbove = sample.Value >= threshold;
            if (isAbove && !above)
            {
                episodes++;
            }

            above = isAbove;
        }

        return episodes;
    }
}
