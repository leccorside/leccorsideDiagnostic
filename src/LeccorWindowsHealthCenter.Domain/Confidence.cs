namespace LeccorWindowsHealthCenter.Domain;

public enum ConfidenceLevel
{
    /// <summary>Possível problema — indício, precisa de investigação.</summary>
    Possible,

    /// <summary>Provável — múltiplos indícios consistentes.</summary>
    Likely,

    /// <summary>Problema confirmado por evidência direta.</summary>
    Confirmed,
}

/// <summary>
/// Confiança de um finding (0–100%). Diferencia "problema confirmado" de "possível problema"
/// (seção 67 do PROMPT).
/// </summary>
public readonly record struct Confidence
{
    public const int ConfirmedThreshold = 90;
    public const int LikelyThreshold = 60;

    public Confidence(int percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(percent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100);
        Percent = percent;
    }

    public int Percent { get; }

    public ConfidenceLevel Level => Percent switch
    {
        >= ConfirmedThreshold => ConfidenceLevel.Confirmed,
        >= LikelyThreshold => ConfidenceLevel.Likely,
        _ => ConfidenceLevel.Possible,
    };

    public bool IsConfirmed => Level == ConfidenceLevel.Confirmed;

    public static Confidence Confirmed => new(100);

    public static Confidence FromPercent(int percent) => new(percent);

    public override string ToString() => $"{Percent}%";
}
