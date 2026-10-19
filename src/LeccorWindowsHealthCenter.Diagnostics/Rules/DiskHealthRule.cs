using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Storage;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// O próprio disco sinaliza problema: o Windows resume o SMART/NVMe em HealthStatus e OperationalStatus
/// (ex.: "Falha prevista"). Sem esse sinal, nada é concluído — nunca um percentual inventado (seção 14).
/// </summary>
public sealed class DiskHealthRule : IRule
{
    public string Id => "disk.health-warning";

    public string Name => "Disco sinaliza problema de saúde";

    public IReadOnlyList<string> RequiredScanners { get; } = [DiskScanner.ScannerId];

    public IReadOnlyList<RuleParameter> Parameters { get; } = [];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        foreach (var disk in context.ObservationsOf(StorageNames.ObservationDisk))
        {
            var health = disk.GetAttribute("health");
            var predicted = disk.Flag("predictedFailure") == true;
            var critical = predicted || health == "Unhealthy";
            if (!critical && health != "Warning")
            {
                continue;
            }

            var name = disk.GetAttribute("name") ?? disk.Subject;
            var status = disk.GetAttribute("operationalStatus");
            var evidence = new List<Evidence>
            {
                new()
                {
                    Source = EvidenceSource.Wmi,
                    Description = $"Estado de saúde informado pelo Windows para {name}: {HealthText(health)}",
                    Reference = "MSFT_PhysicalDisk.HealthStatus",
                },
            };

            if (!string.IsNullOrEmpty(status))
            {
                evidence.Add(new Evidence
                {
                    Source = EvidenceSource.Wmi,
                    Description = $"Estado operacional: {status}",
                    Reference = "MSFT_PhysicalDisk.OperationalStatus",
                });
            }

            yield return new Finding
            {
                RuleId = Id,
                ScannerId = DiskScanner.ScannerId,
                Category = FindingCategory.Disk,
                Title = critical ? $"O disco {name} indica falha" : $"O disco {name} indica atenção",
                Description = critical
                    ? "O próprio disco reportou ao Windows uma condição de falha (SMART/NVMe). Discos nesse estado podem parar de funcionar a qualquer momento."
                    : "O Windows marcou o disco com estado de atenção a partir dos dados de saúde que o dispositivo informa.",
                Severity = critical ? Severity.Critical : Severity.High,
                Confidence = Confidence.FromPercent(critical ? 95 : 85),
                Evidence = evidence,
                ProbableCause = "Desgaste ou falha física detectada pelo controlador do próprio disco.",
                Impact = "Risco de perda de dados e de travamentos ao ler ou gravar arquivos nesse disco.",
                Recommendation = "Faça backup dos dados importantes desse disco agora. Depois, confirme com a ferramenta do fabricante e planeje a substituição. Não execute desfragmentação nem verificações longas antes do backup.",
            };
        }
    }

    private static string HealthText(string? health) => health switch
    {
        "Unhealthy" => "com problema (Unhealthy)",
        "Warning" => "atenção (Warning)",
        "Healthy" => "sem alertas (Healthy)",
        _ => "não informado",
    };
}
