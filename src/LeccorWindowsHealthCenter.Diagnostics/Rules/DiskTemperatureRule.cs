using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Storage;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Temperatura alta — só quando o dispositivo informa a temperatura. Usa o limite de aviso do próprio disco quando
/// ele o declara; senão, limites típicos de fabricante por tipo de mídia.
/// </summary>
public sealed class DiskTemperatureRule : IRule
{
    public string Id => "disk.temperature-high";

    public string Name => "Disco com temperatura alta";

    public IReadOnlyList<string> RequiredScanners { get; } = [DiskScanner.ScannerId];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("SolidStateWarningCelsius", 70, "°C", "Controladores de SSD/NVMe costumam reduzir velocidade (thermal throttling) a partir de 70–80 °C; 70 °C é o máximo de operação mais comum nas fichas técnicas."),
        new("HddWarningCelsius", 55, "°C", "Fichas técnicas de HDD indicam máximo de operação entre 55 e 60 °C; acima disso a taxa de falhas sobe."),
        new("CriticalMarginCelsius", 10, "°C", "Sem limite crítico declarado pelo disco, 10 °C acima do aviso já é zona crítica."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        foreach (var disk in context.ObservationsOf(StorageNames.ObservationDisk))
        {
            if (disk.Number("temperature") is not { } celsius)
            {
                continue;
            }

            var solidState = disk.Flag("solidState") == true;
            var declaredWarning = disk.Number("warningTemperature");
            var warning = declaredWarning ?? context.Parameter(solidState ? "SolidStateWarningCelsius" : "HddWarningCelsius");
            if (celsius < warning)
            {
                continue;
            }

            var critical = disk.Number("criticalTemperature") ?? warning + context.Parameter("CriticalMarginCelsius");
            var name = disk.GetAttribute("name") ?? disk.Subject;
            yield return new Finding
            {
                RuleId = Id,
                ScannerId = DiskScanner.ScannerId,
                Category = FindingCategory.Disk,
                Title = $"{name} está quente ({celsius:0} °C)",
                Description = $"O disco informou {celsius:0} °C, acima do limite de aviso de {warning:0} °C.",
                Severity = celsius >= critical ? Severity.High : Severity.Medium,

                // Uma leitura só: pode ser um pico de cópia longa, não um estado permanente.
                Confidence = Confidence.FromPercent(85),
                Evidence =
                [
                    new Evidence
                    {
                        Source = EvidenceSource.WindowsApi,
                        Description = $"Temperatura informada pelo dispositivo: {celsius:0} °C",
                        Value = celsius,
                        Unit = "°C",
                        SampleCount = 1,
                        Reference = "IOCTL_STORAGE_QUERY_PROPERTY (StorageDeviceTemperatureProperty)",
                    },
                    new Evidence
                    {
                        Source = declaredWarning is null ? EvidenceSource.Configuration : EvidenceSource.WindowsApi,
                        Description = declaredWarning is null
                            ? $"Limite de aviso típico para {(solidState ? "SSD" : "HDD")}: {warning:0} °C (o disco não declarou o próprio limite)"
                            : $"Limite de aviso declarado pelo próprio disco: {warning:0} °C",
                        Reference = Id,
                    },
                ],
                ProbableCause = "Ventilação insuficiente no gabinete/notebook, disco NVMe sem dissipador ou carga de gravação contínua.",
                Impact = "O disco reduz a velocidade para esfriar e, mantido quente, desgasta mais rápido.",
                Recommendation = "Verifique a ventilação e a limpeza do gabinete; em NVMe, considere um dissipador. Se a temperatura persistir em uso leve, consulte o fabricante.",
            };
        }
    }
}
