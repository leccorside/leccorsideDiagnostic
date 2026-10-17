using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Application.Rules;

/// <summary>
/// Regra de diagnóstico (seção 47). Toda decisão "isto é um problema?" vive em uma regra —
/// nunca espalhada por scanners ou ViewModels.
/// </summary>
public interface IRule
{
    /// <summary>Identificador estável, ex.: "cpu.sustained-high". Usado na configuração de thresholds.</summary>
    string Id { get; }

    string Name { get; }

    /// <summary>Scanners cujos dados a regra precisa. A regra só roda se TODOS concluíram com sucesso.</summary>
    IReadOnlyList<string> RequiredScanners { get; }

    /// <summary>Parâmetros configuráveis com valor padrão e justificativa técnica.</summary>
    IReadOnlyList<RuleParameter> Parameters { get; }

    IEnumerable<Finding> Evaluate(RuleContext context);
}

/// <summary>
/// Threshold configurável de uma regra. A justificativa é obrigatória: thresholds sem
/// fundamento técnico geram falsos positivos (seção 47).
/// </summary>
public sealed record RuleParameter(string Key, double DefaultValue, string Unit, string Justification);
