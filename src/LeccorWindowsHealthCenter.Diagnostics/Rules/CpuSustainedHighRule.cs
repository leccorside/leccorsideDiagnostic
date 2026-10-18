using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Performance;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>CPU acima do limite por período contínuo (seção 49: duração e contexto, nunca "CPU alta" isolada).</summary>
public sealed class CpuSustainedHighRule : IRule
{
    public string Id => "cpu.sustained-high";

    public string Name => "CPU alta sustentada";

    public IReadOnlyList<string> RequiredScanners { get; } = [CpuScanner.ScannerId];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("Threshold", 90, "%", "Acima de 90% sobra menos de 10% da capacidade para tarefas interativas; picos são normais, o problema é a sustentação."),
        new("MinimumSustainedSeconds", 300, "s", "Compilações, instalações e abertura de aplicativos saturam a CPU por segundos ou poucos minutos; 5 min contínuos indicam carga persistente."),
        new("HighSeveritySeconds", 900, "s", "15 min contínuos acima do limite afetam qualquer uso interativo da máquina."),
        new("MinimumSamples", 30, "amostras", "Menos amostras não descrevem o comportamento ao longo do tempo."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var series = context.Series(PerfNames.CpuTotal);
        var threshold = context.Parameter("Threshold");
        if (series.Count < context.Parameter("MinimumSamples"))
        {
            yield break;
        }

        var run = series.LongestRunAtOrAbove(threshold, RuleText.MaxSampleGap);
        if (run.TotalSeconds < context.Parameter("MinimumSustainedSeconds"))
        {
            yield break;
        }

        var evidence = new List<Evidence>
        {
            new()
            {
                Source = EvidenceSource.PerformanceCounter,
                Description = $"CPU ≥ {RuleText.Percent(threshold)} sem interrupção por {RuleText.Duration(run)}",
                Value = run.TotalSeconds,
                Unit = "s",
                Reference = @"GetSystemTimes / \Processor Information(_Total)\% Processor Time",
            },
            new()
            {
                Source = EvidenceSource.PerformanceCounter,
                Description = $"Uso médio de CPU no período observado: {RuleText.Percent(series.Mean)}",
                Value = series.Mean,
                Unit = "%",
                SampleCount = series.Count,
                Reference = PerfNames.CpuTotal,
            },
            RuleText.Window(series, "Observação"),
        };
        evidence.AddRange(RuleText.TopProcesses(context, PerfNames.ObservationProcessCpu, "cpuPercent", RuleText.Percent, "durante a varredura"));

        var top = RuleText.TopProcessName(context, PerfNames.ObservationProcessCpu, "cpuPercent");
        yield return new Finding
        {
            RuleId = Id,
            ScannerId = CpuScanner.ScannerId,
            Category = FindingCategory.Cpu,
            Title = "CPU sobrecarregada por período prolongado",
            Description = $"O processador ficou acima de {RuleText.Percent(threshold)} de uso por {RuleText.Duration(run)} seguidos.",
            Severity = run.TotalSeconds >= context.Parameter("HighSeveritySeconds") ? Severity.High : Severity.Medium,
            Confidence = Confidence.FromPercent(95),
            Evidence = evidence,
            ProbableCause = top is null
                ? "Não foi possível identificar um processo dominante durante a varredura (carga distribuída ou processos protegidos)."
                : $"Maior consumidor durante a varredura: {top}.",
            Impact = "Aplicativos respondem com atraso, compilações e builds ficam mais lentos e o computador aquece e consome mais energia.",
            Recommendation = "Verifique se a carga é esperada (build, máquina virtual, renderização). Se não for, encerre ou atualize o processo responsável; se for recorrente sem motivo, investigue tarefas agendadas e programas de inicialização.",
        };
    }
}
