using System.Globalization;
using LeccorWindowsHealthCenter.Application.DevTools;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta entradas órfãs ou diretórios inexistentes na variável de ambiente PATH (seção 31 do PROMPT).
/// </summary>
public sealed class DevPathInvalidEntriesRule : IRule
{
    public string Id => "dev.path-invalid-entries";

    public string Name => "Entradas inválidas na variável de ambiente PATH";

    public IReadOnlyList<string> RequiredScanners { get; } = ["devtools"];

    public IReadOnlyList<RuleParameter> Parameters { get; } = [];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var pathObs = context.Observations.FirstOrDefault(o => o.Kind == DevToolsNames.ObservationPath);
        if (pathObs == null)
        {
            yield break;
        }

        var countStr = pathObs.Attributes.GetValueOrDefault(DevToolsNames.AttrPathInvalidCount);
        if (!int.TryParse(countStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count <= 0)
        {
            yield break;
        }

        var evidence = new List<Evidence>
        {
            new()
            {
                Source = EvidenceSource.Registry,
                Description = $"A variável PATH contém {count} caminho(s) que apontam para pastas inexistentes no disco.",
                Reference = "PATH",
                CollectedAt = DateTimeOffset.UtcNow
            }
        };

        yield return new Finding
        {
            Id = Guid.NewGuid(),
            RuleId = Id,
            ScannerId = "devtools",
            Category = FindingCategory.Development,
            Severity = Severity.Low,
            Confidence = Confidence.FromPercent(90),
            Title = $"{count} diretório(s) inexistente(s) na variável PATH",
            Description = $"Foram identificados caminhos órfãos configurados na variável de ambiente PATH que não existem mais no disco.",
            ProbableCause = "Softwares, compilers ou SDKs desinstalados que não removeram suas entradas do PATH do usuário ou do sistema.",
            Impact = "Pode retardar a busca de comandos no terminal e causar avisos ou falhas em scripts de build.",
            Recommendation = "Remover os caminhos obsoletos das Variáveis de Ambiente do Windows.",
            Evidence = evidence,
            RequiresElevation = false,
            RequiresRestart = false,
            SupportsRollback = false,
            DetectedAt = DateTimeOffset.UtcNow
        };
    }
}
