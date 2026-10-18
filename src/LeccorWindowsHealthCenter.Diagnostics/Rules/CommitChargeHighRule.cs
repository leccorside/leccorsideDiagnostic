using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Performance;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>Memória comprometida (commit) perto do limite: alocações começam a falhar.</summary>
public sealed class CommitChargeHighRule : IRule
{
    public string Id => "memory.commit-high";

    public string Name => "Memória virtual quase esgotada";

    public IReadOnlyList<string> RequiredScanners { get; } = [MemoryScanner.ScannerId];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("Threshold", 90, "%", "O commit é limitado por RAM + arquivo de paginação; perto do limite, alocações falham e aplicativos podem fechar com erro de falta de memória."),
        new("MinimumFraction", 0.8, "fração", "80% das amostras acima do limite caracteriza um estado, não um pico."),
        new("MinimumSamples", 10, "amostras", "Menos amostras não caracterizam um estado persistente."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var commit = context.Series(PerfNames.MemoryCommit);
        var threshold = context.Parameter("Threshold");
        if (commit.Count < context.Parameter("MinimumSamples") || commit.FractionAtOrAbove(threshold) < context.Parameter("MinimumFraction"))
        {
            yield break;
        }

        var evidence = new List<Evidence>
        {
            new()
            {
                Source = EvidenceSource.WindowsApi,
                Description = $"Memória comprometida ≥ {RuleText.Percent(threshold)} do limite em {RuleText.Percent(commit.FractionAtOrAbove(threshold) * 100)} das amostras (média {RuleText.Percent(commit.Mean)})",
                Value = commit.Mean,
                Unit = "%",
                SampleCount = commit.Count,
                Reference = "GetPerformanceInfo (CommitTotal / CommitLimit)",
            },
            RuleText.Window(commit, "Observação"),
        };

        var total = RuleText.ScanOnly(context, PerfNames.MemoryCommitTotal).Latest;
        var limit = RuleText.ScanOnly(context, PerfNames.MemoryCommitLimit).Latest;
        if (total is { } t && limit is { } l)
        {
            evidence.Add(new Evidence
            {
                Source = EvidenceSource.WindowsApi,
                Description = $"Comprometido: {RuleText.Bytes(t)} de {RuleText.Bytes(l)} (RAM + arquivo de paginação)",
                Value = t,
                Unit = "bytes",
                Reference = "GetPerformanceInfo",
            });
        }

        evidence.AddRange(RuleText.TopProcesses(context, PerfNames.ObservationProcessMemory, "privateBytes", RuleText.Bytes, "memória privada"));

        yield return new Finding
        {
            RuleId = Id,
            ScannerId = MemoryScanner.ScannerId,
            Category = FindingCategory.Memory,
            Title = "Memória virtual quase esgotada",
            Description = $"A memória comprometida pelo sistema ficou acima de {RuleText.Percent(threshold)} do limite (RAM + arquivo de paginação).",
            Severity = Severity.High,
            Confidence = Confidence.FromPercent(90),
            Evidence = evidence,
            ProbableCause = "Aplicativos reservaram quase toda a memória que o Windows consegue garantir; ver os maiores consumidores de memória privada.",
            Impact = "Novas alocações podem falhar: aplicativos fecham com erro de memória insuficiente e o Windows pode exibir o aviso de memória virtual baixa.",
            Recommendation = "Feche os aplicativos com maior memória privada. Confirme que o arquivo de paginação está gerenciado pelo sistema (não desativado ou fixo pequeno). Se recorrente, considere mais RAM.",
        };
    }
}
