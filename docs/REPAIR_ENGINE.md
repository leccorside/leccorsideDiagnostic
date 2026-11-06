# Repair Engine

Encontrar um problema **não** significa corrigi-lo. Scanner e Repair Engine são estritamente separados (seção 5).

## Contrato

```csharp
public interface IRepairAction
{
    string Id { get; }
    string Title { get; }
    string Description { get; }
    RiskLevel Risk { get; }
    bool RequiresElevation { get; }
    bool RequiresRestart { get; }
    bool SupportsRollback { get; }

    Task<IReadOnlyList<PlannedChange>> PlanAsync(CancellationToken ct);              // Dry Run
    Task<IReadOnlyDictionary<string, string?>> CaptureStateAsync(CancellationToken ct); // backup prévio
    Task<RepairResult> ExecuteAsync(CancellationToken ct);
    Task<RepairValidation> ValidateAsync(CancellationToken ct);
    Task<RollbackResult> RollbackAsync(BackupRecord backup, CancellationToken ct);
}

// Opcional: comprova melhoria com medição antes/depois (seção 39)
public interface IMeasurableRepairAction : IRepairAction
{
    string MeasurementName { get; }
    string MeasurementUnit { get; }
    bool LowerIsBetter { get; }
    Task<double> MeasureAsync(CancellationToken ct);
}
```

## Fluxo (`RepairEngine.ExecuteAsync`)

```text
1. RiskAnalyzer.Assess(action)            → risco efetivo + exigência de aprovação
2. PlanAsync                              → alterações planejadas (Dry Run)
3. Dry Run (pedido ou Repair:DryRun)      → devolve o plano: "Nenhuma alteração foi realizada."
4. Pré-condições                          → CRITICAL nunca; aprovação suficiente; elevação disponível
5. MeasureAsync (antes)                   → se IMeasurableRepairAction
6. CaptureStateAsync + IBackupStore.Save  → backup ANTES de qualquer alteração (falhou? aborta sem tocar no sistema)
7. ExecuteAsync                           → exceção/falha ⇒ rollback automático se suportado
8. ValidateAsync + MeasureAsync (depois)  → MeasurementComparison (melhoria só com as duas medições)
9. Backup atualizado com NewValues + RepairRecord no histórico SQLite
```

`RollbackAsync(repairRecordId)` restaura o sistema a partir do backup e marca o registro como `RolledBack`
no histórico. Não é possível desfazer duas vezes.

## Níveis de Risco (seção 6)

| Nível | Exigência | Exemplo |
| --- | --- | --- |
| SAFE | pode ser automático | cache temporário comprovadamente descartável |
| LOW | confirmação | baixo risco, facilmente reversível |
| MEDIUM | confirmação | altera comportamento do sistema |
| HIGH | **confirmação explícita** | boot, drivers, serviços, rede, aplicações |
| CRITICAL | **nunca executado** — apenas orientação manual | partições, corrupção, perda de dados, hardware falhando |

### Escalonamento Automático (`RiskAnalyzer`)

O risco declarado nunca é reduzido; é elevado quando a ação contradiz o nível:
- sem rollback ⇒ no mínimo MEDIUM (SAFE/LOW pressupõem reversibilidade);
- exige reinicialização ⇒ no mínimo MEDIUM;
- exige administrador ⇒ no mínimo LOW (nunca automático).

## Backup e Ponto de Restauração (seções 36 e 38)

- **Backup Store (`FileBackupStore`)**: arquivos JSON isolados em `%ProgramData%\LeccorWindowsHealthCenter\Backups\{id}.json` com timestamp, ação, valores anteriores, novos valores, usuário e resultado. Permissões de escrita e leitura protegidas.
- **Ponto de Restauração Nativo (`IRestorePointService` / `RestorePointService`)**: cria pontos de restauração no Windows através da API WMI (`root\default:SystemRestore`). Verifica privilégios de Administrador e estado da Proteção de Sistema no volume C:. Disponível diretamente na Central de Correções antes de aplicar alterações.

## Ações de Reparo Reais Implementadas (MVP 3 — Passo 8)

| Ação | ID | Risco | Rollback | Elevação | Descrição Técnica |
| --- | --- | --- | --- | --- | --- |
| **Desativar Inicialização** | `repair.startup.disable` | LOW | Total | Não (HKCU) | Desativa entradas da chave `Run` escrevendo o sinalizador binário nativo `0x03` na chave `StartupApproved\Run`. O comando original é preservado. Rollback remove o sinalizador ou restaura o valor prévio. |
| **Bloquear Shell Extension** | `repair.explorer.disable-shell-extension` | LOW | Total | Não (HKCU) | Adiciona o CLSID órfão ou instável na chave oficial `HKCU\Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked`. O Explorer ignora a biblioteca sem mexer nas chaves de classe do sistema. Rollback remove o CLSID da lista. |
| **Limpeza Segura de Temporários** | `repair.storage.safe-clean` | SAFE | Não (descarte) | Não | Remove apenas temporários com mais de 24 horas no `User Temp`, arquivos em `Windows\Temp`, cache de miniaturas e relatórios WER / CrashDumps. Implementa `IMeasurableRepairAction` medindo em MB o espaço liberado. Arquivos em uso são ignorados com segurança. |
| **Reativar Windows Update** | `repair.windowsupdate.enable-service` | LOW | Total | Sim | Altera o tipo de inicialização de `wuauserv` no registro (`SYSTEM\CurrentControlSet\Services\wuauserv\Start`) de Desativado (4) para Manual (3) e inicializa o serviço. Rollback restaura o modo anterior. |

## Testes Automatizados

- `RepairEngineTests`: cobre ordem estrita de backup→execução, bloqueios de segurança (aprovação, elevação, CRITICAL), cancelamento, rollback automático em falha e validação antes/depois.
- `RepairActionsTests`: valida planejamento, captura de estado, execução, validação e rollback nas ações reais e tratamento de privilégios no `RestorePointService`.
- `RepairsViewModelTests`: valida interface de correções, seleção múltipla, Dry Run, criação de ponto de restauração, execução em lote com medição e acionamento de rollback.
