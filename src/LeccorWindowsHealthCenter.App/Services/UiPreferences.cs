using CommunityToolkit.Mvvm.ComponentModel;
using LeccorWindowsHealthCenter.Application.Settings;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.App.Services;

/// <summary>Preferências de interface da sessão (inicializadas do appsettings.json).</summary>
public sealed partial class UiPreferences : ObservableObject
{
    public UiPreferences(IOptions<HealthCenterOptions> options)
    {
        _advancedMode = options.Value.AdvancedMode;
    }

    /// <summary>Modo avançado (seção 56): exibe Event IDs, caminhos de registro, comandos e métricas detalhadas.</summary>
    [ObservableProperty]
    private bool _advancedMode;
}
