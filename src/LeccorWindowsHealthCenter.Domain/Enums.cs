namespace LeccorWindowsHealthCenter.Domain;

/// <summary>Gravidade do problema encontrado (o que aconteceu).</summary>
public enum Severity
{
    /// <summary>Informação/recomendação. Nunca reduz o score.</summary>
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

/// <summary>Risco de executar uma correção (o que pode dar errado ao corrigir).</summary>
public enum RiskLevel
{
    /// <summary>Pode ser executada automaticamente (ex.: cache temporário comprovadamente descartável).</summary>
    Safe = 0,

    /// <summary>Baixo risco e facilmente reversível.</summary>
    Low = 1,

    /// <summary>Pode alterar comportamento do sistema. Exige confirmação.</summary>
    Medium = 2,

    /// <summary>Pode afetar boot, drivers, serviços, rede ou aplicações. Exige confirmação explícita.</summary>
    High = 3,

    /// <summary>Partições, corrupção, perda de dados, hardware falhando. Nunca executar automaticamente.</summary>
    Critical = 4,
}

/// <summary>Área funcional onde o finding foi detectado.</summary>
public enum FindingCategory
{
    Hardware,
    Cpu,
    Memory,
    Gpu,
    Disk,
    Storage,
    Windows,
    WindowsUpdate,
    Explorer,
    Processes,
    Startup,
    Services,
    Drivers,
    Applications,
    Events,
    Network,
    Security,
    Development,
}

/// <summary>Categorias que compõem o score de saúde (seção 46 do PROMPT).</summary>
public enum ScoreCategory
{
    Hardware,
    Windows,
    Performance,
    Storage,
    Stability,
    Security,
}

public enum FindingStatus
{
    Open,
    Acknowledged,
    Ignored,
    Repaired,
    RolledBack,
}

/// <summary>Origem técnica de uma evidência.</summary>
public enum EvidenceSource
{
    Measurement,
    PerformanceCounter,
    EventLog,
    Registry,
    Wmi,
    Command,
    FileSystem,
    Process,
    WindowsApi,
    Configuration,
}

public enum ScanMode
{
    Quick,
    Full,
    Custom,
}

/// <summary>Módulos selecionáveis na varredura personalizada (seção 9 do PROMPT).</summary>
public enum ScanModule
{
    Hardware,
    Performance,
    Disks,
    Windows,
    Explorer,
    Processes,
    Startup,
    Services,
    Drivers,
    Applications,
    Events,
    WindowsUpdate,
    Network,
    Security,
    Development,
    Storage,
}

public enum ScannerRunStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Skipped,
}

public enum HealthRating
{
    /// <summary>Não há dados suficientes para calcular o score (nenhum scanner relevante executou).</summary>
    Unknown,
    Critical,
    Attention,
    Good,
    Excellent,
}
