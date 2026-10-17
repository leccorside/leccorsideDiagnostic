using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Application.Rules;

/// <summary>Dados disponíveis para uma regra durante a avaliação.</summary>
public sealed class RuleContext
{
    private readonly IRule _rule;
    private readonly IRuleSettings _settings;

    public RuleContext(IRule rule, IReadOnlyList<Metric> metrics, IReadOnlyList<Observation> observations, IRuleSettings settings, TimeProvider timeProvider)
    {
        _rule = rule;
        _settings = settings;
        Metrics = metrics;
        Observations = observations;
        TimeProvider = timeProvider;
    }

    public IReadOnlyList<Metric> Metrics { get; }

    public IReadOnlyList<Observation> Observations { get; }

    public TimeProvider TimeProvider { get; }

    /// <summary>Valor configurado do parâmetro (ou padrão da regra).</summary>
    public double Parameter(string key)
    {
        var definition = _rule.Parameters.FirstOrDefault(p => p.Key == key)
            ?? throw new InvalidOperationException($"A regra '{_rule.Id}' não declara o parâmetro '{key}'.");
        return _settings.GetParameter(_rule.Id, key) ?? definition.DefaultValue;
    }

    /// <summary>Série temporal de uma métrica, opcionalmente filtrada por tag.</summary>
    public MetricSeries Series(string metricName, string? tagKey = null, string? tagValue = null)
    {
        var samples = Metrics.Where(m => m.Name == metricName);
        if (tagKey is not null && tagValue is not null)
        {
            samples = samples.Where(m => m.HasTag(tagKey, tagValue));
        }

        return new MetricSeries(metricName, samples.OrderBy(m => m.Timestamp).ToList());
    }

    public IEnumerable<Observation> ObservationsOf(string kind) => Observations.Where(o => o.Kind == kind);
}
