using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Performance;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Processador abaixo da frequência nominal enquanto está sob carga — indício de limitação por energia ou temperatura.
/// Só compara amostras com carga: em repouso, frequência baixa é economia de energia esperada.
/// </summary>
public sealed class CpuThrottlingRule : IRule
{
    public string Id => "cpu.throttling";

    public string Name => "Possível limitação de frequência da CPU";

    public IReadOnlyList<string> RequiredScanners { get; } = [CpuScanner.ScannerId];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("LoadThreshold", 50, "%", "Abaixo desse uso o Windows reduz a frequência de propósito para economizar energia; a comparação só vale sob carga."),
        new("PerformanceThreshold", 60, "%", "Sob carga, processadores atuais operam em 100% ou mais da frequência nominal (turbo); abaixo de 60% de forma consistente indica limitação."),
        new("MinimumLoadedSamples", 5, "amostras", "Poucas amostras sob carga não distinguem limitação de uma transição momentânea de estado."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var load = RuleText.ScanOnly(context, PerfNames.CpuTotal).Samples
            .GroupBy(s => s.Timestamp)
            .ToDictionary(g => g.Key, g => g.First().Value);
        var loaded = RuleText.ScanOnly(context, PerfNames.CpuPerformance).Samples
            .Where(s => load.TryGetValue(s.Timestamp, out var cpu) && cpu >= context.Parameter("LoadThreshold"))
            .ToList();

        if (loaded.Count < context.Parameter("MinimumLoadedSamples"))
        {
            yield break;
        }

        var meanPerformance = loaded.Average(s => s.Value);
        var threshold = context.Parameter("PerformanceThreshold");
        if (meanPerformance >= threshold)
        {
            yield break;
        }

        var baseFrequency = RuleText.ScanOnly(context, PerfNames.CpuBaseFrequency).Latest;
        var effective = RuleText.ScanOnly(context, PerfNames.CpuEffectiveFrequency);
        var evidence = new List<Evidence>
        {
            new()
            {
                Source = EvidenceSource.PerformanceCounter,
                Description = $"Desempenho médio do processador sob carga: {RuleText.Percent(meanPerformance)} da frequência nominal",
                Value = meanPerformance,
                Unit = "%",
                SampleCount = loaded.Count,
                Reference = @"\Processor Information(_Total)\% Processor Performance",
            },
        };

        if (baseFrequency is { } nominal && !effective.IsEmpty)
        {
            evidence.Add(new Evidence
            {
                Source = EvidenceSource.PerformanceCounter,
                Description = $"Frequência efetiva média {effective.Mean:0} MHz para nominal de {nominal:0} MHz",
                Value = effective.Mean,
                Unit = "MHz",
                Reference = @"\Processor Information(_Total)\Processor Frequency",
            });
        }

        yield return new Finding
        {
            RuleId = Id,
            ScannerId = CpuScanner.ScannerId,
            Category = FindingCategory.Cpu,
            Title = "Processador operando abaixo da frequência nominal sob carga",
            Description = $"Com a CPU acima de {RuleText.Percent(context.Parameter("LoadThreshold"))} de uso, o processador trabalhou em média a {RuleText.Percent(meanPerformance)} da frequência nominal.",
            Severity = Severity.Low,
            Confidence = Confidence.FromPercent(55),
            Evidence = evidence,
            ProbableCause = "Plano/modo de energia em economia, notebook na bateria ou limitação térmica/de potência. A causa exata não é mensurável sem sensores de temperatura.",
            Impact = "Tarefas pesadas (builds, compressão, jogos) levam mais tempo do que o hardware permite.",
            Recommendation = "Confira o modo de energia do Windows (Configurações › Sistema › Energia) e se o notebook está na tomada. Se persistir sob carga, verifique a ventilação e a temperatura do equipamento.",
        };
    }
}
