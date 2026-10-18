using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Performance;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Processo cuja memória privada cresce de forma contínua e quase sem recuo ao longo do histórico — padrão de vazamento.
/// É sempre "possível": crescimento contínuo também acontece com caches legítimos.
/// </summary>
public sealed class PossibleMemoryLeakRule : IRule
{
    public string Id => "memory.possible-leak";

    public string Name => "Possível vazamento de memória";

    public IReadOnlyList<string> RequiredScanners { get; } = [MemoryScanner.ScannerId];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("MinimumSamples", 10, "amostras", "Com amostras a cada 60 s, são 10 min de observação; em janelas curtas crescer é comum (carregamento, cache)."),
        new("MinimumGrowthMB", 500, "MB", "Crescimentos menores são normais em navegadores e IDEs ao abrir conteúdo."),
        new("MonotonicFraction", 0.9, "fração", "Vazamento cresce quase sempre; uso normal oscila porque a memória é liberada periodicamente."),
        new("HighGrowthMB", 2048, "MB", "Acima de 2 GB de crescimento o impacto na memória do sistema é significativo."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var groups = context.Metrics
            .Where(m => m.Name == PerfNames.ProcessPrivate)
            .GroupBy(m => (Pid: m.Tags.GetValueOrDefault("pid"), Start: m.Tags.GetValueOrDefault("start")));

        foreach (var group in groups)
        {
            var samples = group.OrderBy(m => m.Timestamp).ToList();
            if (samples.Count < context.Parameter("MinimumSamples"))
            {
                continue;
            }

            var growth = samples[^1].Value - samples[0].Value;
            if (growth < context.Parameter("MinimumGrowthMB") * 1024 * 1024)
            {
                continue;
            }

            var steps = samples.Zip(samples.Skip(1), (a, b) => b.Value >= a.Value).ToList();
            var monotonic = steps.Count(up => up) / (double)steps.Count;
            if (monotonic < context.Parameter("MonotonicFraction"))
            {
                continue;
            }

            var name = samples[0].Tags.GetValueOrDefault("name") ?? "processo";
            var pid = group.Key.Pid ?? "?";
            var duration = samples[^1].Timestamp - samples[0].Timestamp;
            var perHour = duration.TotalHours > 0 ? growth / duration.TotalHours : 0;

            yield return new Finding
            {
                RuleId = Id,
                ScannerId = MemoryScanner.ScannerId,
                Category = FindingCategory.Memory,
                Title = $"Possível vazamento de memória em {name}",
                Description = $"A memória privada de {name} (PID {pid}) cresceu de {RuleText.Bytes(samples[0].Value)} para {RuleText.Bytes(samples[^1].Value)} em {RuleText.Duration(duration)}, quase sem nunca diminuir.",
                Severity = growth >= context.Parameter("HighGrowthMB") * 1024 * 1024 ? Severity.Medium : Severity.Low,
                Confidence = Confidence.FromPercent(60),
                Evidence =
                [
                    new Evidence
                    {
                        Source = EvidenceSource.Process,
                        Description = $"Crescimento de {RuleText.Bytes(growth)} ({RuleText.Bytes(perHour)}/h) em {samples.Count} amostras; cresceu em {RuleText.Percent(monotonic * 100)} dos intervalos",
                        Value = growth,
                        Unit = "bytes",
                        SampleCount = samples.Count,
                        Reference = $"Process.PrivateMemorySize64 · PID {pid}",
                    },
                ],
                ProbableCause = "Comportamento do próprio aplicativo: vazamento de memória ou cache que nunca é liberado.",
                Impact = "Se continuar, o processo consome a RAM disponível e causa lentidão no sistema ou falha do próprio aplicativo.",
                Recommendation = $"Reinicie {name} quando for conveniente e observe se o crescimento volta. Se for recorrente, atualize o aplicativo ou reporte ao fabricante.",
            };
        }
    }
}
