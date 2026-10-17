# Repair Engine

Encontrar um problema **não** significa corrigi-lo. Scanner e Repair Engine são separados (seção 5).

## Contrato

```csharp
public interface IRepairAction
{
    string Id { get; }  string Title { get; }  string Description { get; }
    RiskLevel Risk { get; }
    bool RequiresElevation { get; }  bool RequiresRestart { get; }  bool SupportsRollback { get; }

    Task<IReadOnlyList<PlannedChange>> PlanAsync(CancellationToken ct);              // Dry Run
    Task<IReadOnlyDictionary<string, string?>> CaptureStateAsync(CancellationToken ct); // backup
    Task<RepairResult> ExecuteAsync(CancellationToken ct);
    Task<RepairValidation> ValidateAsync(CancellationToken ct);
    Task<RollbackResult> RollbackAsync(BackupRecord backup, CancellationToken ct);
}

// Opcional: comprova melhoria com medição antes/depois (seção 39)
public interface IMeasurableRepairAction : IRepairAction
{
    string MeasurementName { get; }  string MeasurementUnit { get; }  bool LowerIsBetter { get; }
    Task<double> MeasureAsync(CancellationToken ct);
}
```

## Fluxo (`RepairEngine.ExecuteAsync`)

```text
1. RiskAnalyzer.Assess(action)            → risco efetivo + exigência de aprovação
2. PlanAsync                              → alterações planejadas
3. Dry Run (pedido ou Repair:DryRun)      → devolve o plano: "Nenhuma alteração foi realizada."
4. Pré-condições                          → CRITICAL nunca; aprovação suficiente; elevação disponível
5. MeasureAsync (antes)                   → se IMeasurableRepairAction
6. CaptureStateAsync + IBackupStore.Save  → backup ANTES de qualquer alteração (falhou? aborta)
7. ExecuteAsync                           → exceção/falha ⇒ rollback automático se suportado
8. ValidateAsync + MeasureAsync (depois)  → MeasurementComparison (melhoria só com as duas medições)
9. Backup atualizado com NewValues + RepairRecord no histórico
```

`RollbackAsync(repairRecordId)` restaura a partir do backup e marca o registro como `RolledBack`
(tabela `Rollback`). Não é possível desfazer duas vezes.

## Níveis de risco (seção 6)

| Nível | Exigência | Exemplo |
|---|---|---|
| SAFE | pode ser automático | cache temporário comprovadamente descartável |
| LOW | confirmação | baixo risco, facilmente reversível |
| MEDIUM | confirmação | altera comportamento do sistema |
| HIGH | **confirmação explícita** | boot, drivers, serviços, rede, aplicações |
| CRITICAL | **nunca executado** — apenas orientação manual | partições, corrupção, perda de dados, hardware falhando |

### Escalonamento automático (`RiskAnalyzer`)

O risco declarado nunca é reduzido; é elevado quando a ação contradiz o nível:

- sem rollback ⇒ no mínimo MEDIUM (SAFE/LOW pressupõem reversibilidade);
- exige reinicialização ⇒ no mínimo MEDIUM;
- exige administrador ⇒ no mínimo LOW (nunca automático).

## Backup (seção 36)

`%ProgramData%\LeccorWindowsHealthCenter\Backups\{id}.json` com timestamp, ação, valores anteriores,
novos valores, usuário e resultado. Diretório com ACL restrita. Restore Point (seção 38) será oferecido
para ações de maior impacto no MVP 3 — e **não** substitui backup de dados.

## Ações disponíveis

| Ação | Risco | Rollback | Status |
|---|---|---|---|
| — | — | — | Nenhuma ação concreta no Passo 1 (MVP 3 — Passo 8). |

## Testes

Ações reais nunca são executadas em testes automatizados. `RepairEngineTests` usa `FakeRepairAction`
(estado em memória) e cobre: Dry Run, bloqueios por aprovação/CRITICAL/elevação, ordem backup→execução,
falha de backup, rollback automático, validação falha com rollback disponível, rollback único, antes/depois.
