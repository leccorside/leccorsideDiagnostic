using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Startup;
using LeccorWindowsHealthCenter.Diagnostics.Startup;
using LeccorWindowsHealthCenter.Diagnostics.Storage;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Hospedeiro de script do Windows (PowerShell, mshta, rundll32, regsvr32…) iniciado no logon com um padrão que
/// programas legítimos quase nunca usam: comando codificado, janela oculta baixando da internet, script embutido na
/// linha de comando. O executável é do Windows, então nada no caminho denuncia; o risco está no argumento.
/// "Possível": alguns instaladores fazem isso uma vez (RunOnce) para terminar a instalação.
/// </summary>
public sealed class StartupScriptHostRule : IRule
{
    public string Id => "startup.script-host";

    public string Name => "Inicialização executa script oculto";

    public IReadOnlyList<string> RequiredScanners { get; } = [StartupScanner.ScannerId];

    public IReadOnlyList<RuleParameter> Parameters { get; } = [];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        foreach (var entry in context.ObservationsOf(StartupScanner.ObservationEntry))
        {
            var command = entry.GetAttribute("command");
            if (entry.Flag("enabled") != true || StartupCommand.ScriptConcern(command) is not { } concern)
            {
                continue;
            }

            var name = entry.GetAttribute("name") ?? entry.Subject;
            yield return new Finding
            {
                RuleId = Id,
                ScannerId = StartupScanner.ScannerId,
                Category = FindingCategory.Startup,
                Title = $"\"{name}\" executa um script oculto na inicialização",
                Description = $"A entrada de inicialização \"{name}\" usa um programa do Windows para rodar um script. {concern}",
                Severity = Severity.Medium,

                // Possível, não confirmado: instaladores às vezes terminam a instalação assim, uma única vez.
                Confidence = Confidence.FromPercent(60),
                Evidence =
                [
                    new Evidence
                    {
                        Source = EvidenceSource.Registry,
                        Description = $"Entrada em {entry.GetAttribute("location")}: {command}",
                        Reference = entry.GetAttribute("id"),
                    },
                ],
                ProbableCause = "Um instalador que termina a configuração no próximo logon, ou um software indesejado persistindo sem deixar um executável próprio.",
                Impact = "O que o script faz não aparece no nome nem no caminho do programa; é a forma mais comum de persistência de malware sem arquivo.",
                Recommendation = "Copie a linha de comando na tela Inicialização e confira de onde veio. Se não reconhecer, faça uma verificação completa com o Windows Defender antes de remover a entrada.",
            };
        }
    }
}
