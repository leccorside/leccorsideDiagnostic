using LeccorWindowsHealthCenter.Application.Drivers;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta dispositivos com falha ou código de erro no Gerenciador de Dispositivos do Windows (seção 25 do PROMPT).
/// </summary>
public sealed class DriversDeviceErrorRule : IRule
{
    public string Id => "drivers.device-error";

    public string Name => "Dispositivo com falha ou erro de hardware/driver";

    public IReadOnlyList<string> RequiredScanners { get; } = ["drivers"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("Enabled", 1, "booleano", "Identifica dispositivos com códigos de problema no Gerenciador de Dispositivos."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var observations = context.Observations.Where(o => o.Kind == DriverNames.ObservationDevice);

        foreach (var obs in observations)
        {
            var isProblemStr = obs.Attributes.GetValueOrDefault(DriverNames.AttrIsProblem);
            if (!bool.TryParse(isProblemStr, out var isProblem) || !isProblem)
            {
                continue;
            }

            var isMissingStr = obs.Attributes.GetValueOrDefault(DriverNames.AttrIsMissing);
            if (bool.TryParse(isMissingStr, out var isMissing) && isMissing)
            {
                continue; // Código 28 (driver ausente) é tratado em regra dedicada
            }

            var deviceName = obs.Subject;
            var deviceId = obs.Attributes.GetValueOrDefault(DriverNames.AttrDeviceId) ?? string.Empty;
            var errorCode = obs.Attributes.GetValueOrDefault(DriverNames.AttrErrorCode) ?? "0";
            var statusMessage = obs.Attributes.GetValueOrDefault(DriverNames.AttrStatusMessage) ?? "O dispositivo reportou um erro ao sistema operacional.";
            var manufacturer = obs.Attributes.GetValueOrDefault(DriverNames.AttrManufacturer);

            var evidence = new List<Evidence>
            {
                new()
                {
                    Source = EvidenceSource.Wmi,
                    Description = $"Dispositivo '{deviceName}' com código de erro {errorCode}: {statusMessage}",
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
                Severity = Severity.High,
                Confidence = Confidence.FromPercent(95),
                Title = $"Dispositivo com código de erro: {deviceName}",
                Description = $"O Windows identificou uma falha no dispositivo '{deviceName}' (Código {errorCode}). {statusMessage}",
                ProbableCause = "Driver incompatível, falha de firmware, problema elétrico/físico no hardware ou conflito de recursos.",
                Impact = "O dispositivo não funciona ou opera em modo degradado/instável.",
                Recommendation = "Atualizar o driver pelo site do fabricante ou pelo Windows Update, ou reconectar o dispositivo se for removível/USB.",
                Evidence = evidence,
                RequiresElevation = false,
                RequiresRestart = false,
                SupportsRollback = false,
                DetectedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}

