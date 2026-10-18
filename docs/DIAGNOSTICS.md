# Diagnóstico — framework e scanners

## Contrato

```csharp
public interface IDiagnosticScanner
{
    string Id { get; }              // estável: "cpu", "eventlog"...
    string Name { get; }            // exibido na UI
    ScanModule Module { get; }      // usado na varredura personalizada e na cobertura do score
    bool IncludedInQuickScan { get; }
    int Order { get; }              // ordem da seção 73: Hardware → CPU → RAM → GPU → Discos → ...
    Task<DiagnosticResult> ScanAsync(CancellationToken cancellationToken);
}
```

Um scanner **apenas coleta**:

- `Metric` — amostra numérica (nome, valor, unidade, timestamp, tags). Várias amostras com o mesmo nome formam uma série temporal.
- `Observation` — fato não numérico (ex.: "grupo de eventos System/Disk/153 com 47 ocorrências"), com atributos e evidências.

Um scanner **nunca**: decide se algo é problema (isso é do [RuleEngine](RULE_ENGINE.md)), altera o sistema,
executa processos fora do `ICommandExecutor`, ou armazena conteúdo de arquivos pessoais.

## `DiagnosticScannerBase`

Implementar `CollectAsync(ScanCollector collector, CancellationToken ct)`. A base cuida de:

- medir duração e registrar log estruturado (`Scanner {ScannerId} concluído em {DurationMs} ms`);
- converter exceções em `ScannerRunStatus.Failed` com mensagem amigável (a varredura continua);
- propagar cancelamento.

## Orquestração (`ScanOrchestrator`)

| Garantia | Implementação |
| --- | --- |
| Concorrência limitada | `Scan:MaxConcurrency` (padrão 2) — a ferramenta não pode causar a lentidão que mede |
| Timeout por scanner | `Scan:ScannerTimeout` (padrão 5 min) → `Failed` com "Tempo limite excedido" |
| Cancelamento | botão "Cancelar varredura"; scanners em execução recebem o token; os pendentes viram `Cancelled`; o já coletado é preservado e salvo |
| Isolamento de falhas | exceção de um scanner não derruba os demais |
| UI responsiva | scanners rodam em `Task.Run`; progresso via `IProgress<ScanProgress>`, com ícone Segoe Fluent e nome acessível por status (`Display.StatusGlyph`/`Display.Status`) |

## Modos

| Modo | Scanners |
| --- | --- |
| Rápida | `IncludedInQuickScan == true` |
| Completa | todos |
| Personalizada | os de `ScanModule` selecionados |

## Catálogo de scanners

| Scanner | Id | Módulo | Rápida | Ordem | Passo |
| --- | --- | --- | --- | --- | --- |
| Hardware | `hardware` | Hardware | sim | 10 | 2 |
| CPU | `cpu` | Performance | sim | 20 | 2 |
| Memória | `memory` | Performance | sim | 30 | 2 |
| GPU | `gpu` | Hardware | sim | 40 | 2 |

Cada scanner novo deve ser documentado aqui com: fonte de dados (WMI/contador/API/EventLog/comando),
métricas e observações produzidas (nome, unidade, tags), custo estimado (tempo/CPU), necessidade de
elevação e comportamento quando a fonte não está disponível ("não disponível", nunca valor inventado).

### Fontes de dados compartilhadas

| Fonte | Implementação | Observações |
| --- | --- | --- |
| Contadores PDH | `Monitoring/PdhPerformanceCounterSession` (`IPerformanceCounterSessionFactory`) | `PdhAddEnglishCounter`: nomes em inglês funcionam em Windows de qualquer idioma (no pt-BR os nomes são traduzidos). Contador inexistente é ignorado e vira "não disponível". Taxas e percentuais só existem a partir da 2ª coleta da sessão. |
| Memória do sistema | `Monitoring/SystemMemory` | `GlobalMemoryStatusEx` + `K32GetPerformanceInfo` (commit, cache, pools — em páginas × PageSize). |
| Processos | `Monitoring/ProcessSnapshotProvider` (`IProcessSnapshotProvider`) | Memória e contagens vêm da lista do sistema; tempo de CPU, horário de início e caminho exigem abrir o processo e ficam **null** (nunca 0) em processos protegidos. |
| Inventário | `Infrastructure/SystemInfo/WmiHardwareInventoryProvider` (`IHardwareInventoryProvider`) | WMI `root\cimv2`, uma classe por consulta (falha vira aviso), cache de 60 s, `forceRefresh` para a tela de Hardware. **Não** usar `Rewindable=false`: no .NET 10 isso devolve coleções vazias sem erro. VRAM vem do registro (`HardwareInformation.qwMemorySize`) porque `Win32_VideoController.AdapterRAM` é uint32 e trava em 4 GB. |
| Histórico de fundo | `Monitoring/MetricsHistoryService` (`IMetricsHistory`, `BackgroundService`) | CPU, memória e commit a cada 5 s; memória privada por processo (≥ 50 MB) a cada 60 s; retenção de 30 min; só em memória. Configurável em `MetricsHistory`. |

### Hardware (`hardware`)

- **Fonte:** inventário WMI + registro (sistema, SO, BIOS, placa-mãe, processadores, módulos de RAM, GPUs).
- **Métricas:** `memory.installed` (bytes), `memory.modules` (count), `cpu.cores` (count).
- **Observações:** `hardware.inventory` com o inventário serializado em JSON (atributo `json`) — persistido na tabela
  `HardwareSnapshot` a cada varredura.
- **Custo:** ~1–3 s na primeira leitura do WMI; depois cache. **Elevação:** não.
- **Findings:** nenhum (puramente informativo). A virtualização no firmware **não** vira finding: o WMI informa
  "desativada" quando o Hyper-V está ativo, o que geraria falso positivo justamente em máquinas de desenvolvedor.

### CPU (`cpu`)

- **Fonte:** histórico de fundo + PDH durante a varredura (`Sampling:Window`, padrão 15 s a cada 1 s) + duas fotografias de processos.
- **Métricas:** `cpu.total` (% — tag `source=history|scan`), `cpu.performance` (% da nominal), `cpu.frequency.base` (MHz),
  `cpu.frequency.effective` (MHz), `cpu.queue` (threads), `cpu.logical` (count).
- **Observações:** `process.cpu` — até `Sampling:TopProcesses` maiores consumidores (`pid`, `name`, `cpuPercent` em % da
  capacidade total da máquina, `path`); `sampling` (janela da varredura e duração do histórico).
- **Custo:** a janela de amostragem + ~50 ms por fotografia de processos. **Elevação:** não (processos protegidos ficam sem CPU medida).
- **Regras:** `cpu.sustained-high`, `cpu.throttling`.

### Memória (`memory`)

- **Fonte:** histórico de fundo + PDH/APIs de memória durante a varredura + fotografia de processos.
- **Métricas:** `memory.used` (%), `memory.commit` (%), `memory.pages_input` (páginas/s, hard faults), `memory.pagefile` (%),
  e na última leitura `memory.total`, `memory.available`, `memory.commit.total`, `memory.commit.limit`, `memory.cache`,
  `memory.pool.paged`, `memory.pool.nonpaged` (bytes); `process.private` (bytes, do histórico, tags `pid`, `name`, `start`).
- **Observações:** `process.memory` — maiores consumidores por memória privada (`privateBytes`, `workingSetBytes`, `path`).
- **Custo:** a janela de amostragem. **Elevação:** não.
- **Regras:** `memory.pressure`, `memory.commit-high`, `memory.possible-leak`.

### GPU (`gpu`)

- **Fonte:** inventário (nome, fabricante, VRAM, driver, data, status) + contadores `GPU Engine` e `GPU Adapter Memory`
  (`Sampling:GpuWindow`, padrão 5 s).
- **Métricas:** `gpu.count`, `gpu.utilization` (% — motor mais ocupado, tag `luid`), `gpu.dedicated.used` (bytes, tag `luid`).
- **Observações:** `gpu.adapter` por adaptador.
- **Agregação:** soma os processos por tipo de motor (3D, Copy, VideoDecode…) e usa o motor mais ocupado — mesmo
  critério do Gerenciador de Tarefas. Máquinas/VMs sem esses contadores mostram "não disponível".
- **Findings:** nenhum ainda. Eventos de driver (TDR, LiveKernelEvent, Display) dependem do Event Viewer Analyzer (Passo 5).

## Métricas do dashboard (Monitoring)

`LiveMetricsProvider` usa `GetSystemTimes` (CPU por delta), `GlobalMemoryStatusEx` (RAM), `DriveInfo` (disco do
sistema) e uma sessão PDH só com os contadores de GPU, a cada 2 s, somente com o Dashboard visível e a janela não
minimizada. A primeira leitura de CPU é `null` (sem delta). A tela de Performance tem sua própria sessão PDH, criada
ao abrir a tela e descartada ao sair.

Consumo medido no Passo 2 (Release, histórico de fundo ativo): **0,17% de CPU** com o Dashboard visível, **0,46%** com a
Performance visível e **0,00%** minimizado; **101–120 MB** de memória privada.
