using System.Runtime.Versioning;
using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;
using Microsoft.Win32;

namespace LeccorWindowsHealthCenter.Repair.Actions;

/// <summary>
/// Bloqueia uma extensão problemática ou órfã do Windows Explorer (seção 16 e 35 do PROMPT).
/// Utiliza a chave nativa do Windows: HKCU\Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked.
/// O Windows Explorer ignora extensões listadas nessa chave, prevenindo lentidão no menu de contexto
/// sem a necessidade de excluir DLLs ou chaves COM protegidas.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DisableShellExtensionRepairAction : IRepairAction
{
    public const string ActionId = "repair.explorer.disable-shell-extension";
    private const string BlockedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";

    private string _clsid = "{00000000-0000-0000-0000-000000000000}";
    private string _extensionName = "Extensão de Exemplo";

    public string Id => ActionId;

    public string Title => "Bloquear extensão do Windows Explorer";

    public string Description => "Bloqueia a extensão de Shell adicionando seu identificador CLSID à chave oficial Shell Extensions\\Blocked, eliminando travamentos no menu de contexto.";

    public RiskLevel Risk => RiskLevel.Low;

    public bool RequiresElevation => false;

    public bool RequiresRestart => false;

    public bool SupportsRollback => true;

    public string Clsid
    {
        get => _clsid;
        set => _clsid = string.IsNullOrWhiteSpace(value) ? "{00000000-0000-0000-0000-000000000000}" : value.Trim();
    }

    public string ExtensionName
    {
        get => _extensionName;
        set => _extensionName = string.IsNullOrWhiteSpace(value) ? "Extensão de Exemplo" : value.Trim();
    }

    public DisableShellExtensionRepairAction()
    {
    }

    public DisableShellExtensionRepairAction(string clsid, string extensionName)
    {
        Clsid = clsid;
        ExtensionName = extensionName;
    }

    public Task<IReadOnlyList<PlannedChange>> PlanAsync(CancellationToken cancellationToken)
    {
        var target = $@"HKCU\{BlockedKeyPath}\{_clsid}";
        var change = new PlannedChange
        {
            Target = target,
            Description = $"Adicionar '{_clsid}' ({_extensionName}) à lista de bloqueio do Windows Explorer.",
            PreviousValue = IsBlocked() ? "Bloqueado" : "(não bloqueado)",
            NewValue = $"Bloqueado ({_extensionName})"
        };

        IReadOnlyList<PlannedChange> list = [change];
        return Task.FromResult(list);
    }

    public Task<IReadOnlyDictionary<string, string?>> CaptureStateAsync(CancellationToken cancellationToken)
    {
        var wasBlocked = IsBlocked();
        var state = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Clsid"] = _clsid,
            ["ExtensionName"] = _extensionName,
            ["WasBlocked"] = wasBlocked.ToString()
        };

        return Task.FromResult<IReadOnlyDictionary<string, string?>>(state);
    }

    public Task<RepairResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(BlockedKeyPath, writable: true);
            if (key == null)
            {
                return Task.FromResult(new RepairResult(false, $"Não foi possível criar ou abrir a chave '{BlockedKeyPath}'."));
            }

            key.SetValue(_clsid, _extensionName, RegistryValueKind.String);

            var change = new PlannedChange
            {
                Target = $@"HKCU\{BlockedKeyPath}\{_clsid}",
                Description = $"Extensão '{_extensionName}' ({_clsid}) bloqueada no Windows Explorer.",
                PreviousValue = "(não bloqueado)",
                NewValue = _extensionName
            };

            return Task.FromResult(new RepairResult(true, $"Extensão de Shell '{_extensionName}' bloqueada com sucesso.")
            {
                AppliedChanges = [change]
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new RepairResult(false, $"Falha ao bloquear extensão do Explorer: {ex.Message}"));
        }
    }

    public Task<RepairValidation> ValidateAsync(CancellationToken cancellationToken)
    {
        if (IsBlocked())
        {
            return Task.FromResult(new RepairValidation(true, $"Extensão '{_extensionName}' confirmada como bloqueada no registro."));
        }

        return Task.FromResult(new RepairValidation(false, $"Extensão '{_clsid}' não encontrada na lista de bloqueados."));
    }

    public Task<RollbackResult> RollbackAsync(BackupRecord backup, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backup);

        try
        {
            var clsid = backup.PreviousValues.GetValueOrDefault("Clsid") ?? _clsid;
            var name = backup.PreviousValues.GetValueOrDefault("ExtensionName") ?? _extensionName;
            var wasBlocked = bool.TryParse(backup.PreviousValues.GetValueOrDefault("WasBlocked"), out var blocked) && blocked;

            using var key = Registry.CurrentUser.OpenSubKey(BlockedKeyPath, writable: true);
            if (key != null)
            {
                if (wasBlocked)
                {
                    key.SetValue(clsid, name, RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(clsid, throwOnMissingValue: false);
                }
            }

            return Task.FromResult(new RollbackResult(true, $"Bloqueio da extensão '{name}' desfeito com sucesso."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new RollbackResult(false, $"Falha ao desfazer bloqueio de extensão: {ex.Message}"));
        }
    }

    private bool IsBlocked()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(BlockedKeyPath, writable: false);
            return key?.GetValue(_clsid) != null;
        }
        catch
        {
            return false;
        }
    }
}

