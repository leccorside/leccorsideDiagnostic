# RuleEngine

Toda decisão "isto é um problema?" vive em uma regra (`IRule`). Nada de `if (cpu > 90)` espalhado
em scanners ou ViewModels (seção 47).

## Contrato

```csharp
public interface IRule
{
    string Id { get; }                              // "cpu.sustained-high"
    string Name { get; }
    IReadOnlyList<string> RequiredScanners { get; } // a regra só roda se TODOS concluíram
    IReadOnlyList<RuleParameter> Parameters { get; }// thresholds com justificativa técnica
    IEnumerable<Finding> Evaluate(RuleContext context);
}

public sealed record RuleParameter(string Key, double DefaultValue, string Unit, string Justification);
```

## Garantias do motor

1. **Sem dados, sem conclusão**: se um scanner exigido falhou/foi cancelado, a regra não roda.
2. **Evidência obrigatória**: finding sem `Evidence` é descartado (e logado).
3. **Risco obrigatório**: finding com `RepairActionId` sem `RepairRisk` é descartado.
4. **Isolamento**: exceção em uma regra é logada e não afeta as outras.
5. **Rastreabilidade**: `Finding.RuleId` sempre aponta para a regra que o gerou.

## Configuração de thresholds

`appsettings.json`:

```json
"Rules": {
  "cpu.sustained-high": { "Enabled": true, "Threshold": 85, "MinimumSustainedSeconds": 120 }
}
```

`context.Parameter("Threshold")` devolve o valor configurado ou o padrão declarado. Pedir um parâmetro
não declarado é erro de programação (a regra falha e é isolada).

## Detecção de falsos positivos (seção 49)

`context.Series("cpu.total")` devolve uma `MetricSeries` com:

| Membro | Uso |
| --- | --- |
| `Count` | quantidade mínima de amostras |
| `Duration` | janela observada |
| `Mean`, `Min`, `Max`, `Percentile(p)` | estatísticas |
| `FractionAtOrAbove(t)` | proporção do tempo acima do limite |
| `LongestRunAtOrAbove(t)` | maior período **contínuo** acima do limite |
| `EpisodesAtOrAbove(t)` | recorrência (quantos episódios distintos) |

Padrão esperado (coberto por teste — `RuleEngineTests.False_positive_guard_short_spike_vs_sustained_load`):

```csharp
var series = ctx.Series("cpu.total");
if (series.Count < ctx.Parameter("MinimumSamples") ||
    series.LongestRunAtOrAbove(ctx.Parameter("Threshold")) < TimeSpan.FromSeconds(ctx.Parameter("MinimumSustainedSeconds")))
{
    return [];   // CPU 100% por 2 s NÃO é problema
}
```

## Severidade × Confiança

- `Severity` (Info/Low/Medium/High/Critical) — gravidade do problema. `Info` = recomendação, não conta como problema.
- `Confidence` (0–100%) — ≥ 90 confirmado, ≥ 60 provável, abaixo disso possível problema.

## Score (`ScoringEngine`)

- Categorias: Hardware, Windows, Performance, Storage, Stability, Security — só entram as **medidas**.
- Dedução por finding = peso da severidade (Critical 45, High 22, Medium 9, Low 3, Info 0) × confiança.
- Mesma regra repetida: retorno decrescente (1, 1/2, 1/3...) limitado a 2× o maior peso — 50 eventos iguais não são 50 problemas.
- Score geral = média ponderada das categorias (Hardware 1,3; Stability 1,2; demais 1,0; Performance 0,9).
- Faixas: ≥ 90 Excelente · ≥ 75 Bom · ≥ 50 Atenção · < 50 Crítico · sem cobertura = **Sem dados**.

## Catálogo de regras

Todas as séries combinam o histórico de fundo (5 s) com a janela da varredura (1 s). Sequências contínuas são
interrompidas quando duas amostras estão a mais de 30 s de distância (computador suspenso: sem medição, sem afirmação).

| Regra | Scanner | Dispara quando | Severidade | Confiança |
| --- | --- | --- | --- | --- |
| `cpu.sustained-high` | cpu | CPU ≥ `Threshold` (90%) sem interrupção por ≥ `MinimumSustainedSeconds` (300 s), com ≥ `MinimumSamples` (30) | Médio; Alto a partir de `HighSeveritySeconds` (900 s) | 95% (confirmado) |
| `cpu.throttling` | cpu | Com CPU ≥ `LoadThreshold` (50%), desempenho médio < `PerformanceThreshold` (60% da nominal) em ≥ `MinimumLoadedSamples` (5) | Baixo | 55% (possível) |
| `memory.pressure` | memory | RAM em uso ≥ `UsedThreshold` (90%) em ≥ `MinimumFraction` (80%) de ≥ `MinimumSamples` (10) amostras | Médio; Alto se leituras de página ≥ `PagingThreshold` (100/s) | 90% |
| `memory.commit-high` | memory | Commit ≥ `Threshold` (90% do limite) em ≥ 80% de ≥ 10 amostras | Alto | 90% |
| `memory.possible-leak` | memory | Memória privada de um processo (mesmo PID e início) cresce ≥ `MinimumGrowthMB` (500 MB) em ≥ `MinimumSamples` (10 min) e cresce em ≥ `MonotonicFraction` (90%) dos intervalos | Baixo; Médio a partir de `HighGrowthMB` (2 GB) | 60% (provável) |

### Discos e armazenamento (Passo 3)

Leituras únicas (não séries): cada regra conclui a partir do estado informado pelo Windows/dispositivo. Sem o dado
(ex.: temperatura em SATA/USB sem admin), a regra não conclui nada.

| Regra | Scanner | Dispara quando | Severidade | Confiança |
| --- | --- | --- | --- | --- |
| `storage.low-free-space` | disks | Volume do Windows com < `SystemMinimumFreeGb` (15 GB); volume de dados com < `DataMinimumFreePercent` (5%) **e** < `DataMinimumFreeGb` (20 GB). Removíveis e volumes < 1 GB ignorados | Sistema: Médio; Alto < `SystemCriticalFreeGb` (5 GB); Crítico < `AlwaysCriticalFreeGb` (1 GB). Dados: Baixo; Médio < 1 GB | 100% (medição direta) |
| `disk.health-warning` | disks | `HealthStatus` Warning/Unhealthy ou `OperationalStatus` com falha prevista/erro | Alto (Warning); Crítico (Unhealthy ou falha prevista) | 85% / 95% |
| `disk.temperature-high` | disks | Temperatura ≥ limite de aviso declarado pelo disco; sem declaração, `SolidStateWarningCelsius` (70 °C) ou `HddWarningCelsius` (55 °C) | Médio; Alto ≥ limite crítico (declarado ou aviso + `CriticalMarginCelsius`, 10 °C) | 85% (leitura única) |
| `disk.trim-disabled` | disks | SSD/NVMe com TRIM desativado no NTFS (registro) ou informado desativado pelo dispositivo. TRIM desconhecido não é problema | Baixo | 90% |
| `storage.temp-cache-large` | storage | Temporários + caches (sem Lixeira) ≥ `MinimumGb` (5 GB) | Informação (recomendação) | 100% |

Testes: `StorageRulesTests` (um caso que não dispara e outro que dispara por regra, incluindo o C: real desta máquina —
7% livre, mas 65 GB — que **não** deve gerar alerta).

A justificativa técnica de cada parâmetro está no próprio código (`RuleParameter.Justification`) e é verificada por
teste (`Every_performance_rule_declares_justified_parameters`). Cada regra tem testes com uma série que **não** deve
disparar e outra que deve (`PerformanceRulesTests`).

**Deliberadamente não implementado:** alerta de virtualização desativada no firmware (o WMI informa "desativada" com o
Hyper-V ativo — falso positivo em máquinas de desenvolvimento); qualquer regra de "CPU alta" sem duração; percentual de
saúde de disco estimado (sem desgaste informado pelo dispositivo, a UI mostra "não disponível"); alerta de espaço por
percentual em volumes grandes (5% de 2 TB ainda são ~100 GB).
