using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Security;
using LeccorWindowsHealthCenter.Application.WindowsHealth;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

/// <summary>
/// Tela de Integridade do Windows (seção 22 do PROMPT): DISM, SFC e CHKDSK.
/// </summary>
public sealed partial class WindowsViewModel : ObservableObject, IPageLifecycle
{
    private readonly IWindowsIntegrityProvider _provider;
    private readonly ILogger<WindowsViewModel> _logger;
    private CancellationTokenSource? _cts;

    public WindowsViewModel(IWindowsIntegrityProvider provider, UiPreferences preferences, ILogger<WindowsViewModel> logger)
    {
        _provider = provider;
        Preferences = preferences;
        _logger = logger;
    }

    public UiPreferences Preferences { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QuickCheckCommand), nameof(FullCheckCommand), nameof(CancelCommand))]
    private bool _isChecking;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasRun;

    [ObservableProperty]
    private bool _hasIssues;

    [ObservableProperty]
    private string _overallAlertTitle = string.Empty;

    [ObservableProperty]
    private string _overallAlertMessage = string.Empty;

    [ObservableProperty]
    private string _overallAlertTone = "Ok";

    [ObservableProperty]
    private string _componentStoreTitle = "Repositório de Componentes (DISM)";

    [ObservableProperty]
    private string _componentStoreStatus = "Não verificado";

    [ObservableProperty]
    private string _componentStoreMessage = "Execute a verificação para avaliar a integridade do repositório de pacotes e imagens do Windows.";

    [ObservableProperty]
    private string _componentStoreCommand = "dism.exe /Online /Cleanup-Image /CheckHealth";

    [ObservableProperty]
    private string _componentStoreOutput = string.Empty;

    [ObservableProperty]
    private string _componentStoreTone = "Neutral";

    [ObservableProperty]
    private string _systemFilesTitle = "Arquivos do Sistema (SFC)";

    [ObservableProperty]
    private string _systemFilesStatus = "Não verificado";

    [ObservableProperty]
    private string _systemFilesMessage = "Analisa a integridade de todas as bibliotecas (.dll) e arquivos essenciais protegidos do Windows.";

    [ObservableProperty]
    private string _systemFilesCommand = "sfc.exe /VERIFYONLY";

    [ObservableProperty]
    private string _systemFilesOutput = string.Empty;

    [ObservableProperty]
    private string _systemFilesTone = "Neutral";

    [ObservableProperty]
    private string _fileSystemTitle = "Sistema de Arquivos (CHKDSK C:)";

    [ObservableProperty]
    private string _fileSystemStatus = "Não verificado";

    [ObservableProperty]
    private string _fileSystemMessage = "Verifica estruturas de diretório, metadados NTFS e integridade do volume principal.";

    [ObservableProperty]
    private string _fileSystemCommand = "chkdsk.exe C:";

    [ObservableProperty]
    private string _fileSystemOutput = string.Empty;

    [ObservableProperty]
    private string _fileSystemTone = "Neutral";

    [ObservableProperty]
    private string _verifiedAtText = string.Empty;

    public void Activate()
    {
        // Se ainda não rodou, pode manter estado inicial ou aguardar ação do usuário
    }

    public void Deactivate()
    {
    }

    private bool CanCheck() => !IsChecking;

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task QuickCheckAsync()
    {
        await RunCheckAsync(deepScan: false).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task FullCheckAsync()
    {
        await RunCheckAsync(deepScan: true).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(IsChecking))]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    private async Task RunCheckAsync(bool deepScan)
    {
        IsChecking = true;
        StatusMessage = deepScan ? "Executando verificação profunda do Windows (DISM, SFC e CHKDSK)..." : "Executando verificação rápida do Windows (DISM e CHKDSK)...";
        using var cts = new CancellationTokenSource();
        _cts = cts;

        try
        {
            StatusMessage = deepScan ? "1/3: Analisando Component Store via DISM /ScanHealth..." : "1/2: Verificando Component Store via DISM /CheckHealth...";
            var componentCheck = await _provider.CheckComponentStoreAsync(deepScan, cts.Token).ConfigureAwait(true);

            UpdateComponentStoreDisplay(componentCheck);

            SystemFilesCheck? systemFilesCheck = null;
            if (deepScan)
            {
                StatusMessage = "2/3: Verificando arquivos protegidos via SFC /VERIFYONLY...";
                systemFilesCheck = await _provider.CheckSystemFilesAsync(cts.Token).ConfigureAwait(true);
                UpdateSystemFilesDisplay(systemFilesCheck);
            }

            StatusMessage = deepScan ? "3/3: Verificando integridade do volume C: via CHKDSK..." : "2/2: Verificando volume C: via CHKDSK...";
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:";
            var fileSystemCheck = await _provider.CheckFileSystemAsync(systemDrive, cts.Token).ConfigureAwait(true);
            UpdateFileSystemDisplay(fileSystemCheck);

            HasRun = true;
            VerifiedAtText = $"Última verificação: {DateTime.Now:dd/MM/yyyy HH:mm}";

            EvaluateOverallState(componentCheck, systemFilesCheck, fileSystemCheck);
        }
        catch (ElevationCancelledException)
        {
            StatusMessage = "A verificação foi cancelada porque a elevação de administrador foi recusada no UAC.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Verificação cancelada pelo usuário.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante a verificação de integridade do Windows");
            StatusMessage = $"Falha na verificação: {ex.Message}";
        }
        finally
        {
            _cts = null;
            IsChecking = false;
        }
    }

    private void UpdateComponentStoreDisplay(ComponentStoreCheck check)
    {
        ComponentStoreStatus = check.Status switch
        {
            Application.WindowsHealth.ComponentStoreStatus.Healthy => "Íntegro",
            Application.WindowsHealth.ComponentStoreStatus.Repairable => "Danificado (Reparável)",
            Application.WindowsHealth.ComponentStoreStatus.Corrupted => "Corrompido",
            _ => "Falha na verificação",
        };
        ComponentStoreTone = check.Status switch
        {
            Application.WindowsHealth.ComponentStoreStatus.Healthy => "Ok",
            Application.WindowsHealth.ComponentStoreStatus.Repairable => "Warning",
            Application.WindowsHealth.ComponentStoreStatus.Corrupted => "Critical",
            _ => "Neutral",
        };
        ComponentStoreMessage = check.Message;
        ComponentStoreCommand = check.CommandResult?.CommandLine ?? "dism.exe /Online /Cleanup-Image /CheckHealth";
        ComponentStoreOutput = check.CommandResult?.StandardOutput ?? string.Empty;
    }

    private void UpdateSystemFilesDisplay(SystemFilesCheck check)
    {
        SystemFilesStatus = check.Status switch
        {
            Application.WindowsHealth.SystemFilesStatus.IntegrityOk => "Íntegro",
            Application.WindowsHealth.SystemFilesStatus.CorruptedRepaired => "Reparado",
            Application.WindowsHealth.SystemFilesStatus.CorruptedCannotRepair => "Corrompido",
            _ => "Falha na verificação",
        };
        SystemFilesTone = check.Status switch
        {
            Application.WindowsHealth.SystemFilesStatus.IntegrityOk => "Ok",
            Application.WindowsHealth.SystemFilesStatus.CorruptedRepaired => "Warning",
            Application.WindowsHealth.SystemFilesStatus.CorruptedCannotRepair => "Critical",
            _ => "Neutral",
        };
        SystemFilesMessage = check.Message;
        SystemFilesCommand = check.CommandResult?.CommandLine ?? "sfc.exe /VERIFYONLY";
        SystemFilesOutput = check.CommandResult?.StandardOutput ?? string.Empty;
    }

    private void UpdateFileSystemDisplay(FileSystemCheck check)
    {
        FileSystemStatus = check.Status switch
        {
            Application.WindowsHealth.FileSystemStatus.VolumeOk => "Íntegro",
            Application.WindowsHealth.FileSystemStatus.ErrorsFound => "Erros detectados",
            _ => "Falha na verificação",
        };
        FileSystemTone = check.Status switch
        {
            Application.WindowsHealth.FileSystemStatus.VolumeOk => "Ok",
            Application.WindowsHealth.FileSystemStatus.ErrorsFound => "Critical",
            _ => "Neutral",
        };
        FileSystemMessage = check.Message;
        FileSystemCommand = check.CommandResult?.CommandLine ?? $"chkdsk.exe {check.Drive}";
        FileSystemOutput = check.CommandResult?.StandardOutput ?? string.Empty;
    }

    private void EvaluateOverallState(ComponentStoreCheck comp, SystemFilesCheck? sfc, FileSystemCheck chk)
    {
        var hasCritical = comp.Status == Application.WindowsHealth.ComponentStoreStatus.Corrupted ||
                          sfc?.Status == Application.WindowsHealth.SystemFilesStatus.CorruptedCannotRepair ||
                          chk.Status == Application.WindowsHealth.FileSystemStatus.ErrorsFound;

        var hasWarning = comp.Status == Application.WindowsHealth.ComponentStoreStatus.Repairable ||
                         sfc?.Status == Application.WindowsHealth.SystemFilesStatus.CorruptedRepaired;

        HasIssues = hasCritical || hasWarning;

        if (hasCritical)
        {
            OverallAlertTone = "Critical";
            OverallAlertTitle = "Problemas críticos de integridade detectados";
            OverallAlertMessage = "O Windows identificou arquivos corrompidos ou inconsistências no sistema de arquivos. É recomendável realizar backup e proceder com os reparos oficiais.";
        }
        else if (hasWarning)
        {
            OverallAlertTone = "Warning";
            OverallAlertTitle = "Reparos preventivos recomendados";
            OverallAlertMessage = "Foram identificados pacotes que necessitam de recuperação no repositório de componentes do Windows.";
        }
        else
        {
            OverallAlertTone = "Ok";
            OverallAlertTitle = "Todos os subsistemas do Windows estão íntegros";
            OverallAlertMessage = "Component Store, arquivos essenciais do sistema e volume NTFS foram validados e não apresentaram inconsistências.";
        }
    }
}
