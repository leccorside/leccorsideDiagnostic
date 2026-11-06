using LeccorWindowsHealthCenter.Application.Explorer;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Identifica extensões do Windows Explorer de terceiros sem assinatura digital válida (seção 16 do PROMPT).
/// </summary>
public sealed class ExplorerUnsignedShellExtensionRule : IRule
{
    public string Id => "explorer.unsigned-shell-extension";

    public string Name => "Extensão do Windows Explorer sem assinatura digital";

    public IReadOnlyList<string> RequiredScanners { get; } = ["explorer"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("Enabled", 1, "booleano", "Gera recomendação preventiva para extensões de terceiros não assinadas carregadas pelo Explorer."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var observations = context.Observations.Where(o => o.Kind == ExplorerNames.ObservationShellExtension);

        foreach (var obs in observations)
        {
            var isWindowsStr = obs.Attributes.GetValueOrDefault(ExplorerNames.AttrWindows);
            if (bool.TryParse(isWindowsStr, out var isWindows) && isWindows)
            {
                continue; // Extensões nativas do Windows são ignoradas
            }

            var signedStr = obs.Attributes.GetValueOrDefault(ExplorerNames.AttrSigned);
            if (!bool.TryParse(signedStr, out var isSigned) || isSigned)
            {
                continue;
            }

            var name = obs.Subject;
            var clsid = obs.Attributes.GetValueOrDefault(ExplorerNames.AttrClsid) ?? string.Empty;
            var dllPath = obs.Attributes.GetValueOrDefault(ExplorerNames.AttrDllPath) ?? string.Empty;

            var evidence = new List<Evidence>
            {
                new()
                {
                    Source = EvidenceSource.FileSystem,
                    Description = $"Biblioteca '{dllPath}' não possui assinatura digital válida",
                    Reference = clsid,
                    CollectedAt = DateTimeOffset.UtcNow,
                },
            };

            yield return new Finding
            {
                Id = Guid.NewGuid(),
                RuleId = Id,
                ScannerId = "explorer",
                Category = FindingCategory.Explorer,
                Severity = Severity.Info,
                Confidence = Confidence.FromPercent(90),
                Title = $"Extensão do Windows Explorer sem assinatura: {name}",
                Description = $"A biblioteca de extensão '{dllPath}' não possui assinatura digital da Microsoft ou de fabricante conhecido.",
                ProbableCause = "Software legado ou ferramenta utilitária não assinada.",
                Impact = "Extensões sem assinatura rodando dentro de explorer.exe têm maior probabilidade de causar instabilidade ou vazamentos de memória no processo da Área de Trabalho.",
                Recommendation = "Verificar a autenticidade do aplicativo ou mantê-lo atualizado.",
                Evidence = evidence,
                RequiresElevation = false,
                RequiresRestart = false,
                SupportsRollback = true,
                DetectedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}

