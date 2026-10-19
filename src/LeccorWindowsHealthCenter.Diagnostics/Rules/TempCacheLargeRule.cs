using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Storage;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>Temporários e caches ocupando muito espaço. Recomendação (Info): não reduz o score e não apaga nada.</summary>
public sealed class TempCacheLargeRule : IRule
{
    public string Id => "storage.temp-cache-large";

    public string Name => "Temporários e caches ocupando espaço";

    public IReadOnlyList<string> RequiredScanners { get; } = [StorageScanner.ScannerId];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("MinimumGb", 5, "GB", "Abaixo de 5 GB a limpeza raramente muda algo perceptível; acima, costuma valer a revisão."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var total = context.Series(StorageNames.TempCacheTotal).Latest;
        var minimum = context.Parameter("MinimumGb") * 1024 * 1024 * 1024;
        if (total is not { } bytes || bytes < minimum)
        {
            yield break;
        }

        var evidence = context.ObservationsOf(StorageNames.ObservationLocation)
            .Where(o => o.GetAttribute("kind") != "RecycleBin" && o.Number("sizeBytes") is > 0)
            .OrderByDescending(o => o.Number("sizeBytes"))
            .Select(o => new Evidence
            {
                Source = EvidenceSource.FileSystem,
                Description = $"{o.GetAttribute("title")}: {RuleText.Bytes(o.Number("sizeBytes")!.Value)} em {o.Number("files")?.ToString("#,##0", RuleText.PtBr)} arquivos"
                    + (o.Number("inaccessible") is > 0 ? " (parte sem permissão de leitura: pode ser maior)" : string.Empty),
                Value = o.Number("sizeBytes"),
                Unit = "bytes",
                Reference = o.GetAttribute("paths"),
            })
            .ToList();

        yield return new Finding
        {
            RuleId = Id,
            ScannerId = StorageScanner.ScannerId,
            Category = FindingCategory.Storage,
            Title = $"Temporários e caches ocupam {RuleText.Bytes(bytes)}",
            Description = $"Os locais conhecidos de temporários e caches somam {RuleText.Bytes(bytes)} (a Lixeira não entra nessa conta).",
            Severity = Severity.Info,
            Confidence = Confidence.FromPercent(100),
            Evidence = evidence,
            Impact = "Espaço em disco ocupado por arquivos que os aplicativos normalmente recriam quando precisam.",
            Recommendation = "Revise os itens na tela Armazenamento. A limpeza segura por categorias chega na tela Limpeza; nada é apagado sem sua confirmação.",
        };
    }
}
