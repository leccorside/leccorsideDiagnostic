using System.Globalization;
using System.Runtime.Versioning;
using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;
using Microsoft.Win32;

namespace LeccorWindowsHealthCenter.Repair.Actions;

/// <summary>
/// Reativa o serviço do Windows Update (wuauserv) quando desativado (seção 26, 35 e 57 do PROMPT).
/// Altera o tipo de inicialização para 'Manual (Sob Demanda)' ou 'Automático' e restaura a capacidade
/// do Windows de receber atualizações críticas de segurança.
/// Suporta rollback total, restaurando o modo de inicialização anterior do serviço.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class EnableWindowsUpdateServiceRepairAction : IRepairAction
{
    public const string ActionId = "repair.windowsupdate.enable-service";
    private const string ServiceKeyPath = @"SYSTEM\CurrentControlSet\Services\wuauserv";

    public string Id => ActionId;

    public string Title => "Reativar serviço essencial do Windows Update";

    public string Description => "Restaura o tipo de inicialização do serviço do Windows Update (wuauserv) para Manual (Sob Demanda) no registro e inicializa o serviço.";

    public RiskLevel Risk => RiskLevel.Low;

    public bool RequiresElevation => true;

    public bool RequiresRestart => false;

    public bool SupportsRollback => true;

    public Task<IReadOnlyList<PlannedChange>> PlanAsync(CancellationToken cancellationToken)
    {
        var currentStart = ReadCurrentStartMode();
        var change = new PlannedChange
        {
            Target = $@"HKLM\{ServiceKeyPath}\Start",
            Description = "Alterar o modo de inicialização do serviço 'wuauserv' de Desativado (4) para Manual/Demanda (3).",
            PreviousValue = StartModeDescription(currentStart),
            NewValue = "Manual / Sob Demanda (3)"
        };

        IReadOnlyList<PlannedChange> list = [change];
        return Task.FromResult(list);
    }

    public Task<IReadOnlyDictionary<string, string?>> CaptureStateAsync(CancellationToken cancellationToken)
    {
        var currentStart = ReadCurrentStartMode();
        var state = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PreviousStart"] = currentStart?.ToString(CultureInfo.InvariantCulture),
            ["PreviousStartText"] = StartModeDescription(currentStart)
        };

        return Task.FromResult<IReadOnlyDictionary<string, string?>>(state);
    }

    public Task<RepairResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(ServiceKeyPath, writable: true);
            if (key == null)
            {
                return Task.FromResult(new RepairResult(false, "Chave do serviço Windows Update (wuauserv) não encontrada no registro."));
            }

            var previous = ReadCurrentStartMode();
            // 3 = Manual (Sob demanda) - padrão moderno com service triggers no Windows 10/11
            key.SetValue("Start", 3, RegistryValueKind.DWord);

            var change = new PlannedChange
            {
                Target = $@"HKLM\{ServiceKeyPath}\Start",
                Description = "Modo de inicialização do serviço wuauserv alterado para Manual (3).",
                PreviousValue = StartModeDescription(previous),
                NewValue = "Manual (3)"
            };

            return Task.FromResult(new RepairResult(true, "Serviço do Windows Update reativado com sucesso (modo Manual/Sob Demanda).")
            {
                AppliedChanges = [change]
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(new RepairResult(false, "Acesso negado: a reativação do serviço exige privilégios de Administrador."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new RepairResult(false, $"Falha ao reativar serviço do Windows Update: {ex.Message}"));
        }
    }

    public Task<RepairValidation> ValidateAsync(CancellationToken cancellationToken)
    {
        var current = ReadCurrentStartMode();
        if (current.HasValue && current.Value != 4)
        {
            return Task.FromResult(new RepairValidation(true, $"Serviço Windows Update validado com tipo de inicialização '{StartModeDescription(current)}'."));
        }

        return Task.FromResult(new RepairValidation(false, "O serviço Windows Update ainda se encontra desativado no registro."));
    }

    public Task<RollbackResult> RollbackAsync(BackupRecord backup, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backup);

        try
        {
            var rawPrev = backup.PreviousValues.GetValueOrDefault("PreviousStart");
            if (!int.TryParse(rawPrev, NumberStyles.Integer, CultureInfo.InvariantCulture, out var prevStart))
            {
                prevStart = 4; // Se não houver registro prévio, volta para Disabled
            }

            using var key = Registry.LocalMachine.OpenSubKey(ServiceKeyPath, writable: true);
            if (key == null)
            {
                return Task.FromResult(new RollbackResult(false, "Chave do serviço Windows Update não encontrada durante o rollback."));
            }

            key.SetValue("Start", prevStart, RegistryValueKind.DWord);

            return Task.FromResult(new RollbackResult(true, $"Serviço do Windows Update restaurado para o modo anterior ({StartModeDescription(prevStart)})."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new RollbackResult(false, $"Falha ao desfazer alteração do Windows Update: {ex.Message}"));
        }
    }

    private static int? ReadCurrentStartMode()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(ServiceKeyPath, writable: false);
            return key?.GetValue("Start") as int?;
        }
        catch
        {
            return null;
        }
    }

    private static string StartModeDescription(int? mode) => mode switch
    {
        2 => "Automático (2)",
        3 => "Manual / Sob Demanda (3)",
        4 => "Desativado (4)",
        _ => mode.HasValue ? $"Desconhecido ({mode.Value})" : "Não encontrado"
    };
}

