using System.Globalization;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Performance;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Shared.Formatting;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>Formatação pt-BR e evidências reutilizadas pelas regras de performance.</summary>
internal static class RuleText
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Intervalo máximo entre amostras para considerar uma sequência contínua (histórico a cada 5 s).</summary>
    public static readonly TimeSpan MaxSampleGap = TimeSpan.FromSeconds(30);

    public static string Percent(double value) => value.ToString("0", PtBr) + "%";

    /// <summary>Uma casa decimal: "7,2%" (espaço livre perto do limite não pode virar "7%" e "7%").</summary>
    public static string PercentPrecise(double value) => value.ToString("0.#", PtBr) + "%";

    public static string Duration(TimeSpan duration) =>
        duration.TotalMinutes >= 1
            ? string.Create(PtBr, $"{duration.TotalMinutes:0.#} min")
            : string.Create(PtBr, $"{duration.TotalSeconds:0} s");

    public static string Bytes(double bytes) => ByteSize.Format((long)bytes, PtBr);

    /// <summary>Janela observada (histórico + varredura), para dizer ao usuário "observado por X".</summary>
    public static Evidence Window(MetricSeries series, string what) => new()
    {
        Source = EvidenceSource.PerformanceCounter,
        Description = $"{what}: {series.Count} amostras ao longo de {Duration(series.Duration)}",
        SampleCount = series.Count,
        Reference = series.Name,
    };

    /// <summary>Até três maiores consumidores registrados pelo scanner, como evidência complementar.</summary>
    public static IEnumerable<Evidence> TopProcesses(RuleContext context, string kind, string valueAttribute, Func<double, string> format, string context2)
    {
        return context.ObservationsOf(kind)
            .Select(o => (Observation: o, Value: double.TryParse(o.GetAttribute(valueAttribute), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0))
            .OrderByDescending(x => x.Value)
            .Take(3)
            .Select(x => new Evidence
            {
                Source = EvidenceSource.Process,
                Description = $"{x.Observation.GetAttribute("name")} (PID {x.Observation.GetAttribute("pid")}) {context2}: {format(x.Value)}",
                Value = x.Value,
                Reference = string.IsNullOrEmpty(x.Observation.GetAttribute("path")) ? x.Observation.Subject : x.Observation.GetAttribute("path"),
            });
    }

    public static string? TopProcessName(RuleContext context, string kind, string valueAttribute) =>
        context.ObservationsOf(kind)
            .OrderByDescending(o => double.TryParse(o.GetAttribute(valueAttribute), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0)
            .Select(o => o.GetAttribute("name"))
            .FirstOrDefault();

    public static MetricSeries ScanOnly(RuleContext context, string metric) => context.Series(metric, PerfNames.SourceTag, PerfNames.SourceScan);
}
