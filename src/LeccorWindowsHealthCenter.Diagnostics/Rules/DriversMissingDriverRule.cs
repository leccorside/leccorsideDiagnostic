using LeccorWindowsHealthCenter.Application.Drivers;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta dispositivos sem driver instalado no Windows (Dispositivo Desconhecido / Código 28) (seção 25 do PROMPT).
/// </summary>
public sealed class DriversMissingDriverRule : IRule
{
    public string Id => "drivers.missing-driver";

    public string Name => "Dispositivo sem driver instalado (desconhecido)";

    public IReadOnlyList<string> RequiredScanners { get; } = ["drivers"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("Enabled", 1, "booleano", "Identifica dispositivos instalados na placa-mãe ou conectados que não possuem driver."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var observations = context.Observations.Where(o => o.Kind == DriverNames.ObservationDevice);

        foreach (var obs in observations)
        {
            var isMissingStr = obs.Attributes.GetValueOrDefault(DriverNames.AttrIsMissing);
            if (!bool.TryParse(isMissingStr, out var isMissing) || !isMissing)
            {
                continue;
            }

            var deviceName = obs.Subject;
            var deviceId = obs.Attributes.GetValueOrDefault(DriverNames.AttrDeviceId) ?? string.Empty;

            var evidence = new List<Evidence>
            {
                new()
                {
                    Source = EvidenceSource.Wmi,
                    Description = $"Dispositivo '{deviceName}' não possui driver instalado (Código 28)",
                    Reference = deviceId,
                    CollectedAt = DateTimeOffset.UtcNow,
                },
            };

            yield return new Finding
            {
                Id = Guid.NewGuid(),
                RuleId = Id,
                ScannerId = "drivers",
                Category = FindingCategory.Drivers,
                Severity = Severity.Medium,
                Confidence = Confidence.FromPercent(95),
                Title = $"Driver ausente para dispositivo: {deviceName}",
                Description = $"O dispositivo '{deviceName}' está presente no computador, mas não possui driver instalado e não pode ser utilizado.",
                ProbableCause = "Dispositivo recente ou periférico adicionado sem instalação do pacote de drivers correspondente.",
                Impact = "As funcionalidades associadas a este hardware não estão operacionais.",
                Recommendation = "Verificar se há atualizações opcionais de drivers no Windows Update ou baixar o instalador oficial no site do fabricante.",
                Evidence = evidence,
                RequiresElevation = false,
                RequiresRestart = false,
                SupportsRollback = false,
                DetectedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}

