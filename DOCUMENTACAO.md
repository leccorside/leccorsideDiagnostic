# Documentação técnica — Leccor Windows Health Center

Documento central de referência. Detalhes por assunto em `docs/` (links ao final de cada seção).
**Atualizar ao final de cada ação** (regra do projeto).

Versão documentada: **0.3.0 (Passo 3 — Discos e Armazenamento)**.

---

## 1. Visão geral

Aplicativo WPF (.NET 10) que executa **VARREDURA → DIAGNÓSTICO → CLASSIFICAÇÃO → CORREÇÃO → VALIDAÇÃO → RELATÓRIO**.
Scanners coletam dados; o RuleEngine transforma dados em findings com evidência; o ScoringEngine calcula
a saúde; o RepairEngine aplica correções aprovadas com backup e rollback. Tudo local.

Arquitetura e camadas: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## 2. Componentes implementados

| Componente | Local | Função |
| --- | --- | --- |
| `ScanOrchestrator` | Application/Scanning | Executa scanners (concorrência limitada, timeout, cancelamento, progresso), aplica regras, score e salva |
| `DiagnosticScannerBase` / `ScanCollector` | Application/Scanning | Base para scanners: duração, logs, falhas isoladas |
| `RuleEngine` / `MetricSeries` / `ConfigurationRuleSettings` | Application/Rules | Regras centralizadas, thresholds configuráveis, estatísticas anti falso positivo |
| `RiskAnalyzer` | Application/Risk | Risco efetivo e exigência de aprovação |
| `ScoringEngine` | Application/Scoring | Score 0–100 por categoria; "sem dados" quando nada foi medido |
| `RepairEngine` | Repair | Fluxo seguro de correção com Dry Run, backup, validação, antes/depois e rollback |
| `CommandExecutor` / `ExecutablePolicy` / `JobObject` | Infrastructure/Commands | Execução segura de processos externos |
| `SqliteDatabase` / `MigrationRunner` / repositórios | Infrastructure/Persistence | Histórico local em SQLite |
| `FileBackupStore` | Infrastructure/Backup | Backups JSON com ACL restrita e escrita atômica |
| `LoggingSetup` / `RedactionEnricher` | Infrastructure/Logging | Logs JSON estruturados, rotação, retenção, redação de segredos |
| `PrivilegeService` | Infrastructure/Security | Elevação atual e usuário |
| `LiveMetricsProvider` | Monitoring | CPU/RAM/disco/GPU do dashboard (Win32 + PDH) |
| `PdhPerformanceCounterSession` / `PdhQuery` / `GpuCounterParser` | Monitoring | Contadores PDH por nome em inglês (CPU, frequência, fila, hard faults, paginação, GPU Engine, VRAM) |
| `SystemMemory` / `CpuUsageMeter` | Monitoring | Memória do sistema (commit, cache, pools) e CPU por `GetSystemTimes` |
| `ProcessSnapshotProvider` | Monitoring | Fotografia de processos (CPU, memória, início, caminho; protegidos = null) |
| `MetricsHistoryService` | Monitoring | Histórico de fundo de 30 min (sistema a cada 5 s, processos a cada 60 s) para duração/recorrência |
| `WmiHardwareInventoryProvider` | Infrastructure/SystemInfo | Inventário WMI + VRAM pelo registro, cache de 60 s |
| `WmiStorageInventoryProvider` / `StorageDeviceQuery` / `WmiQuery` | Infrastructure/SystemInfo | Discos, partições e volumes (WMI de armazenamento) + IOCTLs com acesso 0 (TRIM, SSD × HDD, temperatura, disco de cada volume), cache de 30 s |
| `FileSystemSpaceAnalyzer` | Infrastructure/Storage | Análise de espaço (árvore podada, maiores/antigos, sem seguir junções, sem contar arquivos só na nuvem) e medição de temporários/caches |
| `StorageText` / `StorageLocations` | Application | Tradução dos códigos do WMI de armazenamento; catálogo de temporários e caches medidos |
| `HardwareScanner`, `CpuScanner`, `MemoryScanner`, `GpuScanner` | Diagnostics | Scanners do Passo 2 (ver docs/DIAGNOSTICS.md) |
| `DiskScanner`, `StorageScanner` | Diagnostics/Storage | Scanners do Passo 3 |
| `LowFreeSpaceRule`, `DiskHealthRule`, `DiskTemperatureRule`, `TrimDisabledRule`, `TempCacheLargeRule` | Diagnostics/Rules | Regras do Passo 3 (ver docs/RULE_ENGINE.md) |
| `CpuSustainedHighRule`, `CpuThrottlingRule`, `MemoryPressureRule`, `CommitChargeHighRule`, `PossibleMemoryLeakRule` | Diagnostics/Rules | Regras do Passo 2 (ver docs/RULE_ENGINE.md) |
| `JsonReportExporter` | Reporting | Exportação do `ScanReport` em JSON |
| `HealthCenterHost` | App | Composition root (Generic Host + DI validada) |
| `StartupTrace` | App | Tempo de cada fase da inicialização (processo → primeiro quadro) e watchdog de inicialização lenta (> 10 s) |
| Telas | App/Views | Dashboard, Varredura, Performance, Hardware, Armazenamento, Configurações e páginas "em construção" para os demais módulos |

## 3. Interface

- Sidebar com o menu completo da seção 8. Módulos ainda não implementados mostram uma página honesta
  "Módulo em construção" com o MVP previsto — nunca dados não medidos.
- **Dashboard**: anel de score (ou "SEM DADOS") com a **cobertura** ("Avaliou 2 de 6 áreas…"), resultado da última
  varredura (críticos, importantes, menores, recomendações, problema mais importante), uso atual de CPU/RAM com
  histórico de até 2 min, disco do sistema e GPU (motor mais ocupado; "não disponível" sem contadores).
  Logo de boas-vindas antes da primeira varredura.
- **Varredura**: rápida, completa, personalizada (16 módulos selecionáveis), progresso por scanner com
  "Cancelar varredura", resultado com score, cobertura e lista de findings expansíveis respondendo às 6 perguntas da seção 72.
- **Performance**: CPU (uso, frequência efetiva, % da nominal, fila, processadores lógicos), Memória (uso, commit, cache,
  pools, arquivo de paginação, leituras de página do disco), GPU (motores, VRAM em uso), gráficos com o histórico de fundo
  (até 30 min) e maiores consumidores de CPU e memória. Amostra a cada 2 s só com a tela aberta.
- **Hardware**: sistema, SO, BIOS e placa-mãe; processadores; memória e módulos; placas de vídeo (VRAM real, driver e data).
  Botão "Ler novamente"; fontes WMI que falharam aparecem no modo avançado.
- **Armazenamento** (ordenada como laudo):
  1. **Diagnóstico no topo:** cartões para disco com falha prevista/estado ruim e volume com pouco espaço (evidência,
     volumes afetados, ação e botão "Analisar"), pelos mesmos critérios das regras; sem achados, uma linha de OK.
  2. **Volumes** com espaço livre e barra de uso (âmbar/vermelho pela regra `storage.low-free-space`; compactos acima de 6).
  3. **Análise de espaço** sob demanda por volume ou pasta ("Analisar outra pasta…"): progresso ao vivo, cancelar,
     análises guardadas por volume, trilha de navegação, ordenar por tamanho/nome, linhas que entram na pasta ou a
     mostram no Explorer, menu de contexto (Mostrar no Explorer, Copiar caminho), atalhos (Enter, Backspace/Alt+←, Ctrl+C, F5).
  4. **Arquivos da pasta atual** (maiores e grandes sem alteração há anos) com filtro por nome/extensão e "Mostrar todos";
     explicação de itens conhecidos (`StorageHints`: discos virtuais do Docker/WSL, modelos de IA, ISOs, `.sys`,
     `node_modules`…) com "não apague" quando apagar quebra algo.
  5. **Discos físicos** (tipo, utilizado/livre, vida útil, estado SMART, temperatura, TRIM, partições, firmware); lista
     compacta acima de 4 discos com o crítico já aberto.
  6. **Temporários e caches** medidos, incluindo npm, Yarn, pnpm, NuGet, Gradle, Maven e pip com o comando oficial de
     limpeza; a Lixeira fica fora da soma. Nada é apagado.
  Sem administrador, vida útil e horas ligadas aparecem como "não disponível" com a explicação. Leitor de tela: status
  da análise anunciado no início/fim, nomes acessíveis completos e foco que acompanha a ação.
  "Mostrar no Explorer" usa a API do shell (`ShellLauncher`, `SHOpenFolderAndSelectItems`): seleciona, nunca executa.
- **Configurações**: tema (Windows/Claro/Escuro, inclusive barra de título), modo avançado, estado de privilégio,
  Dry Run, caminhos de dados, versão.
- Logos: `logo2.png` (tema claro) e `logo1.jpg` (tema escuro, como selo); `logo-mark.png` e `app.ico` são
  recortes do símbolo da `logo2.png`.
- **Sistema visual** (`Themes/`): `Light.xaml`/`Dark.xaml` definem todos os pincéis (superfícies, texto, destaque,
  barra de rolagem, controles e cores semânticas com contraste ≥ 4,5:1 medido); `Styles.xaml` define tipografia,
  cartão, botões (com quebra de linha), ScrollBar estilo Windows 11, CheckBox, RadioButton, Expander, seleção de texto
  e os estilos semânticos `SemanticText`, `SeverityDot` e `ScoreRing` (valor via `Tag`). Sempre referenciar pincéis
  com `DynamicResource` para acompanhar a troca de tema.
- **Revisão de UI:** toda alteração de interface passa pelo skill `impeccable` e é verificada nos temas claro e escuro.
- **Fonte da verdade visual:** [DESIGN.md](DESIGN.md) (tokens normativos e regras) e `.impeccable/design.json`
  (escalas tonais, sombra, movimento e componentes de referência). Ao mudar um pincel em `Light.xaml`/`Dark.xaml`,
  atualizar o token correspondente no DESIGN.md.

## 4. Configuração (`src/LeccorWindowsHealthCenter.App/appsettings.json`)

| Chave | Padrão | Descrição |
| --- | --- | --- |
| `HealthCenter:AdvancedMode` | `false` | Exibe detalhes técnicos (referências de evidência, comandos...) |
| `HealthCenter:Theme` | `System` | `System`, `Light` ou `Dark` |
| `HealthCenter:DashboardRefreshInterval` | `00:00:02` | Intervalo de amostragem do dashboard (mín. 1 s) |
| `Scan:MaxConcurrency` | `2` | Scanners em paralelo |
| `Scan:ScannerTimeout` | `00:05:00` | Tempo máximo por scanner |
| `Sampling:Window` / `Sampling:Interval` | `00:00:15` / `00:00:01` | Janela e intervalo de amostragem de CPU/memória na varredura |
| `Sampling:GpuWindow` | `00:00:05` | Janela de amostragem da GPU |
| `Sampling:TopProcesses` | `10` | Quantos maiores consumidores entram como evidência |
| `MetricsHistory:Enabled` | `true` | Liga o histórico de fundo (sem ele, regras de duração não concluem) |
| `MetricsHistory:SystemInterval` / `ProcessInterval` | `00:00:05` / `00:01:00` | Intervalos do histórico de sistema e de processos |
| `MetricsHistory:Retention` | `00:30:00` | Quanto histórico fica em memória |
| `MetricsHistory:ProcessMinimumPrivateBytes` | `52428800` | Só processos com ≥ 50 MB privados entram no histórico |
| `SpaceAnalysis:MaxConcurrency` | `4` | Pastas lidas em paralelo na análise de espaço em SSD/NVMe |
| `SpaceAnalysis:RotationalMaxConcurrency` | `1` | O mesmo em HDD ou tipo de disco não identificado (leituras simultâneas pioram em disco mecânico) |
| `SpaceAnalysis:MaxChildrenPerFolder` / `MinFolderBytes` | `50` / `1048576` | Subpastas listadas por pasta; menores são somadas ("N pastas menores") |
| `SpaceAnalysis:TopFiles` | `50` | Tamanho das listas de maiores arquivos e de arquivos antigos (a tela mostra 20) |
| `SpaceAnalysis:OldFileAge` / `OldFileMinimumBytes` | `730.00:00:00` / `10485760` | "Arquivo grande sem alteração": ≥ 10 MB e sem alteração há 2 anos |
| `Repair:DryRun` | `false` | Força simulação em todas as correções |
| `FileLogging:MinimumLevel` | `Information` | Nível mínimo de log |
| `FileLogging:FileSizeLimitBytes` | `10485760` | Tamanho máximo por arquivo antes de rolar |
| `FileLogging:RetainedFileCount` | `14` | Quantidade de arquivos de log mantidos |
| `CommandExecution:AdditionalTrustedExecutables` | `[]` | Executáveis fora de `%windir%` permitidos (caminho completo) |
| `Rules:<ruleId>:Enabled` / `Rules:<ruleId>:<Parametro>` | — | Habilita/desabilita regra e sobrescreve thresholds |

Variáveis de ambiente e argumentos de linha de comando também sobrescrevem (padrão do Generic Host).

## 5. Dados locais

| Item | Caminho (instalado) | Portable (`portable.flag`) |
| --- | --- | --- |
| Raiz | `%ProgramData%\LeccorWindowsHealthCenter` (fallback `%LocalAppData%\...`) | `.\Data` |
| Logs | `...\Logs\lwhc-AAAAMMDD.log` (JSON compacto: `@t`, `@mt`, propriedades). Cada abertura registra "Inicialização concluída em X ms" com o tempo por fase; acima de 10 s, aviso "Inicialização lenta" e o arquivo `startup-slow-{pid}.txt` com a fase em que estava | `.\Data\Logs` |
| Backups | `...\Backups\{id}.json` (ACL restrita) | `.\Data\Backups` |
| Banco | `...\Data\health.db` (SQLite, WAL) | `.\Data\Data\health.db` |
| Relatórios | `...\Reports` | `.\Data\Reports` |

## 6. Banco de dados (schema v1)

Migrations versionadas em `Infrastructure/Persistence/Migrations.cs` (nunca editar uma migration publicada;
sempre adicionar nova versão). Controle em `SchemaMigrations`.

| Tabela | Conteúdo |
| --- | --- |
| `Scan` | varredura: modo, início/fim, cancelada, score, rating, máquina, scores por categoria (JSON) |
| `ScannerRun` | execução de cada scanner: status, duração, erro |
| `Finding` | finding completo (regra, categoria, severidade, confiança, impacto, recomendação, risco, rollback, status) |
| `Evidence` | evidências de cada finding (origem, descrição, valor, unidade, referência técnica, amostras) |
| `Metric` | amostras numéricas da varredura (com tags JSON) |
| `Repair` | histórico de correções (risco, resultado, usuário, motivo, backup, alterações, antes/depois) |
| `Rollback` | rollbacks executados |
| `HardwareSnapshot` | inventário de hardware em JSON, gravado a cada varredura que inclui o scanner `hardware` |
| `EventSummary` | grupos de eventos (Passo 5) |
| `Application` | inventário de software (Passo 7) |
| `Configuration` | preferências persistidas (Passo 10) |

## 7. Segurança

Resumo: app sem admin (`asInvoker`), comandos só via `ICommandExecutor` com `ArgumentList` e allowlist de
executáveis, interpretadores de shell bloqueados, Job Object contra processos órfãos, redação de segredos em
logs e saídas, backups com ACL. Detalhes: [docs/SECURITY.md](docs/SECURITY.md).

## 8. Diagnóstico, regras e reparo

- Framework de scanners e catálogo: [docs/DIAGNOSTICS.md](docs/DIAGNOSTICS.md)
- Regras, thresholds, falsos positivos e score: [docs/RULE_ENGINE.md](docs/RULE_ENGINE.md)
- Reparo, risco, backup, rollback e Dry Run: [docs/REPAIR_ENGINE.md](docs/REPAIR_ENGINE.md)

## 9. Desenvolvimento, build e release

- [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) — convenções, como adicionar scanner/regra/ação, testes
- [docs/BUILD.md](docs/BUILD.md) — build local, publicação, portable, CI
- [docs/RELEASE.md](docs/RELEASE.md) — versionamento e checklist

## 10. Testes (Passo 3)

| Projeto | Testes |
| --- | --- |
| UnitTests | 230 (inclui `InventoryTextTests`, `StorageTextTests`, `StorageRulesTests` e `StorageHintsTests`) |
| IntegrationTests | 32 (inclui lista ampliada com pastas dominantes, `ShellLauncher` com caminho inexistente, leitura real de WMI, PDH, memória, processos, histórico, discos/volumes desta máquina e o analisador de espaço sobre uma árvore temporária: soma, poda, maiores/antigos, junção não seguida, cancelamento) |
| SystemTests | 20 (inclui varredura completa real com os 6 scanners, `FmtTests` e `StorageScreenTests`) |
| **Total** | **282 — todos passando** (Debug e Release com `-warnaserror`; 3 rodadas seguidas com as suítes em paralelo) |

## 11. Dados de desenvolvimento e textos de inventário

- **Seletor de dados (só DEBUG)** — `src/LeccorWindowsHealthCenter.App/DevData/`: Dados reais · Pior caso · Vazio ·
  Um · Muitos, no centro inferior da janela. Os dados trocam na fronteira (decoradores dos provedores de contadores,
  processos, histórico e inventário). Modo inicial: `--DevData:Mode=worst|empty|one|many`. Não existe no Release.
  As fixtures são o teste de regressão visual das telas de Performance, Hardware e Armazenamento
  (`DevDataStorageFixtures`: discos com falha prevista, sem mídia, sem letra, 24 discos, análise com OneDrive e
  `node_modules`, estados vazio e de um item).
- **`InventoryText`** (Application/SystemInfo): `Clean` descarta placeholders de firmware ("To Be Filled By O.E.M.",
  "System Product Name"…); `MemoryVendor` traduz código JEDEC em fabricante; `DeviceStatus` traduz o status WMI com
  `StatusTone` (Normal/Warning/Critical), exibido em âmbar/vermelho pelos templates de `InfoRow`.
- **Formatação (`Fmt`, `ByteSize`)**: `Fmt.Reading` → "—" quando não há leitura (a legenda explica); `Fmt.Count` com
  plural e separador de milhar; `ByteSize.Format` também usa separador de milhar ("1.012,8 MB"); `ByteSize.FormatPair`/`Fmt.BytesOf`
  mostram parte e total na mesma unidade; `Fmt.Duration` ("1 min 52 s").
- **Estilo `MetricValue`**: números grandes dos cartões com algarismos tabulares, em `Grid(*,Auto)` ao lado do título.
