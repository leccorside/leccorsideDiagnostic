using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Diagnostics.Startup;
using LeccorWindowsHealthCenter.Diagnostics.Storage;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Entrada de inicialização ATIVA apontando para um arquivo que não existe: resto de programa desinstalado. O Windows
/// tenta iniciar a cada logon e falha em silêncio (ou mostra "o Windows não encontrou…").
/// </summary>
public sealed class StartupMissingExecutableRule : IRule
{
    public string Id => "startup.missing-executable";

    public string Name => "Inicialização aponta para arquivo inexistente";

    public IReadOnlyList<string> RequiredScanners { get; } = [StartupScanner.ScannerId];

    public IReadOnlyList<RuleParameter> Parameters { get; } = [];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        foreach (var entry in context.ObservationsOf(StartupScanner.ObservationEntry))
        {
            // Só com prova: executável interpretado, conferido e ausente; entrada ativa. Desativada não roda, não incomoda.
            if (entry.Flag("exists") != false || entry.Flag("enabled") != true || string.IsNullOrEmpty(entry.GetAttribute("executable")))
            {
                continue;
            }

            var name = entry.GetAttribute("name") ?? entry.Subject;
            var service = entry.GetAttribute("source") == "Service";

            // Tarefa do próprio Windows (\Microsoft\Windows\…) cujo arquivo uma atualização removeu (ex.: Autochk\Proxy no
            // Windows 11): falha sem efeito. Informativo, sem sugerir desativar componente do sistema.
            if (entry.Flag("windows") == true)
            {
                yield return WindowsLeftover(entry, name);
                continue;
            }

            yield return new Finding
            {
                RuleId = Id,
                ScannerId = StartupScanner.ScannerId,
                Category = FindingCategory.Startup,
                Title = service ? $"Serviço \"{name}\" aponta para um arquivo que não existe" : $"Inicialização de \"{name}\" aponta para um arquivo que não existe",
                Description = $"O Windows tenta iniciar \"{name}\" a cada {(service ? "boot" : "logon")}, mas o arquivo não está mais no disco.",
                Severity = service ? Severity.Medium : Severity.Low,
                Confidence = Confidence.FromPercent(95),
                Evidence =
                [
                    new Evidence
                    {
                        Source = service ? EvidenceSource.Wmi : EvidenceSource.Registry,
                        Description = $"Entrada em {entry.GetAttribute("location")}: {entry.GetAttribute("command")}",
                        Reference = entry.GetAttribute("id"),
                    },
                    new Evidence
                    {
                        Source = EvidenceSource.FileSystem,
                        Description = $"Arquivo não encontrado: {entry.GetAttribute("executable")}",
                        Reference = entry.GetAttribute("executable"),
                    },
                ],
                ProbableCause = "O programa foi desinstalado ou movido e deixou a entrada de inicialização para trás.",
                Impact = service
                    ? "O serviço falha em todo boot, gera erros no log do sistema e pode atrasar a inicialização."
                    : "Nada é iniciado; a entrada só polui a lista e, em alguns casos, mostra um erro no logon.",
                Recommendation = "Se o programa não está mais instalado, a entrada pode ser removida. A remoção com backup e desfazer chega na tela Correções; por ora, remova pelo próprio programa ou pelo Gerenciador de Tarefas.",
            };
        }
    }

    private Finding WindowsLeftover(Observation entry, string name) => new()
    {
        RuleId = Id,
        ScannerId = StartupScanner.ScannerId,
        Category = FindingCategory.Startup,
        Title = $"Tarefa do Windows \"{name}\" aponta para um arquivo removido",
        Description = $"A tarefa \"{name}\" faz parte do próprio Windows, mas o arquivo que ela chama não existe mais.",
        Severity = Severity.Info,
        Confidence = Confidence.FromPercent(95),
        Evidence =
        [
            new Evidence
            {
                Source = EvidenceSource.Registry,
                Description = $"Tarefa {entry.GetAttribute("location")}: {entry.GetAttribute("command")}",
                Reference = entry.GetAttribute("id"),
            },
            new Evidence
            {
                Source = EvidenceSource.FileSystem,
                Description = $"Arquivo não encontrado: {entry.GetAttribute("executable")}",
                Reference = entry.GetAttribute("executable"),
            },
        ],
        ProbableCause = "Uma atualização do Windows removeu o arquivo e manteve a tarefa.",
        Impact = "Nenhum: a tarefa falha sem efeito visível.",
        Recommendation = "Não é preciso fazer nada. Evite desativar tarefas do próprio Windows: a próxima atualização pode corrigi-la ou removê-la.",
    };
}
