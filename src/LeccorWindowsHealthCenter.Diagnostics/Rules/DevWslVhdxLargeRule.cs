using System.Globalization;
using LeccorWindowsHealthCenter.Application.DevTools;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta distribuições do WSL2 com discos virtuais (ext4.vhdx) excessivamente volumosos (> 50 GB) (seção 33 do PROMPT).
/// </summary>
public sealed class DevWslVhdxLargeRule : IRule
{
    private const long ThresholdBytes = 50L * 1024 * 1024 * 1024; // 50 GB

    public string Id => "dev.wsl-vhdx-large";

    public string Name => "Disco virtual WSL (ext4.vhdx) volumoso";

    public IReadOnlyList<string> RequiredScanners { get; } = ["devtools"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("ThresholdGb", 50, "gigabytes", "Tamanho mínimo em GB para alertar sobre crescimento do VHDX do WSL."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var distros = context.Observations.Where(o => o.Kind == DevToolsNames.ObservationWslDistro);

        foreach (var distro in distros)
        {
            var sizeStr = distro.Attributes.GetValueOrDefault(DevToolsNames.AttrWslVhdxSize);
            if (!long.TryParse(sizeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sizeBytes) || sizeBytes < ThresholdBytes)
            {
                continue;
            }

            var distroName = distro.Subject;
            var sizeGb = sizeBytes / (1024.0 * 1024.0 * 1024.0);

            var evidence = new List<Evidence>
            {
                new()
                {
                    Source = EvidenceSource.FileSystem,
                    Description = $"O disco virtual ext4.vhdx da distribuição '{distroName}' ocupa {sizeGb:F1} GB na unidade do sistema.",
                    Reference = distroName,
                    CollectedAt = DateTimeOffset.UtcNow
                }
            };

            yield return new Finding
            {
                Id = Guid.NewGuid(),
                RuleId = Id,
                ScannerId = "devtools",
                Category = FindingCategory.Storage,
                Severity = Severity.Medium,
                Confidence = Confidence.FromPercent(95),
                Title = $"Disco WSL da distribuição \"{distroName}\" ocupa {sizeGb:F1} GB",
                Description = $"A imagem de disco virtual do WSL2 para '{distroName}' cresceu consideravelmente e está consumindo {sizeGb:F1} GB.",
                ProbableCause = "O WSL2 aloca espaço dinamicamente conforme pacotes, imagens Docker e arquivos são criados, mas não o reduz automaticamente quando os arquivos são excluídos.",
                Impact = "Reduz o espaço livre na unidade do sistema (C:), podendo levar a avisos de pouco espaço e degradação de desempenho.",
                Recommendation = "No Windows 11, ative o modo esparso com 'wsl --manage " + distroName + " --set-sparse true' ou execute compactação com diskpart.",
                Evidence = evidence,
                RequiresElevation = false,
                RequiresRestart = false,
                SupportsRollback = false,
                DetectedAt = DateTimeOffset.UtcNow
            };
        }
    }
}

