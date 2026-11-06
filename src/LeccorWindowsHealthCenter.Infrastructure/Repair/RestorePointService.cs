using System.Management;
using System.Runtime.Versioning;
using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LeccorWindowsHealthCenter.Infrastructure.Repair;

/// <summary>
/// Criação de Ponto de Restauração nativo do Windows (seção 38 do PROMPT).
/// Utiliza a API WMI (root\default:SystemRestore).
/// Se a Proteção do Sistema estiver desativada no volume C: ou o processo não for elevado,
/// fornece diagnóstico claro sem falhar a aplicação.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RestorePointService : IRestorePointService
{
    private const string SystemRestoreScope = @"\\.\root\default";
    private const string SystemRestoreClass = "SystemRestore";
    private const int ModifySettingsType = 12; // MODIFY_SETTINGS
    private const int BeginSystemChange = 100; // BEGIN_SYSTEM_CHANGE

    private readonly IPrivilegeService _privilegeService;
    private readonly ILogger<RestorePointService> _logger;

    public RestorePointService(IPrivilegeService privilegeService, ILogger<RestorePointService> logger)
    {
        _privilegeService = privilegeService;
        _logger = logger;
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                // Verifica no registro se o System Restore está desativado globalmente
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore");
                if (key != null)
                {
                    var disableSr = key.GetValue("DisableSR");
                    if (disableSr is int intVal && intVal == 1)
                    {
                        return false;
                    }
                }

                // Verifica se a classe WMI responde
                var scope = new ManagementScope(SystemRestoreScope);
                scope.Connect();
                using var sysRestoreClass = new ManagementClass(scope, new ManagementPath(SystemRestoreClass), null);
                return sysRestoreClass.Methods["CreateRestorePoint"] != null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SystemRestore WMI não disponível");
                return false;
            }
        }, cancellationToken);
    }

    public Task<RestorePointResult> CreateRestorePointAsync(string description, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return Task.Run(() =>
        {
            if (!_privilegeService.IsElevated)
            {
                const string elevationMsg = "A criação de Ponto de Restauração nativo do Windows exige privilégios de Administrador.";
                _logger.LogWarning(elevationMsg);
                return new RestorePointResult(false, elevationMsg);
            }

            try
            {
                var scope = new ManagementScope(SystemRestoreScope);
                scope.Connect();

                using var sysRestoreClass = new ManagementClass(scope, new ManagementPath(SystemRestoreClass), null);
                using var inParams = sysRestoreClass.GetMethodParameters("CreateRestorePoint");

                inParams["Description"] = description;
                inParams["RestorePointType"] = ModifySettingsType;
                inParams["EventType"] = BeginSystemChange;

                using var outParams = sysRestoreClass.InvokeMethod("CreateRestorePoint", inParams, null);
                if (outParams == null)
                {
                    return new RestorePointResult(false, "Falha ao invocar WMI SystemRestore.CreateRestorePoint: retorno nulo.");
                }

                var returnValue = Convert.ToUInt32(outParams["ReturnValue"], System.Globalization.CultureInfo.InvariantCulture);
                if (returnValue == 0)
                {
                    _logger.LogInformation("Ponto de restauração criado com sucesso: \"{Description}\"", description);
                    return new RestorePointResult(true, $"Ponto de restauração \"{description}\" criado com sucesso.");
                }

                var errorMessage = returnValue switch
                {
                    1058 => "O serviço de Restauração do Sistema (ou VSS) está desativado.",
                    1053 => "Tempo limite esgotado ao aguardar o serviço de Restauração do Sistema.",
                    _ => $"Código de retorno da Restauração do Sistema: {returnValue}."
                };

                _logger.LogWarning("Falha ao criar ponto de restauração. Código: {ReturnValue}", returnValue);
                return new RestorePointResult(false, errorMessage);
            }
            catch (ManagementException mex)
            {
                _logger.LogError(mex, "Falha de WMI ao criar ponto de restauração");
                return new RestorePointResult(false,
                    "A Proteção do Sistema (Restauração do Sistema) parece estar desativada para a unidade C: ou o serviço VSS não está em execução.");
            }
            catch (UnauthorizedAccessException uex)
            {
                _logger.LogError(uex, "Acesso negado ao criar ponto de restauração");
                return new RestorePointResult(false, "Acesso negado. Execute o aplicativo como Administrador para criar Pontos de Restauração.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao criar ponto de restauração");
                return new RestorePointResult(false, $"Erro ao criar ponto de restauração: {ex.Message}");
            }
        }, cancellationToken);
    }
}

