using System.Globalization;
using LeccorWindowsHealthCenter.Application.Network;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta tempo excessivo de resposta na resolução de nomes de domínio (DNS > 200 ms) (seção 27 do PROMPT).
/// </summary>
public sealed class NetworkDnsSlowRule : IRule
{
    public string Id => "network.dns-slow";

    public string Name => "Tempo de resolução DNS elevado";

    public IReadOnlyList<string> RequiredScanners { get; } = ["network"];

    public IReadOnlyList<RuleParameter> Parameters { get; } =
    [
        new("ThresholdMs", 200, "ms", "Consultas DNS demorando mais de 200 ms causam atrasos perceptíveis ao abrir sites e conectar em serviços."),
    ];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var diagObs = context.Observations.FirstOrDefault(o => o.Kind == NetworkNames.ObservationDiagnostic);
        if (diagObs == null)
        {
            yield break;
        }

        var dnsTimeStr = diagObs.Attributes.GetValueOrDefault(NetworkNames.AttrDnsResolutionTime);
        if (string.IsNullOrWhiteSpace(dnsTimeStr) || !double.TryParse(dnsTimeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var dnsTime))
        {
            yield break;
        }

        var threshold = context.Parameter("ThresholdMs");
        if (dnsTime <= threshold)
        {
            yield break;
        }

        var evidence = new List<Evidence>
        {
            new()
            {
                Source = EvidenceSource.Measurement,
                Description = $"Tempo de resolução de nomes DNS medido em {dnsTime:0.#} ms (limite recomendado: {threshold:0} ms)",
                Value = dnsTime,
                Unit = "ms",
                Reference = "DnsResolution",
                CollectedAt = DateTimeOffset.UtcNow,
            },
        };

        yield return new Finding
        {
            Id = Guid.NewGuid(),
            RuleId = Id,
            ScannerId = "network",
            Category = FindingCategory.Network,
            Severity = Severity.Low,
            Confidence = Confidence.FromPercent(85),
            Title = $"Lentidão no servidor DNS ({dnsTime:0} ms)",
            Description = $"O tempo necessário para traduzir nomes de servidores na internet para endereços IP foi de {dnsTime:0.#} ms, acima do padrão ideal de {threshold:0} ms.",
            ProbableCause = "Servidor DNS fornecido pelo provedor de internet sobrecarregado ou com alta latência.",
            Impact = "Sensação de lentidão inicial e espera ao acessar novas páginas na web ou abrir aplicativos que usam serviços em nuvem.",
            Recommendation = "Configurar servidores DNS públicos rápidos e confiáveis (como Cloudflare 1.1.1.1 ou Google 8.8.8.8) nas propriedades do adaptador de rede.",
            Evidence = evidence,
            RequiresElevation = false,
            RequiresRestart = false,
            SupportsRollback = false,
            DetectedAt = DateTimeOffset.UtcNow,
        };
    }
}

