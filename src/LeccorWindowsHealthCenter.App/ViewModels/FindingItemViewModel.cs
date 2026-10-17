using System.Globalization;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.App.ViewModels;

/// <summary>
/// Finding pronto para exibição. Responde às perguntas da seção 72: o que aconteceu, como
/// sabemos, qual impacto, como corrigir, qual o risco da correção e se é possível desfazer.
/// </summary>
public sealed class FindingItemViewModel
{
    public FindingItemViewModel(Finding finding)
    {
        Finding = finding;
        Evidence = finding.Evidence.Select(FormatEvidence).ToList();
    }

    public Finding Finding { get; }

    public Severity Severity => Finding.Severity;

    public string Title => Finding.Title;

    public string SeverityText => Display.Severity(Finding.Severity);

    public string WhatHappened => Finding.Description;

    public IReadOnlyList<EvidenceLine> Evidence { get; }

    public string? ProbableCause => Finding.ProbableCause;

    public string Impact => Finding.Impact;

    public string Recommendation => Finding.Recommendation;

    public string ConfidenceText => Display.Confidence(Finding.Confidence);

    public string RiskText => Display.Risk(Finding.RepairRisk);

    public string RollbackText => Finding.HasRepair ? (Finding.SupportsRollback ? "Sim" : "Não") : "—";

    public string Requirements
    {
        get
        {
            var parts = new List<string>();
            if (Finding.RequiresElevation)
            {
                parts.Add("exige administrador");
            }

            if (Finding.RequiresRestart)
            {
                parts.Add("exige reinicialização");
            }

            return parts.Count == 0 ? "—" : string.Join(", ", parts);
        }
    }

    private static EvidenceLine FormatEvidence(Evidence evidence)
    {
        var value = evidence.Value is null
            ? null
            : string.Create(CultureInfo.CurrentCulture, $"{evidence.Value:0.##} {evidence.Unit}").Trim();
        var samples = evidence.SampleCount > 1 ? $" · {evidence.SampleCount} amostras" : string.Empty;
        return new EvidenceLine(evidence.Description + samples, value, $"{evidence.Source}: {evidence.Reference ?? "-"}");
    }
}

/// <summary>Linha de evidência. <see cref="Technical"/> só aparece no modo avançado.</summary>
public sealed record EvidenceLine(string Text, string? Value, string Technical);
