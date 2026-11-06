using LeccorWindowsHealthCenter.Application.Network;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta perda de conectividade de rede ou gateway padrão inacessível (seção 27 do PROMPT).
/// </summary>
public sealed class NetworkNoGatewayRule : IRule
{
    public string Id => "network.no-gateway";

    public string Name => "Falha de conectividade ou Gateway inacessível";

    public IReadOnlyList<string> RequiredScanners { get; } = ["network"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("Enabled", 1, "booleano", "Identifica adaptadores ativos que não conseguem alcançar a internet ou o roteador local."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var diagObs = context.Observations.FirstOrDefault(o => o.Kind == NetworkNames.ObservationDiagnostic);
        if (diagObs == null)
        {
            yield break;
        }

        var internetStr = diagObs.Attributes.GetValueOrDefault(NetworkNames.AttrInternet);
        if (bool.TryParse(internetStr, out var hasInternet) && hasInternet)
        {
            yield break;
        }

        var lossStr = diagObs.Attributes.GetValueOrDefault(NetworkNames.AttrPacketLoss) ?? "100";
        var gatewayLatency = diagObs.Attributes.GetValueOrDefault(NetworkNames.AttrGatewayLatency);

        var evidence = new List<Evidence>
        {
            new()
            {
                Source = EvidenceSource.Measurement,
                Description = $"Teste de conectividade falhou: 100% de perda de pacotes para o gateway ou servidores DNS públicos",
                Reference = "NetworkDiagnostic",
                CollectedAt = DateTimeOffset.UtcNow,
            },
        };

        yield return new Finding
        {
            Id = Guid.NewGuid(),
            RuleId = Id,
            ScannerId = "network",
            Category = FindingCategory.Network,
            Severity = Severity.Medium,
            Confidence = Confidence.FromPercent(90),
            Title = "Sem conexão ativa com a internet ou gateway",
            Description = "Os testes de rede indicam que o computador está sem acesso à internet ou não consegue se comunicar com o roteador/gateway local.",
            ProbableCause = "Cabo de rede desconectado, Wi-Fi sem sinal, endereço IP estático configurado incorretamente ou roteador desligado/travado.",
            Impact = "Navegação na web, sincronização em nuvem e atualizações do sistema ficam inoperantes.",
            Recommendation = "Verificar a conexão Wi-Fi/cabo de rede, reiniciar o roteador ou executar a Solução de Problemas de Rede do Windows.",
            Evidence = evidence,
            RequiresElevation = false,
            RequiresRestart = false,
            SupportsRollback = false,
            DetectedAt = DateTimeOffset.UtcNow,
        };
    }
}

