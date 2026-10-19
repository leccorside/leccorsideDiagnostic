using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Storage;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>SSD sem TRIM: o Windows não avisa o SSD sobre blocos apagados e as gravações ficam mais lentas com o tempo.</summary>
public sealed class TrimDisabledRule : IRule
{
    public string Id => "disk.trim-disabled";

    public string Name => "TRIM desativado em SSD";

    public IReadOnlyList<string> RequiredScanners { get; } = [DiskScanner.ScannerId];

    public IReadOnlyList<RuleParameter> Parameters { get; } = [];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        foreach (var disk in context.ObservationsOf(StorageNames.ObservationDisk))
        {
            var ntfsTrim = disk.Flag("ntfsTrim");
            var deviceTrim = disk.Flag("trim");
            if (disk.Flag("solidState") != true || (ntfsTrim != false && deviceTrim != false))
            {
                continue;
            }

            var name = disk.GetAttribute("name") ?? disk.Subject;
            var evidence = new List<Evidence>();
            if (ntfsTrim == false)
            {
                evidence.Add(new Evidence
                {
                    Source = EvidenceSource.Registry,
                    Description = "TRIM desativado para NTFS no sistema (DisableDeleteNotification = 1)",
                    Reference = @"HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\DisableDeleteNotification",
                });
            }

            if (deviceTrim == false)
            {
                evidence.Add(new Evidence
                {
                    Source = EvidenceSource.WindowsApi,
                    Description = $"{name} informou TRIM desativado",
                    Reference = "IOCTL_STORAGE_QUERY_PROPERTY (StorageDeviceTrimProperty)",
                });
            }

            yield return new Finding
            {
                RuleId = Id,
                ScannerId = DiskScanner.ScannerId,
                Category = FindingCategory.Disk,
                Title = $"TRIM desativado no SSD {name}",
                Description = "O Windows não está avisando o SSD sobre os blocos apagados.",
                Severity = Severity.Low,
                Confidence = Confidence.FromPercent(90),
                Evidence = evidence,
                ProbableCause = ntfsTrim == false
                    ? "Configuração alterada manualmente ou por um otimizador antigo (o padrão do Windows é TRIM ativo)."
                    : "Controlador, driver ou adaptador (RAID/USB) que não repassa o TRIM ao SSD.",
                Impact = "As gravações ficam mais lentas com o tempo e o desgaste do SSD aumenta.",
                Recommendation = ntfsTrim == false
                    ? "Em um terminal de administrador, execute: fsutil behavior set DisableDeleteNotify 0 (o aplicativo não executa comandos por você neste passo)."
                    : "Atualize o driver do controlador de armazenamento; em RAID ou gaveta USB, confira se o adaptador suporta TRIM.",
            };
        }
    }
}
