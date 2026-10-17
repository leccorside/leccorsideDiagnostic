using System.Diagnostics;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Application.Rules;

public interface IRuleEngine
{
    /// <summary>Avalia as regras sobre os resultados e devolve os resultados com os findings anexados.</summary>
    IReadOnlyList<DiagnosticResult> Evaluate(IReadOnlyList<DiagnosticResult> results);
}

/// <summary>
/// Motor central de regras (seção 47). Garantias:
/// <list type="bullet">
/// <item>regra só roda se todos os scanners exigidos concluíram com sucesso (sem dados = sem conclusão);</item>
/// <item>finding sem evidência é descartado — nunca inventar problemas (seções 1 e 48);</item>
/// <item>falha em uma regra não derruba as demais.</item>
/// </list>
/// </summary>
public sealed class RuleEngine : IRuleEngine
{
    private readonly IEnumerable<IRule> _rules;
    private readonly IRuleSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RuleEngine> _logger;

    public RuleEngine(IEnumerable<IRule> rules, IRuleSettings settings, TimeProvider timeProvider, ILogger<RuleEngine> logger)
    {
        _rules = rules;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public IReadOnlyList<DiagnosticResult> Evaluate(IReadOnlyList<DiagnosticResult> results)
    {
        var completed = results
            .Where(r => r.Status == ScannerRunStatus.Completed)
            .ToDictionary(r => r.ScannerId, StringComparer.OrdinalIgnoreCase);

        var metrics = completed.Values.SelectMany(r => r.Metrics).ToList();
        var observations = completed.Values.SelectMany(r => r.Observations).ToList();
        var findingsByScanner = new Dictionary<string, List<Finding>>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in _rules)
        {
            if (!_settings.IsEnabled(rule.Id))
            {
                _logger.LogDebug("Regra {RuleId} desabilitada por configuração", rule.Id);
                continue;
            }

            if (rule.RequiredScanners.Count == 0 || !rule.RequiredScanners.All(completed.ContainsKey))
            {
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var context = new RuleContext(rule, metrics, observations, _settings, _timeProvider);
                foreach (var finding in rule.Evaluate(context))
                {
                    if (!IsValid(rule, finding))
                    {
                        continue;
                    }

                    var normalized = finding.RuleId == rule.Id ? finding : finding with { RuleId = rule.Id };
                    if (!findingsByScanner.TryGetValue(normalized.ScannerId, out var list))
                    {
                        findingsByScanner[normalized.ScannerId] = list = [];
                    }

                    list.Add(normalized);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Regra {RuleId} falhou durante a avaliação; ignorada nesta varredura", rule.Id);
            }
            finally
            {
                _logger.LogDebug("Regra {RuleId} avaliada em {DurationMs} ms", rule.Id, stopwatch.ElapsedMilliseconds);
            }
        }

        return results
            .Select(r => findingsByScanner.TryGetValue(r.ScannerId, out var found)
                ? r with { Findings = [.. r.Findings, .. found] }
                : r)
            .ToList();
    }

    private bool IsValid(IRule rule, Finding finding)
    {
        if (finding.Evidence.Count == 0)
        {
            _logger.LogWarning("Regra {RuleId} produziu o finding '{Title}' sem evidência — descartado", rule.Id, finding.Title);
            return false;
        }

        if (finding.RepairActionId is not null && finding.RepairRisk is null)
        {
            _logger.LogWarning("Regra {RuleId} produziu o finding '{Title}' com correção mas sem risco declarado — descartado", rule.Id, finding.Title);
            return false;
        }

        return true;
    }
}
