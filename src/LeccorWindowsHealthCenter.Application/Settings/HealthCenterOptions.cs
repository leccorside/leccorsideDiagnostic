namespace LeccorWindowsHealthCenter.Application.Settings;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>Preferências gerais do aplicativo (seção "HealthCenter" do appsettings.json).</summary>
public sealed class HealthCenterOptions
{
    public const string SectionName = "HealthCenter";

    /// <summary>Modo avançado (seção 56): exibe Event IDs, caminhos de registro, comandos, exit codes, stdout/stderr.</summary>
    public bool AdvancedMode { get; set; }

    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>Intervalo de amostragem do dashboard. Mínimo 1s para manter CPU ociosa &lt; 1% (seção 58).</summary>
    public TimeSpan DashboardRefreshInterval { get; set; } = TimeSpan.FromSeconds(2);
}

/// <summary>Configuração de logs (seção 42).</summary>
public sealed class LoggingFileOptions
{
    public const string SectionName = "FileLogging";

    public long FileSizeLimitBytes { get; set; } = 10 * 1024 * 1024;

    public int RetainedFileCount { get; set; } = 14;

    public string MinimumLevel { get; set; } = "Information";
}
