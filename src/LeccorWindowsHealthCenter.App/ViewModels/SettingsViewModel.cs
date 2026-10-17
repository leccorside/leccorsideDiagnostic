using CommunityToolkit.Mvvm.ComponentModel;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Application.Settings;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Shared;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ThemeService _themeService;

    public SettingsViewModel(ThemeService themeService, UiPreferences preferences, AppPaths paths, IPrivilegeService privileges, IOptions<RepairOptions> repairOptions)
    {
        _themeService = themeService;
        Preferences = preferences;
        Paths = paths;
        IsElevated = privileges.IsElevated;
        DryRunEnabled = repairOptions.Value.DryRun;
        _selectedTheme = themeService.Current;
    }

    public UiPreferences Preferences { get; }

    public AppPaths Paths { get; }

    public bool IsElevated { get; }

    public string PrivilegeText => IsElevated
        ? "O aplicativo está executando como administrador. Recomenda-se executar sem elevação."
        : "Executando sem privilégios de administrador (recomendado). A elevação será solicitada apenas para operações específicas.";

    public bool DryRunEnabled { get; }

    public string DryRunText => DryRunEnabled ? "Ativo" : "Desativado";

    public string Version { get; } = typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public IReadOnlyList<ThemeOption> Themes { get; } =
    [
        new(AppTheme.System, "Seguir o Windows"),
        new(AppTheme.Light, "Claro"),
        new(AppTheme.Dark, "Escuro"),
    ];

    [ObservableProperty]
    private AppTheme _selectedTheme;

    partial void OnSelectedThemeChanged(AppTheme value) => _themeService.Apply(value);
}

public sealed record ThemeOption(AppTheme Theme, string Label);
