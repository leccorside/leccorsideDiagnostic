using LeccorWindowsHealthCenter.Application.Crash;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Diagnostics.Rules;

/// <summary>
/// Detecta ocorrência recente de tela azul (BSOD) ou arquivos de despejo de memória do kernel (seção 29 do PROMPT).
/// </summary>
public sealed class CrashKernelMinidumpRule : IRule
{
    public string Id => "crash.kernel-minidump-detected";

    public string Name => "Travamento do kernel (BSOD) detectado recentemente";

    public IReadOnlyList<string> RequiredScanners { get; } = ["crash"];

    public IReadOnlyList<RuleParameter> Parameters { get; } = [];

    public IEnumerable<Finding> Evaluate(RuleContext context)
    {
        var bsodObs = context.Observations.FirstOrDefault(o => o.Kind == CrashNames.ObservationBsod);
        var dumps = context.Observations.Where(o => o.Kind == CrashNames.ObservationDumpFile).ToList();

        if (bsodObs == null && dumps.Count == 0)
        {
            yield break;
        }

        var bugCheck = bsodObs?.Attributes.GetValueOrDefault(CrashNames.AttrBugCheck) ?? "Verificação de Erro de Kernel";

        var evidence = new List<Evidence>
        {
            new()
            {
                Source = EvidenceSource.FileSystem,
                Description = $"Foram encontrados {dumps.Count} arquivo(s) de despejo de memória (.dmp) ou registros de BSOD nos últimos 30 dias. Código: {bugCheck}.",
                Reference = "Minidump",
                CollectedAt = DateTimeOffset.UtcNow
            }
        };

        yield return new Finding
        {
            Id = Guid.NewGuid(),
            RuleId = Id,
            ScannerId = "crash",
            Category = FindingCategory.Windows,
            Severity = Severity.High,
            Confidence = Confidence.FromPercent(90),
            Title = "O Windows registrou travamentos críticos (BSOD) recentemente",
            Description = $"Foram identificados arquivos de despejo do kernel ou eventos de BugCheck (BSOD) no sistema. Detalhe: {bugCheck}.",
            ProbableCause = "Falha em drivers de dispositivos (placa de vídeo, rede, armazenamento), instabilidade de hardware ou incompatibilidade após atualizações.",
            Impact = "Reinicializações inesperadas com potencial perda de dados não salvos em aplicações abertas.",
            Recommendation = "Inspecionar os drivers recentemente atualizados, verificar a integridade da memória RAM com o Diagnóstico de Memória do Windows e analisar o relatório detalhado do Crash Analyzer.",
            Evidence = evidence,
            RequiresElevation = false,
            RequiresRestart = false,
            SupportsRollback = false,
            DetectedAt = DateTimeOffset.UtcNow
        };
    }
}

