namespace LeccorWindowsHealthCenter.Domain.Repairs;

/// <summary>Alteração concreta que uma ação de reparo fará (ou fez). Usada no Dry Run, no backup e no histórico.</summary>
public sealed record PlannedChange
{
    /// <summary>Alvo técnico, ex.: "HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Discord".</summary>
    public required string Target { get; init; }

    public required string Description { get; init; }

    public string? PreviousValue { get; init; }

    public string? NewValue { get; init; }
}

public enum RepairOutcome
{
    Succeeded,
    Failed,
    DryRun,
    Blocked,
    Cancelled,
    ValidationFailed,
    RolledBack,
}

/// <summary>Nível de aprovação dado pelo usuário para uma ação.</summary>
public enum UserApproval
{
    None,

    /// <summary>Usuário confirmou (suficiente para MEDIUM).</summary>
    Confirmed,

    /// <summary>Usuário confirmou explicitamente após ver riscos e alterações (necessário para HIGH).</summary>
    ExplicitlyConfirmed,
}

public sealed record RepairResult(bool Success, string Message)
{
    public IReadOnlyList<PlannedChange> AppliedChanges { get; init; } = [];
}

public sealed record RollbackResult(bool Success, string Message);

/// <summary>Medição antes/depois (seção 39). Melhoria só é afirmada com as duas medições.</summary>
public sealed record MeasurementComparison
{
    public required string Name { get; init; }

    public required string Unit { get; init; }

    public required double Before { get; init; }

    public required double After { get; init; }

    /// <summary>True quando valores menores são melhores (tempo, uso de CPU...).</summary>
    public bool LowerIsBetter { get; init; } = true;

    /// <summary>Melhoria percentual (positiva = melhorou). Null quando a medição "antes" é zero.</summary>
    public double? ImprovementPercent
    {
        get
        {
            if (Before == 0)
            {
                return null;
            }

            var delta = LowerIsBetter ? Before - After : After - Before;
            return Math.Round(delta / Math.Abs(Before) * 100, 1);
        }
    }
}

public sealed record RepairValidation(bool Passed, string Message)
{
    public MeasurementComparison? Comparison { get; init; }
}

/// <summary>Estado salvo pelo Backup Engine antes de uma alteração (seção 36).</summary>
public sealed record BackupRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string ActionId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required string User { get; init; }

    public required IReadOnlyDictionary<string, string?> PreviousValues { get; init; }

    public IReadOnlyDictionary<string, string?> NewValues { get; init; } = new Dictionary<string, string?>();

    public string? Result { get; init; }
}

/// <summary>Entrada do Histórico de Correções (seção 37).</summary>
public sealed record RepairRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string ActionId { get; init; }

    public required string ActionTitle { get; init; }

    public Guid? FindingId { get; init; }

    public required RiskLevel Risk { get; init; }

    public required RepairOutcome Outcome { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public required string User { get; init; }

    /// <summary>Motivo (normalmente o título do finding que originou a correção).</summary>
    public string? Reason { get; init; }

    public required string Message { get; init; }

    public Guid? BackupId { get; init; }

    public bool SupportsRollback { get; init; }

    public IReadOnlyList<PlannedChange> Changes { get; init; } = [];

    public MeasurementComparison? Comparison { get; init; }

    public DateTimeOffset? RolledBackAt { get; init; }

    public bool CanRollback => SupportsRollback && Outcome == RepairOutcome.Succeeded && RolledBackAt is null;
}
