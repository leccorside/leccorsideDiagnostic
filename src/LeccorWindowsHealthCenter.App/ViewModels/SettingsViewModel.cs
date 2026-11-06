using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Application.Scheduling;
using LeccorWindowsHealthCenter.Application.Settings;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Shared;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ThemeService _themeService;
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IScanSchedulerService _schedulerService;

    public SettingsViewModel(
        ThemeService themeService,
        UiPreferences preferences,
        AppPaths paths,
        IPrivilegeService privileges,
        IOptions<RepairOptions> repairOptions,
        IConfigurationRepository configurationRepository,
        IScanSchedulerService schedulerService)
    {
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        Paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _schedulerService = schedulerService ?? throw new ArgumentNullException(nameof(schedulerService));
        ArgumentNullException.ThrowIfNull(privileges);
        ArgumentNullException.ThrowIfNull(repairOptions);

        IsElevated = privileges.IsElevated;
        DryRunEnabled = repairOptions.Value.DryRun;
        _selectedTheme = themeService.Current;

        Preferences.PropertyChanged += async (s, e) =>
        {
            if (e.PropertyName == nameof(UiPreferences.AdvancedMode))
            {
                await _configurationRepository.SetValueAsync("ui.advanced-mode", Preferences.AdvancedMode.ToString(), CancellationToken.None).ConfigureAwait(false);
            }
        };

        _ = InitializeAsync();
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

    public IReadOnlyList<ScheduleOptionViewModel> ScheduleOptions { get; } =
    [
        new(ScheduledScanFrequency.Disabled, "Manualmente (Desativado)"),
        new(ScheduledScanFrequency.Daily, "Diariamente (às 12:00)"),
        new(ScheduledScanFrequency.Weekly, "Semanalmente (Segundas-feiras às 12:00)"),
        new(ScheduledScanFrequency.Monthly, "Mensalmente (Dia 1 às 12:00)"),
    ];

    [ObservableProperty]
    private AppTheme _selectedTheme;

    [ObservableProperty]
    private ScheduledScanFrequency _selectedFrequency = ScheduledScanFrequency.Disabled;

    [ObservableProperty]
    private string _scheduleStatusText = "Consultando agendamento do Windows...";

    [ObservableProperty]
    private string _scheduleNextRunText = string.Empty;

    [ObservableProperty]
    private bool _isSavingSchedule;

    [ObservableProperty]
    private bool _checkRevocationOnline;

    partial void OnCheckRevocationOnlineChanged(bool value)
    {
        _ = _configurationRepository.SetValueAsync("security.check-revocation-online", value.ToString(), CancellationToken.None);
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        _themeService.Apply(value);
        _ = _configurationRepository.SetValueAsync("ui.theme", value.ToString(), CancellationToken.None);
    }

    private async Task InitializeAsync()
    {
        try
        {
            var savedTheme = await _configurationRepository.GetValueAsync("ui.theme", CancellationToken.None).ConfigureAwait(true);
            if (!string.IsNullOrEmpty(savedTheme) && Enum.TryParse<AppTheme>(savedTheme, true, out var theme))
            {
                SelectedTheme = theme;
            }

            var savedAdvMode = await _configurationRepository.GetValueAsync("ui.advanced-mode", CancellationToken.None).ConfigureAwait(true);
            if (!string.IsNullOrEmpty(savedAdvMode) && bool.TryParse(savedAdvMode, out var adv))
            {
                Preferences.AdvancedMode = adv;
            }

            var savedRevocation = await _configurationRepository.GetValueAsync("security.check-revocation-online", CancellationToken.None).ConfigureAwait(true);
            if (!string.IsNullOrEmpty(savedRevocation) && bool.TryParse(savedRevocation, out var cro))
            {
                CheckRevocationOnline = cro;
            }

            await RefreshScheduleStatusAsync().ConfigureAwait(true);
        }
        catch
        {
            // Fallback gracioso
        }
    }

    [RelayCommand]
    public async Task RefreshScheduleStatusAsync()
    {
        var status = await _schedulerService.GetStatusAsync(CancellationToken.None).ConfigureAwait(true);
        SelectedFrequency = status.Frequency;
        ScheduleStatusText = status.Details ?? (status.IsConfigured ? "Agendamento ativo." : "Desativado.");
        ScheduleNextRunText = !string.IsNullOrEmpty(status.NextRun) ? $"Próxima execução: {status.NextRun}" : string.Empty;
    }

    [RelayCommand]
    public async Task SaveScheduleAsync()
    {
        IsSavingSchedule = true;
        try
        {
            await _schedulerService.ConfigureScheduleAsync(SelectedFrequency, CancellationToken.None).ConfigureAwait(true);
            await RefreshScheduleStatusAsync().ConfigureAwait(true);
        }
        finally
        {
            IsSavingSchedule = false;
        }
    }
}

public sealed record ThemeOption(AppTheme Theme, string Label);

public sealed record ScheduleOptionViewModel(ScheduledScanFrequency Frequency, string Label);
