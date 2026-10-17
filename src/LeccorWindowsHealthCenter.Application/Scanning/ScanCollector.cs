using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Application.Scanning;

/// <summary>Acumula métricas e observações produzidas por um scanner durante a coleta.</summary>
public sealed class ScanCollector
{
    private readonly List<Metric> _metrics = [];
    private readonly List<Observation> _observations = [];

    public ScanCollector(string scannerId)
    {
        ScannerId = scannerId;
    }

    public string ScannerId { get; }

    public IReadOnlyList<Metric> Metrics => _metrics;

    public IReadOnlyList<Observation> Observations => _observations;

    public void AddMetric(string name, double value, string unit, IReadOnlyDictionary<string, string>? tags = null, DateTimeOffset? timestamp = null)
    {
        _metrics.Add(new Metric
        {
            Name = name,
            Value = value,
            Unit = unit,
            ScannerId = ScannerId,
            Tags = tags ?? new Dictionary<string, string>(),
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
        });
    }

    public void AddObservation(string kind, string subject, IReadOnlyDictionary<string, string>? attributes = null, IReadOnlyList<Evidence>? evidence = null)
    {
        _observations.Add(new Observation
        {
            Kind = kind,
            Subject = subject,
            ScannerId = ScannerId,
            Attributes = attributes ?? new Dictionary<string, string>(),
            Evidence = evidence ?? [],
        });
    }
}
