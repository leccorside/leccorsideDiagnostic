using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Performance;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>RAM física quase toda em uso de forma persistente; paginação intensa eleva a severidade.</summary>
public sealed class MemoryPressureRule : IRule
{
    public string Id => "memory.pressure";

    public string Name => "Pouca memória RAM disponível";

    public IReadOnlyList<string> RequiredScanners { get; } = [MemoryScanner.ScannerId];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("UsedThreshold", 90, "%", "Com menos de 10% de RAM livre o Windows passa a retirar páginas de processos ativos e a recorrer ao disco."),
        new("MinimumFraction", 0.8, "fração", "Exigir 80% das amostras acima do limite evita alertar por um pico momentâneo (abrir um aplicativo pesado)."),
        new("MinimumSamples", 10, "amostras", "Menos amostras não caracterizam um estado persistente."),
        new("PagingThreshold", 100, "páginas/s", "Leituras de página do disco sustentadas acima de ~100/s indicam que o conjunto de trabalho não cabe na RAM e o sistema ficou limitado pelo disco."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var used = context.Series(PerfNames.MemoryUsed);
        var threshold = context.Parameter("UsedThreshold");
        if (used.Count < context.Parameter("MinimumSamples"))
        {
            yield break;
        }

        var fraction = used.FractionAtOrAbove(threshold);
        if (fraction < context.Parameter("MinimumFraction"))
        {
            yield break;
        }

        var paging = context.Series(PerfNames.MemoryPagesInput);
        var heavyPaging = !paging.IsEmpty && paging.Mean >= context.Parameter("PagingThreshold");

        var evidence = new List<Evidence>
        {
            new()
            {
                Source = EvidenceSource.WindowsApi,
                Description = $"RAM em uso ≥ {RuleText.Percent(threshold)} em {RuleText.Percent(fraction * 100)} das amostras (média {RuleText.Percent(used.Mean)})",
                Value = used.Mean,
                Unit = "%",
                SampleCount = used.Count,
                Reference = "GlobalMemoryStatusEx",
            },
            RuleText.Window(used, "Observação"),
        };

        if (!paging.IsEmpty)
        {
            evidence.Add(new Evidence
            {
                Source = EvidenceSource.PerformanceCounter,
                Description = $"Leituras de página do disco (hard faults): média de {paging.Mean:0} páginas/s durante a varredura",
                Value = paging.Mean,
                Unit = "páginas/s",
                SampleCount = paging.Count,
                Reference = @"\Memory\Pages Input/sec",
            });
        }

        evidence.AddRange(RuleText.TopProcesses(context, PerfNames.ObservationProcessMemory, "privateBytes", RuleText.Bytes, "memória privada"));

        var top = RuleText.TopProcessName(context, PerfNames.ObservationProcessMemory, "privateBytes");
        yield return new Finding
        {
            RuleId = Id,
            ScannerId = MemoryScanner.ScannerId,
            Category = FindingCategory.Memory,
            Title = heavyPaging ? "Memória RAM esgotada com paginação intensa em disco" : "Pouca memória RAM disponível",
            Description = $"A memória física ficou acima de {RuleText.Percent(threshold)} de uso na maior parte do tempo observado.",
            Severity = heavyPaging ? Severity.High : Severity.Medium,
            Confidence = Confidence.FromPercent(90),
            Evidence = evidence,
            ProbableCause = top is null ? null : $"Maior consumidor de memória privada: {top}.",
            Impact = heavyPaging
                ? "O sistema está lendo memória do disco continuamente: tudo fica lento, principalmente trocar de aplicativo."
                : "Pouca margem para abrir novos aplicativos; o Windows começa a usar o disco como memória.",
            Recommendation = "Feche aplicativos e abas que não estão em uso, principalmente os maiores consumidores listados. Se acontecer com frequência no seu uso normal, considere aumentar a memória RAM.",
        };
    }
}
