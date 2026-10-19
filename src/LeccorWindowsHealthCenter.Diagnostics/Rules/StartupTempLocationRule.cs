using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Startup;
using LeccorWindowsHealthCenter.Diagnostics.Startup;
using LeccorWindowsHealthCenter.Diagnostics.Storage;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Programa iniciado a cada logon a partir de pasta temporária. Instaladores legítimos copiam para Program Files ou
/// AppData; rodar de Temp é técnica comum de malware para persistir. "Possível" (não confirmado): há atualizadores
/// mal feitos que fazem isso.
/// </summary>
public sealed class StartupTempLocationRule : IRule
{
    public string Id => "startup.temp-location";

    public string Name => "Inicialização a partir de pasta temporária";

    public IReadOnlyList<string> RequiredScanners { get; } = [StartupScanner.ScannerId];

    public IReadOnlyList<RuleParameter> Parameters { get; } = [];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        foreach (var entry in context.ObservationsOf(StartupScanner.ObservationEntry))
        {
            var exe = entry.GetAttribute("executable");
            if (entry.Flag("enabled") != true || !StartupCommand.IsTemporaryLocation(exe))
            {
                continue;
            }

            var name = entry.GetAttribute("name") ?? entry.Subject;
            var exists = entry.Flag("exists") == true;
            yield return new Finding
            {
                RuleId = Id,
                ScannerId = StartupScanner.ScannerId,
                Category = FindingCategory.Startup,
                Title = $"\"{name}\" inicia a partir de uma pasta temporária",
                Description = $"A entrada de inicialização \"{name}\" executa um arquivo dentro de uma pasta temporária a cada logon.",
                Severity = exists ? Severity.Medium : Severity.Low,

                // Possível, não confirmado: atualizadores mal escritos também fazem isso.
                Confidence = Confidence.FromPercent(60),
                Evidence =
                [
                    new Evidence
                    {
                        Source = EvidenceSource.Registry,
                        Description = $"Entrada em {entry.GetAttribute("location")}: {entry.GetAttribute("command")}",
                        Reference = entry.GetAttribute("id"),
                    },
                    new Evidence
                    {
                        Source = EvidenceSource.FileSystem,
                        Description = exists ? $"Executável em pasta temporária: {exe}" : $"Executável em pasta temporária (já apagado): {exe}",
                        Reference = exe,
                    },
                ],
                ProbableCause = "Um instalador ou atualizador deixou o programa em Temp, ou um software indesejado se instalou para persistir no logon.",
                Impact = "Programas em pastas temporárias não costumam ter atualização nem desinstalação; é também um padrão usado por malware.",
                Recommendation = "Confira o fabricante e a assinatura na tela Inicialização. Se não reconhecer o programa, faça uma verificação com o Windows Defender antes de remover a entrada.",
            };
        }
    }
}
