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
| Discos | `disks` | Disks | sim | 50 | 3 |
| Armazenamento | `storage` | Storage | sim | 60 | 3 |
| Integridade do Windows | `windows.integrity` | Windows | sim | 65 | 5 |
| Inicialização | `startup` | Startup | sim | 70 | 4 |
| Visualizador de Eventos | `eventviewer` | Events | sim | 75 | 5 |
| Windows Explorer | `explorer` | Explorer | sim | 80 | 6 |
| Serviços do Windows | `services` | Services | sim | 85 | 6 |
| Dispositivos e Drivers | `drivers` | Drivers | sim | 90 | 6 |

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
| Armazenamento | `Infrastructure/SystemInfo/WmiStorageInventoryProvider` (`IStorageInventoryProvider`) | WMI `root\Microsoft\Windows\Storage` (`MSFT_PhysicalDisk`, `MSFT_Disk`, `MSFT_Partition`, `MSFT_Volume`), cache de 30 s. IOCTLs com o disco aberto **com acesso 0** (sem admin, sem ler dados): TRIM e penalidade de busca (`IOCTL_STORAGE_QUERY_PROPERTY`), temperatura quando o dispositivo informa, e o disco de cada volume (`IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS` — o `MSFT_Partition` já estourou o timeout de 20 s sob carga). TRIM do NTFS no registro (`FileSystem\DisableDeleteNotification`). `MSFT_StorageReliabilityCounter` (temperatura, desgaste, horas) e SMART via `root\wmi` dão **acesso negado** sem admin: só são consultados elevado; sem elevação ficam null. |
| Análise de espaço | `Infrastructure/Storage/FileSystemSpaceAnalyzer` (`ISpaceAnalyzer`) | Enumeração com `FileSystemEnumerable` (sem `FileInfo` por arquivo). Não segue junções/links de pasta; arquivos só na nuvem (OneDrive: `Offline`/`RecallOnDataAccess`) não contam; pasta sem permissão é contada. Árvore podada (`MaxChildrenPerFolder`, `MinFolderBytes`); pastas sem permissão ficam na lista mesmo com 0 B (até 10 por pasta), para o aviso "N pastas sem permissão" apontar quais são. Paralelismo por tipo de disco: `MaxConcurrency` (4) em SSD/NVMe, `RotationalMaxConcurrency` (1) em HDD ou tipo desconhecido. Medido: C: com 2,6 mi de arquivos em 73–113 s com cache parcial (um leitor por vez levava ~9 min). Tamanhos lógicos: links físicos (WinSxS) contam em cada pasta. |
| Lista completa de processos | `Infrastructure/SystemInfo/ProcessInventoryProvider` (`IProcessInventoryProvider`) | Usada pela tela Processos e pelo impacto de inicialização. Pai via Toolhelp32 (`CreateToolhelp32Snapshot`); `OpenProcess` com `PROCESS_QUERY_LIMITED_INFORMATION` para caminho (`QueryFullProcessImageName`), início e CPU (`GetProcessTimes`), E/S (`GetProcessIoCounters`, soma disco + rede + dispositivos) e dono (token → SID → conta). Protegidos ficam null. Taxas por `ProcessRates.Compute` (mesmo PID **e** mesmo início). `Terminate` recusa `CriticalProcesses` e PID reaproveitado. |
| Identidade de arquivo | `Infrastructure/SystemInfo/FileIdentityProvider` (`IFileIdentityProvider`) | `FileVersionInfo` (fabricante, descrição, versão) + `WinVerifyTrust` sem consulta de revogação (só cache local, rápido e offline) + catálogo do sistema (`CryptCATAdminCalcHash2`/`EnumCatalogFromHash`: binários do Windows são assinados por catálogo, não no arquivo). Cache por caminho + data de modificação; no máximo 2 verificações simultâneas. |
| Inicialização | `Infrastructure/Startup/StartupInventoryProvider` (`IStartupInventoryProvider`) | Run/RunOnce em HKCU, HKLM (64 bits) e WOW6432Node, com o estado do Gerenciador de Tarefas em `Explorer\StartupApproved\Run`/`Run32` (primeiro byte ímpar = desativado); pastas Inicializar do usuário e comum (destino do `.lnk` via `WScript.Shell`); tarefas agendadas via `Schedule.Service` (gatilho de logon ou boot, ação executável; `\Microsoft\Windows\…` = componente do Windows); serviços com `StartMode='Auto'` via WMI. Cache de 30 s; cada fonte falha sozinha e vira aviso. Linhas de comando interpretadas por `StartupCommand.ExecutablePath`. |
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

### Discos (`disks`)

- **Fonte:** inventário de armazenamento (sempre lido de novo na varredura: espaço livre de 30 s atrás não é evidência).
- **Métricas:** `volume.free.percent` (%), `volume.free.bytes` (bytes) — tag `drive` (ex.: `C:\`); `disk.temperature` (°C, tag `disk`) só quando o dispositivo informa.
- **Observações:** `disk.physical` (nome, tipo, barramento, mídia, `solidState`, saúde, `predictedFailure`, estado operacional,
  TRIM do dispositivo e do NTFS, temperatura e limites declarados, desgaste/horas quando elevado) e `volume`
  (raiz, rótulo, sistema de arquivos, tamanho, livre, `system`, `removable`, disco, saúde do volume).
- **Custo:** ~0,15–0,8 s. **Elevação:** não (com elevação, acrescenta desgaste, horas ligadas e temperatura do contador de confiabilidade).
- **Regras:** `storage.low-free-space`, `disk.health-warning`, `disk.temperature-high`, `disk.trim-disabled`.
- **Nunca estimado:** "Saúde (vida útil)" sem desgaste informado é "não disponível"; o estado SMART é o `HealthStatus` que o
  próprio Windows resume a partir do dispositivo, não um percentual.

### Armazenamento (`storage`)

- **Fonte:** `ISpaceAnalyzer.MeasureLocationsAsync` sobre o catálogo `StorageLocations` (temporários do usuário e do Windows,
  downloads do Windows Update, despejos de memória e relatórios de erro, cache de miniaturas, caches do Edge e do Chrome
  em todos os perfis, Lixeira via `SHQueryRecycleBin`).
- **Métricas:** `storage.temp_cache.bytes` (soma, **sem** a Lixeira — esvaziar é decisão do usuário).
- **Observações:** `storage.location` por local (`sizeBytes`, `files`, `inaccessible`, `found`, `paths`).
- **Custo:** segundos (só os locais conhecidos; a árvore completa é sob demanda na tela). **Elevação:** não; pastas sem
  permissão viram contagem e aviso "o total pode ser maior". **Nada é apagado** (limpeza segura: Passo 7).
- **Regras:** `storage.temp-cache-large` (recomendação).

### Inicialização (`startup`)

- **Fonte:** inventário de inicialização (sempre lido de novo na varredura, `forceRefresh`).
- **Métricas:** `startup.entries` e `startup.entries.enabled` (count).
- **Observações:** `startup.entry` por entrada (`id`, `name`, `source` — `RegistryRun`, `RegistryRunOnce`, `StartupFolder`,
  `ScheduledTask`, `Service` —, `location`, `command`, `executable`, `exists`, `enabled`, `windows`, `user`). Valores
  booleanos em `True`/`False` invariantes; vazio = não determinado (ex.: comando que não pôde ser interpretado).
- **Custo:** ~1–2 s (tarefas agendadas e WMI de serviços). **Elevação:** não; tarefas de outros usuários não aparecem.
- **Regras:** `startup.missing-executable`, `startup.temp-location`, `startup.script-host`. **Somente leitura:** nada é
  desativado (Passo 8).
- **"Do próprio Windows" (`windows`):** tarefa em `\Microsoft\Windows\…` ou executável na pasta do Windows
  (`StartupCommand.IsWindowsOwned`), **exceto** hospedeiros de script (`powershell`, `cmd`, `mshta`, `rundll32`,
  `regsvr32`, `wscript`, `cscript`, `pwsh`: o que roda está no argumento) e drivers no `DriverStore` (NVIDIA, Realtek…).
  A tela oculta esses itens por padrão; os problemas aparecem sempre.
- **Impacto** (só na tela, não é finding): `StartupImpact.Estimate` soma memória privada e tempo de CPU dos processos
  em execução com o mesmo executável — ≥ 100 MB ou 5 s = Médio; ≥ 300 MB ou 30 s = Alto; fora de execução = "não medido".

### Integridade do Windows (`windows.integrity`)

- **Fonte:** executores não destrutivos de ferramentas nativas do Windows (`dism.exe /Online /Cleanup-Image /CheckHealth` na varredura rápida ou `/ScanHealth` sob demanda, `sfc.exe /VERIFYONLY` e `chkdsk.exe C:` somente leitura).
- **Métricas:** `windows.component_store.status` (status numérico).
- **Observações:**
  - `windows.component_store` (status `Healthy`, `Repairable` ou `Corrupted`, mensagem e saída);
  - `windows.system_files` (status `IntegrityOk`, `CorruptedRepaired` ou `CorruptedCannotRepair`);
  - `windows.file_system` (unidade, status `VolumeOk` ou `ErrorsFound`).
- **Codificações:** `sfc.exe` em UTF-16LE (`Encoding.Unicode`); `dism.exe` e `chkdsk.exe` em código OEM (`Encoding.GetEncoding(OEMCodePage)`).
- **Custo:** rápido (< 1 s) para DISM CheckHealth na varredura regular; varreduras profundas (SFC e DISM ScanHealth) executadas sob demanda na tela Windows. **Elevação:** sim (processo elevado mínimo via IPC seguro).
- **Regras:** `windows.component-store-corrupted`, `windows.system-files-corrupted`, `windows.file-system-errors`.

### Visualizador de Eventos (`eventviewer`)

- **Fonte:** leitura dos logs do Windows (`System`, `Application`, `Setup`, `Microsoft-Windows-WHEA-Logger/Operational` e `Microsoft-Windows-WindowsUpdateClient/Operational`) via `EventLogReader` com janela deslizante de 24h.
- **Métricas:** `eventviewer.total_errors` (count), `eventviewer.critical_errors` (count).
- **Observações:** `eventviewer.group` por grupo de Log + Origem + EventID com atributos (`log`, `provider`, `eventId`, `level`, `count24h`, `firstSeen`, `lastSeen`, `message`, `targetResource`, `technicalDetails`). Persistido na tabela `EventSummary` do SQLite.
- **Custo:** ~0,4–1,2 s para ler os eventos recentes com limitação de buffer (`MaxEventsPerLog`). **Elevação:** não (logs padrão são legíveis por usuários comuns; quando elevado, lê canais adicionais).
- **Regras:** `eventviewer.disk-io-errors`, `eventviewer.gpu-driver-crash`, `eventviewer.hardware-whea`, `eventviewer.unexpected-shutdown`, `eventviewer.app-crash`.

### Windows Explorer (`explorer`)

- **Fonte:** varredura de registros de extensões de shell (`HKCR\*\shellex`, `Directory\shellex`, `Folder\shellex`, `Drive\shellex`, etc.) resolvendo `InprocServer32`, lista de bloqueios em `Shell Extensions\Blocked`, e tamanho dos arquivos de cache de ícones e miniaturas (`IconCache.db`, `thumbcache_*.db`) em `%LocalAppData%\Microsoft\Windows\Explorer`.
- **Métricas:** `explorer.shellex.count` (count), `explorer.cache.total_bytes` (bytes), `explorer.cache.files` (count).
- **Observações:**
  - `explorer.shellex` por extensão de shell registrada (`name`, `clsid`, `type`, `dllPath`, `exists`, `publisher`, `signed`, `windows`, `blocked`);
  - `explorer.cache` com tamanho dos caches e eventuais arquivos inacessíveis/bloqueados.
- **Custo:** ~0,1–0,3 s. **Elevação:** não.
- **Regras:** `explorer.missing-shell-extension`, `explorer.unsigned-shell-extension`.

### Serviços (`services`)

- **Fonte:** leitura do registro `SYSTEM\CurrentControlSet\Services` (ImagePath, Tipo de inicialização, Dependências, Conta de logon) correlacionada com WMI `Win32_Service` (`SELECT Name, State, ProcessId FROM Win32_Service` em ~100 ms) para obter estado atual e PID sem dependência de DLLs externas, com validação de existência do executável e assinatura Authenticode/Catálogo.
- **Métricas:** `services.total` (count), `services.running` (count), `services.automatic` (count).
- **Observações:** `windows.service` por serviço do Windows (`name`, `displayName`, `state`, `startupType`, `account`, `executablePath`, `exists`, `publisher`, `signed`, `windows`, `pid`).
- **Custo:** ~0,2–0,4 s. **Elevação:** não (leitura do registro de serviços e consulta básica de WMI funcionam com privilégios de usuário comum).
- **Regras:** `services.missing-executable`, `services.failed-startup` (avalia falhas de SCM no Event Log).

### Dispositivos e Drivers (`drivers`)

- **Fonte:** inventário PnP via WMI `Win32_PnPEntity` e `Win32_PnPSignedDriver` cruzando DeviceID, fabricante, versão, data do driver, e códigos de erro de hardware do sistema (`ConfigManagerErrorCode`).
- **Métricas:** `drivers.devices.total` (count), `drivers.devices.problems` (count).
- **Observações:** `driver.device` por dispositivo com status operacional (`deviceId`, `name`, `manufacturer`, `driverVersion`, `status`, `errorCode`, `statusMessage`, `isProblem`, `isMissing`).
- **Custo:** ~0,3–0,8 s. **Elevação:** não.
- **Regras:** `drivers.device-error`, `drivers.missing-driver` (Código 28 / Dispositivo desconhecido).

## Métricas do dashboard (Monitoring)

`LiveMetricsProvider` usa `GetSystemTimes` (CPU por delta), `GlobalMemoryStatusEx` (RAM), `DriveInfo` (disco do
sistema) e uma sessão PDH só com os contadores de GPU, a cada 2 s, somente com o Dashboard visível e a janela não
minimizada. A primeira leitura de CPU é `null` (sem delta). A tela de Performance tem sua própria sessão PDH, criada
ao abrir a tela e descartada ao sair.

Consumo medido no Passo 2 (Release, histórico de fundo ativo): **0,17% de CPU** com o Dashboard visível, **0,46%** com a
Performance visível e **0,00%** minimizado; **101–120 MB** de memória privada.
