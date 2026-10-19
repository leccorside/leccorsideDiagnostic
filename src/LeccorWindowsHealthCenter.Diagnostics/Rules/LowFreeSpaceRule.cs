using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Storage;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Pouco espaço livre. O volume do Windows tem limites próprios (atualizações, paginação, temporários); volumes de
/// dados só alertam quando o percentual E o valor absoluto estão baixos — 5% de 4 TB ainda são 200 GB.
/// </summary>
public sealed class LowFreeSpaceRule : IRule
{
    private const double Gb = 1024d * 1024 * 1024;

    public string Id => "storage.low-free-space";

    public string Name => "Pouco espaço livre";

    public IReadOnlyList<string> RequiredScanners { get; } = [DiskScanner.ScannerId];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("SystemMinimumFreeGb", 15, "GB", "Atualizações de recurso do Windows pedem cerca de 20 GB livres no volume do sistema; abaixo de 15 GB a instalação costuma falhar."),
        new("SystemCriticalFreeGb", 5, "GB", "Com menos de 5 GB no volume do Windows, atualizações cumulativas, o crescimento do arquivo de paginação e instaladores já falham."),
        new("DataMinimumFreePercent", 5, "%", "Volume de dados com menos de 5% livre: cópias grandes falham e o NTFS fragmenta mais os arquivos novos."),
        new("DataMinimumFreeGb", 20, "GB", "Exigido junto com o percentual: em discos grandes, 5% ainda é bastante espaço e não justifica alerta."),
        new("AlwaysCriticalFreeGb", 1, "GB", "Abaixo de 1 GB qualquer volume já recusa gravações comuns (downloads, salvamentos, logs)."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        foreach (var volume in context.ObservationsOf(StorageNames.ObservationVolume))
        {
            if (volume.Flag("removable") == true
                || volume.Number("sizeBytes") is not { } size || size < Gb
                || volume.Number("freeBytes") is not { } free)
            {
                continue;
            }

            var isSystem = volume.Flag("system") == true;
            var freeGb = free / Gb;
            var freePercent = free * 100 / size;
            var severity = Classify(context, isSystem, freeGb, freePercent);
            if (severity is null)
            {
                continue;
            }

            var root = volume.GetAttribute("root") ?? volume.Subject;
            var drive = root.TrimEnd('\\');
            yield return new Finding
            {
                RuleId = Id,
                ScannerId = DiskScanner.ScannerId,
                Category = FindingCategory.Storage,
                Title = isSystem ? $"Pouco espaço no disco do Windows ({drive})" : $"Pouco espaço livre em {drive}",
                Description = $"{drive} tem {RuleText.Bytes(free)} livres de {RuleText.Bytes(size)} ({RuleText.PercentPrecise(freePercent)} livre).",
                Severity = severity.Value,
                Confidence = Confidence.FromPercent(100),
                Evidence =
                [
                    new Evidence
                    {
                        Source = EvidenceSource.Wmi,
                        Description = $"Espaço livre em {drive}: {RuleText.Bytes(free)} de {RuleText.Bytes(size)}",
                        Value = free,
                        Unit = "bytes",
                        Reference = "MSFT_Volume.SizeRemaining / Size",
                    },
                    new Evidence
                    {
                        Source = EvidenceSource.Configuration,
                        Description = isSystem
                            ? $"Limite para o volume do Windows: {context.Parameter("SystemMinimumFreeGb"):0} GB livres"
                            : $"Limite para volumes de dados: menos de {context.Parameter("DataMinimumFreePercent"):0}% e menos de {context.Parameter("DataMinimumFreeGb"):0} GB livres",
                        Reference = Id,
                    },
                ],
                ProbableCause = isSystem
                    ? "Arquivos acumulados em Downloads, temporários, caches e pastas de aplicativos; a tela Armazenamento mostra as maiores pastas."
                    : "O volume está quase cheio de dados; a tela Armazenamento mostra as maiores pastas e arquivos.",
                Impact = isSystem
                    ? "Atualizações do Windows falham, o arquivo de paginação não cresce (aplicativos podem fechar por falta de memória) e o sistema fica mais lento."
                    : "Cópias e gravações grandes falham nesse volume.",
                Recommendation = $"Abra Armazenamento, analise {drive} e mova ou apague o que você não precisa (arquivos pessoais nunca são apagados automaticamente).",
            };
        }
    }

    private static Severity? Classify(RuleContext context, bool isSystem, double freeGb, double freePercent)
    {
        if (freeGb < context.Parameter("AlwaysCriticalFreeGb"))
        {
            return isSystem ? Severity.Critical : Severity.Medium;
        }

        if (isSystem)
        {
            return freeGb < context.Parameter("SystemCriticalFreeGb") ? Severity.High
                : freeGb < context.Parameter("SystemMinimumFreeGb") ? Severity.Medium
                : null;
        }

        return freePercent < context.Parameter("DataMinimumFreePercent") && freeGb < context.Parameter("DataMinimumFreeGb")
            ? Severity.Low
            : null;
    }
}
