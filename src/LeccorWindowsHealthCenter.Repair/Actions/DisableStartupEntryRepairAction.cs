using System.Runtime.Versioning;
using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;
using Microsoft.Win32;

namespace LeccorWindowsHealthCenter.Repair.Actions;

/// <summary>
/// Desativa entradas de inicialização automática do Windows (seção 19 e 35 do PROMPT)
/// utilizando o mecanismo nativo do Explorer: StartupApproved\Run.
/// Em vez de deletar o comando da chave Run, desativa-o de forma idêntica ao Gerenciador de Tarefas do Windows,
/// permitindo reversão segura e rastreável.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DisableStartupEntryRepairAction : IRepairAction
{
    public const string ActionId = "repair.startup.disable";
    private const string ApprovedRunKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private string _entryName = "ExemploStartup";
    private bool _isCurrentUser = true;

    public string Id => ActionId;

    public string Title => "Desativar entrada de inicialização";

    public string Description => "Desativa a entrada de inicialização automática no registro através do mecanismo padrão do Windows (StartupApproved), mantendo a chave original preservada.";

    public RiskLevel Risk => RiskLevel.Low;

    public bool RequiresElevation => !_isCurrentUser;

    public bool RequiresRestart => false;

    public bool SupportsRollback => true;

    public string EntryName
    {
        get => _entryName;
        set => _entryName = string.IsNullOrWhiteSpace(value) ? "ExemploStartup" : value.Trim();
    }

    public bool IsCurrentUser
    {
        get => _isCurrentUser;
        set => _isCurrentUser = value;
    }

    public DisableStartupEntryRepairAction()
    {
    }

    public DisableStartupEntryRepairAction(string entryName, bool isCurrentUser = true)
    {
        EntryName = entryName;
        IsCurrentUser = isCurrentUser;
    }

    public Task<IReadOnlyList<PlannedChange>> PlanAsync(CancellationToken cancellationToken)
    {
        var hiveName = _isCurrentUser ? "HKCU" : "HKLM";
        var target = $@"{hiveName}\{ApprovedRunKey}\{_entryName}";

        var change = new PlannedChange
        {
            Target = target,
            Description = $"Desativar a entrada de inicialização automática '{_entryName}' definindo sinalizador de desativação (0x03) em StartupApproved\\Run.",
            PreviousValue = GetCurrentStatusText(),
            NewValue = "Desativado (0x03)"
        };

        IReadOnlyList<PlannedChange> list = [change];
        return Task.FromResult(list);
    }

    public Task<IReadOnlyDictionary<string, string?>> CaptureStateAsync(CancellationToken cancellationToken)
    {
        var hive = _isCurrentUser ? Registry.CurrentUser : Registry.LocalMachine;
        using var key = hive.OpenSubKey(ApprovedRunKey, writable: false);
        var existingBytes = key?.GetValue(_entryName) as byte[];

        var state = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["EntryName"] = _entryName,
            ["IsCurrentUser"] = _isCurrentUser.ToString(),
            ["PreviousBytes"] = existingBytes is not null ? Convert.ToBase64String(existingBytes) : null,
            ["HadExistingValue"] = (existingBytes is not null).ToString()
        };

        return Task.FromResult<IReadOnlyDictionary<string, string?>>(state);
    }

    public Task<RepairResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var hive = _isCurrentUser ? Registry.CurrentUser : Registry.LocalMachine;
            using var key = hive.CreateSubKey(ApprovedRunKey, writable: true);
            if (key == null)
            {
                return Task.FromResult(new RepairResult(false, $"Não foi possível abrir a chave de registro '{ApprovedRunKey}'."));
            }

            // Padrão do Windows: 12 bytes. Primeiro byte com bit 0 ligado (ex: 0x03) indica desativado.
            var existing = key.GetValue(_entryName) as byte[];
            byte[] disabledBytes;
            if (existing != null && existing.Length >= 1)
            {
                disabledBytes = (byte[])existing.Clone();
                disabledBytes[0] = (byte)(disabledBytes[0] | 0x01); // Garante ímpar
            }
            else
            {
                disabledBytes = [0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
            }

            key.SetValue(_entryName, disabledBytes, RegistryValueKind.Binary);

            var change = new PlannedChange
            {
                Target = $@"{(_isCurrentUser ? "HKCU" : "HKLM")}\{ApprovedRunKey}\{_entryName}",
                Description = $"Entrada '{_entryName}' desativada em StartupApproved\\Run.",
                PreviousValue = existing != null ? Convert.ToHexString(existing) : "(não configurado)",
                NewValue = Convert.ToHexString(disabledBytes)
            };

            return Task.FromResult(new RepairResult(true, $"Entrada de inicialização '{_entryName}' desativada com sucesso.")
            {
                AppliedChanges = [change]
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new RepairResult(false, $"Falha ao desativar entrada de inicialização: {ex.Message}"));
        }
    }

    public Task<RepairValidation> ValidateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var hive = _isCurrentUser ? Registry.CurrentUser : Registry.LocalMachine;
            using var key = hive.OpenSubKey(ApprovedRunKey, writable: false);
            var value = key?.GetValue(_entryName) as byte[];

            if (value != null && value.Length > 0 && (value[0] & 1) == 1)
            {
                return Task.FromResult(new RepairValidation(true, $"Entrada '{_entryName}' confirmada como desativada no registro."));
            }

            return Task.FromResult(new RepairValidation(false, $"A entrada '{_entryName}' não está configurada como desativada."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new RepairValidation(false, $"Erro ao validar desativação da entrada: {ex.Message}"));
        }
    }

    public Task<RollbackResult> RollbackAsync(BackupRecord backup, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backup);

        try
        {
            var entry = backup.PreviousValues.GetValueOrDefault("EntryName") ?? _entryName;
            var isUser = bool.TryParse(backup.PreviousValues.GetValueOrDefault("IsCurrentUser"), out var parsedUser) ? parsedUser : _isCurrentUser;
            var hadValue = bool.TryParse(backup.PreviousValues.GetValueOrDefault("HadExistingValue"), out var parsedHad) && parsedHad;
            var prevBase64 = backup.PreviousValues.GetValueOrDefault("PreviousBytes");

            var hive = isUser ? Registry.CurrentUser : Registry.LocalMachine;
            using var key = hive.OpenSubKey(ApprovedRunKey, writable: true);
            if (key == null)
            {
                return Task.FromResult(new RollbackResult(false, "Chave de registro StartupApproved não encontrada durante o rollback."));
            }

            if (hadValue && !string.IsNullOrEmpty(prevBase64))
            {
                var originalBytes = Convert.FromBase64String(prevBase64);
                key.SetValue(entry, originalBytes, RegistryValueKind.Binary);
            }
            else
            {
                // Se antes não havia valor em StartupApproved, simplesmente remove a chave para voltar a ser ativo
                key.DeleteValue(entry, throwOnMissingValue: false);
            }

            return Task.FromResult(new RollbackResult(true, $"Entrada de inicialização '{entry}' restaurada ao estado original."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new RollbackResult(false, $"Falha ao executar rollback da inicialização: {ex.Message}"));
        }
    }

    private string GetCurrentStatusText()
    {
        try
        {
            var hive = _isCurrentUser ? Registry.CurrentUser : Registry.LocalMachine;
            using var key = hive.OpenSubKey(ApprovedRunKey, writable: false);
            var bytes = key?.GetValue(_entryName) as byte[];
            if (bytes != null && bytes.Length > 0)
            {
                return (bytes[0] & 1) == 1 ? "Desativado" : "Ativado";
            }
            return "Ativado (padrão)";
        }
        catch
        {
            return "Desconhecido";
        }
    }
}

