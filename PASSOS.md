# PASSOS — Plano de execução

Regras deste plano (definidas no PROMPT.txt, seção 69):

- Os passos são executados **um por vez**.
- Ao final de cada passo: build sem avisos, todos os testes passando, `CONTEXTO.md` e `DOCUMENTACAO.md` atualizados e **texto de commit** entregue.
- **Nunca** avançar para o próximo passo sem autorização explícita do usuário.
- Commits são feitos manualmente pelo usuário (o agente não executa `git add`/`git commit`).

Legenda: ✅ concluído · ⏳ aguardando autorização · ⬜ não iniciado

---

## Passo 1 — Fundação da arquitetura ✅

Corresponde à "primeira execução" (seção 75) e à ordem de implementação (seção 74).

- [x] Documentação inicial: README, CONTEXTO, DOCUMENTACAO, PASSOS, ARCHITECTURE, SECURITY, DIAGNOSTICS, REPAIR_ENGINE, RULE_ENGINE, DEVELOPMENT, BUILD, RELEASE
- [x] Solution `.sln` com 9 projetos `src/` + 3 projetos de teste; `Directory.Build.props`, `Directory.Packages.props` (versões centralizadas), `global.json`, `.editorconfig`
- [x] Domain: `Finding`, `Evidence`, `Metric`, `Observation`, `Confidence`, `DiagnosticResult`, `ScanReport`, `HealthScore`, enums (`Severity`, `RiskLevel`, ...), modelos de reparo/backup
- [x] Application: `IDiagnosticScanner` + `DiagnosticScannerBase`, `ScanOrchestrator` (concorrência limitada, timeout, cancelamento, progresso), `RuleEngine` + `MetricSeries` (anti falso positivo), `RiskAnalyzer`, `ScoringEngine`, abstrações de reparo/persistência/comandos
- [x] Infrastructure: `CommandExecutor` seguro (ArgumentList, política de executáveis, Job Object, timeout, redação), SQLite + migrations, repositórios, `FileBackupStore` com ACL, logging Serilog (rotação/retenção/redação), `PrivilegeService`
- [x] Repair: `RepairEngine` (risco → aprovação → backup → execução → validação → antes/depois → histórico → rollback; Dry Run)
- [x] Monitoring: amostragem leve de CPU/RAM/disco para o dashboard
- [x] Reporting: exportador JSON
- [x] App WPF/MVVM: DI via Generic Host, tema claro/escuro, sidebar com o menu completo, Dashboard, tela de Varredura (rápida/completa/personalizada, progresso, cancelar, resultado), Configurações
- [x] Testes: 115 unitários, 16 integração, 5 sistema — todos passando
- [x] GitHub Actions (restore, build com analisadores como erro, test, publish win-x64)

## Passo 2 — MVP 1 (a): Hardware, CPU, Memória e GPU ✅

- [x] Scanner de hardware (WMI: sistema, SO, BIOS, placa-mãe, CPU, módulos de RAM, GPUs; VRAM real pelo registro) + `HardwareSnapshot` no banco
- [x] Scanner de CPU com amostragem temporal (utilização, frequência efetiva e % da nominal, fila, top processos)
- [x] Scanner de memória (em uso, commit, cache, pools paginado/não paginado, arquivo de paginação, hard faults; top processos)
- [x] Scanner de GPU (fabricante, VRAM, driver e data; utilização por motor e VRAM em uso via "GPU Engine") + dashboard exibe GPU
- [x] Histórico de fundo de 30 min (CPU/memória a cada 5 s, memória por processo a cada 60 s) — base de duração e recorrência das regras
- [x] Regras com thresholds justificados: CPU sustentada, possível limitação de frequência, pressão de memória (com paginação), commit alto, possível vazamento
- [x] Telas Performance e Hardware; cobertura do score ("Avaliou X de 6 áreas") no dashboard e no resultado
- [x] Estado pressionado nos botões (lacuna do DESIGN.md) e revisão de UI com impeccable nos dois temas
- [x] Cada scanner e regra documentados em `docs/DIAGNOSTICS.md` e `docs/RULE_ENGINE.md`
- [x] Testes: 137 unitários, 22 integração (fontes reais), 6 sistema (varredura completa real)

Fica para passos seguintes: eventos de driver de GPU (TDR/LiveKernelEvent — Passo 5, Event Viewer); temperatura de
CPU (não há fonte confiável sem driver de kernel; não será estimada).

## Passo 3 — MVP 1 (b): Discos e Armazenamento ✅

- [x] Discos físicos (modelo, tipo NVMe/SSD/HDD, capacidade, partições, TRIM, firmware) via WMI de armazenamento + IOCTLs sem administrador; saúde **somente quando o dispositivo fornece** (estado SMART resumido pelo Windows; vida útil "não disponível" sem admin; temperatura só quando o disco informa)
- [x] Volumes com espaço livre e vínculo com o disco físico pelo próprio volume (`IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS`), independente do WMI
- [x] Análise de espaço sob demanda: árvore navegável e podada das maiores pastas, maiores arquivos, arquivos grandes antigos; paralelismo por tipo de disco (4 em SSD, 1 em HDD), progresso e cancelamento; sem seguir junções; arquivos só na nuvem fora da soma
- [x] Temporários e caches medidos (temp do usuário/Windows, Windows Update, despejos/WER, miniaturas, Edge, Chrome, Lixeira) — nada é apagado
- [x] Regras: pouco espaço livre, disco sinaliza problema (SMART/HealthStatus), temperatura alta (quando disponível), TRIM desativado em SSD, temporários/caches grandes (recomendação)
- [x] Tela Armazenamento revisada com impeccable nos temas claro e escuro, em 1280 e 980 px
- [x] Teste de estresse break-ui da tela Armazenamento: seletor de dados de desenvolvimento com fixtures de pior caso, 14 achados + 4 decisões corrigidos (formatação de espaço com zero e valores quase iguais, plural, corrida de carregamento, pastas sem permissão, barra do volume pela mesma decisão da regra, lista compacta de discos, rolagem até a análise)
- [x] Crítica com impeccable (26/40) e execução do plano inteiro: tela "laudo primeiro" com diagnóstico no topo, análises guardadas, listas da pasta atual, explicações para desenvolvedor (Docker/WSL/modelos) e caches de npm/NuGet/Gradle/pip/Yarn/pnpm/Maven, acessibilidade (foco, região viva, nomes completos, menu de contexto), mostrar no Explorer pela API do shell
- [x] Testes: 229 unitários, 32 integração (fontes reais + árvore temporária + pasta sem permissão), 20 sistema (varredura completa com 6 scanners + tela Armazenamento)

Fica para passos seguintes: SMART/NVMe detalhado, desgaste e horas ligadas (exigem o processo elevado — Passo 5);
eventos de disco (Disk 7/153, NTFS — Passo 5, Event Viewer); limpeza dos temporários/caches (Passo 7).

## Passo 4 — MVP 1 (c): Processos e Inicialização ✅ (aguardando autorização para o Passo 5)

- [x] Process Manager (PID, CPU, RAM, E/S, threads, handles, usuário, executável, fabricante, assinatura com catálogo; árvore; pesquisa; ordenação; proteção de processos críticos; abrir local, propriedades, copiar caminho e finalizar com confirmação e conferência do horário de início)
- [x] Startup Manager **somente leitura** (Run/RunOnce HKCU/HKLM/WOW6432Node com o estado do Gerenciador de Tarefas, pastas Inicializar, tarefas agendadas de logon/boot, serviços automáticos) com impacto estimado pelo consumo atual
- [x] Regras: entradas apontando para executáveis inexistentes (tarefa do próprio Windows = informativo), executáveis em diretórios temporários, script oculto via hospedeiro do Windows (PowerShell codificado, mshta, regsvr32…) — acrescentada no break-ui
- [x] Telas Processos e Inicialização revisadas com impeccable nos temas claro e escuro, em 1280 e 980 px
- [x] Testes: 294 unitários, 37 integração (inventário real, recusa de processo crítico, encerrar processo do teste, assinatura por catálogo), 25 sistema (varredura completa com 7 scanners)

Fica para passos seguintes: desativar entradas de inicialização com backup e desfazer (Passo 8, Correções); finalizar
processos de administrador (processo elevado, Passo 5); break-ui das duas telas novas.

## Passo 5 — MVP 1 (d): Event Viewer e Integridade do Windows ✅

- [x] Event Viewer Analyzer (System, Application, Setup, WHEA, Windows Update) com agrupamento por log/origem/id e recorrência em janela deslizante de 24h + persistência automática em `EventSummary` no SQLite
- [x] Integridade do Windows **não destrutiva**: executores e parsers para `DISM /CheckHealth` (rápido) e `/ScanHealth` (profundo), `SFC /VERIFYONLY` (UTF-16LE) e `CHKDSK <drive>:` somente leitura (OEM 850)
- [x] Processo Elevado Mínimo (`LeccorWindowsHealthCenter.Elevated.exe`): manifesto com `requireAdministrator`, Named Pipe IPC com ACL restritiva (CurrentUser, BuiltinAdmins, SYSTEM), token criptográfico de handshake de 256 bits, validação de PID do cliente via `GetNamedPipeClientProcessId`, allowlist estrita de executáveis em System32 e flags permitidas, telemetria de confiabilidade WMI de SSD/NVMe (`MSFT_StorageReliabilityCounter`) e encerramento seguro com proteção de PID
- [x] Regras de diagnóstico novas:
  - Event Viewer: `eventviewer.disk-io-errors` (Disk 153/7/11, NTFS 55/137), `eventviewer.gpu-driver-crash` (TDR Display 4101), `eventviewer.hardware-whea` (WHEA 17/18/19), `eventviewer.unexpected-shutdown` (Kernel-Power 41 / EventLog 6008), `eventviewer.app-crash` (Application Error 1000)
  - Integridade: `windows.component-store-corrupted`, `windows.system-files-corrupted`, `windows.file-system-errors`
- [x] Tela de Resultado Completa (seções 44 e 73 do PROMPT): resumo visual com Score, anel de nota e classificação geral, badges semânticos de contagem por severidade (Crítico, Alto, Médio, Baixo, Informativo), card de destaque do "Problema mais importante" com botão [Investigar], e botões de investigação contextual em cada finding direcionando à aba correspondente
- [x] Telas e ViewModels:
  - `WindowsView` e `WindowsViewModel`: verificações dos 3 pilares com console de saída e status de integridade
  - `EventsView` e `EventsViewModel`: visualizador Master-Detail com contadores das últimas 24h, filtros e botão para abrir `eventvwr.msc`
  - `HistoryView` e `HistoryViewModel`: carregamento e navegação pelos laudos anteriores salvos no SQLite
- [x] Testes: 332 unitários, 38 integração (fontes reais de eventos, armazenamento e processos), 27 sistema (varredura completa e ViewModels) — total de **397 testes automatizados aprovados (100%)**

## Passo 6 — MVP 2 (a): Explorer, Pastas lentas, Serviços e Drivers ✅

- [x] Windows Explorer (seção 16 do PROMPT):
  - Varredura de extensões de Shell registradas (`HKCR\*\shellex`, `Directory\shellex`, `Folder\shellex`, etc.), resolvendo CLSID em `InprocServer32`, lista de bloqueios (`Shell Extensions\Blocked`), DLL física, fabricante e assinatura Authenticode/Catálogo
  - Monitoramento e medição do cache de ícones e miniaturas (`IconCache.db`, `thumbcache_*.db`) em `%LocalAppData%\Microsoft\Windows\Explorer`
- [x] Detector de Pastas Lentas (seção 17 do PROMPT):
  - Ferramenta interativa de benchmark com medição precisa de tempo de enumeração e leitura de metadados (`GetFileAttributesEx`/`FileInfo`)
  - Detecção determinística de gargalos: > 3.000 arquivos na raiz, caminhos de rede (UNC/SMB), provedores em nuvem (OneDrive/Dropbox) e lentidão de metadados
- [x] Inventário e Diagnóstico de Serviços (seção 20 do PROMPT):
  - Extração via registro `SYSTEM\CurrentControlSet\Services` correlacionada com WMI `Win32_Service` para PID e estado de execução (~100 ms)
  - Identificação de serviços com inicialização automática cujo executável foi removido (órfãos) e falhas registradas pelo Service Control Manager (Event IDs 7000, 7009, 7024, 7031, 7034)
- [x] Dispositivos e Drivers (seção 25 do PROMPT):
  - Inventário PnP via `Win32_PnPEntity` e `Win32_PnPSignedDriver` com código de erro de hardware (`ConfigManagerErrorCode`) traduzido para mensagens amigáveis em pt-BR
  - Identificação de dispositivos com falha (Código 10, 43, etc.) e dispositivos sem driver / Código 28 (Dispositivo Desconhecido)
- [x] Regras de Diagnóstico Novas (6 regras, totalizando 27 regras na solução):
  - `explorer.missing-shell-extension` e `explorer.unsigned-shell-extension`
  - `services.missing-executable` e `services.failed-startup`
  - `drivers.device-error` e `drivers.missing-driver`
- [x] Telas e ViewModels integrados à navegação principal:
  - `ExplorerView` e `ExplorerViewModel`: abas de Extensões de Shell, Benchmark de Pastas Lentas e Caches do Explorer
  - `ServicesView` e `ServicesViewModel`: catálogo mestre-detalhe de serviços com busca, filtros por status e dependências
  - `DriversView` e `DriversViewModel`: catálogo de dispositivos com filtro de problemas e detalhes técnicos do driver
- [x] Testes: 355 unitários, 42 integração (fontes reais do Windows), 27 sistema (varredura completa com 12 scanners) — total de **424 testes automatizados aprovados (100%)**

## Passo 7 — MVP 2 (b): Aplicativos, Rede, Windows Update e Limpeza segura (visualização) ✅

- [x] Aplicativos Instalados (seção 28 do PROMPT):
  - Inventário unificado de programas Win32 (HKCU, HKLM 64 e WOW6432Node) e pacotes da Microsoft Store (UWP/MSIX), com nome, versão, fabricante, data de instalação e tamanho estimado
  - Diagnóstico de desinstaladores quebrados/órfãos (`UninstallString` aponta para arquivo ausente no disco) e detecção de versões duplicadas instaladas
  - Regra estrita: nunca desinstalar automaticamente
- [x] Rede e Conectividade (seção 27 do PROMPT):
  - Inventário completo de adaptadores físicos e virtuais (Ethernet, Wi-Fi, VPN, Docker, WSL, Hyper-V, Loopback) com status, velocidade, MAC, IPv4, IPv6, Gateway e DNS
  - Diagnóstico não invasivo com cronometragem precisa: latência do gateway local via Ping, tempo de resolução DNS (`Dns.GetHostAddressesAsync`), latência para IPs públicos confiáveis e taxa de perda de pacotes
- [x] Windows Update (seção 26 do PROMPT):
  - Verificação de status dos serviços essenciais (`wuauserv`, `bits`, `dosvc`)
  - Detecção atômica de reinicializações pendentes via registro (`CBS\RebootPending`, `WindowsUpdate\Auto Update\RebootRequired`, `PendingFileRenameOperations`)
  - Histórico de atualizações instaladas (hotfixes via `Win32_QuickFixEngineering`) e falhas recentes nas últimas 24h
- [x] Limpeza Segura — Visualização e Auditoria (seção 34 do PROMPT):
  - Categorização em 3 níveis de risco estritos: **Seguro (Safe)** (temporários antigos, caches de miniaturas, relatórios WER e otimização de entrega), **Revisar (Review)** (caches de navegadores, downloads antigos e logs residuais) e **Avançado (Advanced)** (MEMORY.DMP e backups do WinSxS)
  - Medição de bytes e contagem de arquivos sem alterar ou apagar absolutamente nada
  - Card explícito de modo somente leitura, informando que a execução com rollback será habilitada no Passo 8 (MVP 3)
- [x] Regras de Diagnóstico Novas (6 regras, totalizando 33 regras na solução):
  - `apps.orphaned-uninstall` e `apps.duplicate-install`
  - `network.no-gateway` e `network.dns-slow`
  - `windowsupdate.service-disabled` e `windowsupdate.reboot-pending`
- [x] Telas e ViewModels integrados à navegação principal:
  - `AppsView` e `AppsViewModel`: inventário mestre-detalhe com filtros, busca instantânea e atalhos de desinstalação
  - `NetworkView` e `NetworkViewModel`: painel de diagnóstico em tempo real e lista de interfaces
  - `WindowsUpdateView` e `WindowsUpdateViewModel`: status de serviços, alerta de reboot pendente e histórico de hotfixes
  - `SafeCleanView` e `SafeCleanViewModel`: auditoria categorizada de espaço liberável por nível de risco
- [x] Testes: 369 unitários, 46 integração (fontes reais de rede, apps e updates), 27 sistema (composição com 15 scanners) — total de **442 testes automatizados aprovados (100%)**

## Passo 8 — MVP 3: Correções reais, Backup, Rollback, Restore Point e Antes/Depois ✅

- [x] Contrato e Infraestrutura de Ponto de Restauração nativo do Windows (seção 38 do PROMPT):
  - `IRestorePointService` em `Application/Repair` e implementação `RestorePointService` em `Infrastructure/Repair` via WMI `root\default:SystemRestore.CreateRestorePoint`.
  - Tratamento resiliente e diagnóstico para quando a Proteção de Sistema estiver desativada no C: ou faltar elevação administrativa.
- [x] Quatro primeiras ações de reparo reais (`IRepairAction`), todas com fluxo seguro de backup prévio obrigatório, validação e histórico:
  - `DisableStartupEntryRepairAction` (`repair.startup.disable`): desativa entradas de inicialização automática no registro através da chave nativa `StartupApproved\Run` (0x03). Risco: Low. Rollback total restaurando o estado original.
  - `DisableShellExtensionRepairAction` (`repair.explorer.disable-shell-extension`): bloqueia extensões órfãs ou lentas do Explorer via chave oficial `HKCU\Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked`. Risco: Low. Rollback total removendo da chave.
  - `SafeCleanTempRepairAction` (`repair.storage.safe-clean`): implementa `IMeasurableRepairAction`. Medição antes e depois em MB. Limpeza segura de arquivos temporários (> 24h) no `User Temp`, `Windows\Temp`, miniaturas e relatórios WER / CrashDumps. Risco: Safe. Tratamento tolerante para arquivos bloqueados.
  - `EnableWindowsUpdateServiceRepairAction` (`repair.windowsupdate.enable-service`): reativa o serviço `wuauserv` no registro de Desativado (4) para Manual (3) e inicia o serviço. Risco: Low. Rollback total restaurando o modo anterior.
- [x] Associação das regras diagnósticas aos reparos automáticos:
  - `WindowsUpdateServiceDisabledRule` associada a `repair.windowsupdate.enable-service`.
  - `ExplorerMissingShellExtensionRule` associada a `repair.explorer.disable-shell-extension`.
  - `StartupMissingExecutableRule` associada a `repair.startup.disable`.
- [x] Interface Completa e ViewModels de Correções:
  - `RepairsViewModel` e `RepairsView`:
    - Aba "Correções Disponíveis": lista de ações com checkboxes, badges de risco, se exige elevação, se suporta rollback e medição antes/depois.
    - Botão "Ponto de Restauração": invoca `IRestorePointService` sob demanda.
    - Botão "Simular (Dry Run)": calcula e detalha as alterações planejadas sem modificar o sistema.
    - Botão "Aplicar Selecionadas": executa em lote com backup atômico prévio e medição antes/depois.
    - Aba "Histórico de Correções & Rollback": histórico das ações gravadas no SQLite com data, motivo, mensagem, comparação antes/depois e botão **[Desfazer]** (Rollback).
  - Substituição de `Planned("Correções", ...)` por `new("Correções", "", repairs)` no `MainViewModel`.
- [x] Testes:
  - 377 testes unitários (novos testes em `RepairActionsTests`).
  - 46 testes de integração.
  - 28 testes de sistema (novo teste de ciclo completo em `RepairsViewModelTests`).
  - **Total de 451 testes automatizados aprovados (100% de sucesso)**.
  - Compilação limpa em Release com `-warnaserror --no-incremental` (0 avisos, 0 erros).

## Passo 9 — MVP 4: Docker, WSL, Ferramentas de desenvolvimento, Crash Analyzer, Segurança ✅

- [x] **Developer Mode & DevTools (Seções 31, 32 e 33 do PROMPT):**
  - `DevToolsProvider` (`IDevToolsProvider`):
    - Detecção de runtimes e compilers instalados: Node.js, npm, Yarn, pnpm, .NET SDKs (inventário e versões), Java/JDK (`%JAVA_HOME%` e PATH), Maven, Gradle, Python e Git.
    - Detecção de IDEs: Visual Studio Code, Visual Studio (via `vswhere`), JetBrains (via Toolbox) e Android Studio.
    - **Docker Analyzer (Seção 32):** detecção de Docker Desktop / Engine, status de execução, backend (WSL2 / Hyper-V) e tamanho total dos discos de dados. Alerta obrigatório: volumes podem conter bancos de dados permanentes e nunca são descartados automaticamente.
    - **WSL Analyzer (Seção 33):** detecção das distribuições WSL instaladas via registro `HKCU\Software\Microsoft\Windows\CurrentVersion\Lxss`, versão (WSL1 vs WSL2), caminho dos discos virtuais `ext4.vhdx` e tamanho físico no disco.
    - **Análise do PATH:** contagem de entradas de usuário e do sistema, detecção de caminhos órfãos / pastas inexistentes e verificação de comprimento total.
- [x] **Security Scanner (Seção 30 do PROMPT):**
  - `SecurityProvider` (`ISecurityProvider`):
    - Consulta de produtos antivírus registrados via WMI `root\SecurityCenter2:AntiVirusProduct` com decodificação de `productState` (proteção ativa e definições em dia).
    - Status do Firewall do Windows (`FirewallProduct`).
    - Status do Controle de Conta de Usuário (UAC via `EnableLUA` no registro).
    - Verificação de persistências suspeitas: IFEO (Image File Execution Options) `Debugger` hijack e bibliotecas ativas em `AppInit_DLLs`.
    - Princípio de segurança rigoroso: não substitui o antivírus e não declara malware sem evidência suficiente.
- [x] **Crash Analyzer (Seção 29 do PROMPT):**
  - `CrashAnalyzerProvider` (`ICrashAnalyzerProvider`):
    - Varredura de arquivos de despejo no disco: minidumps do kernel (`%SystemRoot%\Minidump\*.dmp`), despejo completo (`MEMORY.DMP`) e dumps de aplicações de usuário (`%LocalAppData%\CrashDumps\*.dmp`).
    - Agregação dos últimos 30 dias: eventos de Application Error (Event ID 1000), Application Hang (Event ID 1002) e eventos críticos de BSOD / BugCheck (Event ID 1001) com identificação dos executáveis mais instáveis.
- [x] **Scanners e Regras de Diagnóstico:**
  - 3 novos scanners (totalizando **18 scanners** na solução):
    - `DevToolsScanner` (`devtools`, Ordem: 115, Módulo: `Applications`)
    - `SecurityScanner` (`security`, Ordem: 120, Módulo: `Windows`)
    - `CrashScanner` (`crash`, Ordem: 125, Módulo: `Events`)
  - 6 novas regras de diagnóstico (totalizando **39 regras** na solução):
    - `dev.wsl-vhdx-large`: alerta quando o disco `ext4.vhdx` do WSL2 ultrapassa 50 GB.
    - `dev.path-invalid-entries`: identifica caminhos órfãos ou pastas inexistentes configuradas no PATH.
    - `security.antivirus-disabled`: alerta crítico quando a proteção em tempo real está desativada.
    - `security.uac-disabled`: alerta alto quando o UAC está desligado no registro.
    - `security.ifeo-hijack`: alerta crítico quando binários essenciais têm redirecionamento de Debugger.
    - `crash.kernel-minidump-detected`: alerta sobre ocorrência recente de BSOD e despejos de kernel.
- [x] **Telas e ViewModels no WPF:**
  - `DevToolsViewModel` e `DevToolsView`: painéis dedicados para Docker Desktop, WSL2, runtimes instalados e integridade do PATH.
  - `SecurityViewModel` e `SecurityView`: painel de segurança do Windows Security/Defender, Firewall, UAC e dashboard do Crash Analyzer.
  - Integração no menu principal do `MainViewModel` substituindo placeholders por páginas reais.
- [x] **Testes Automatizados:**
  - 386 testes unitários, 46 testes de integração, 30 testes de sistema — total de **462 testes aprovados (100% de sucesso)**.
  - Compilação limpa em Release com `-warnaserror --no-incremental` (0 avisos, 0 erros).

## Passo 10 — MVP 5: Relatórios avançados, Agendamento, Notificações, Instalador e UX ✅

- [x] **Relatórios Avançados (Seções 54 e 55 do PROMPT):**
  - `HtmlReportExporter` (`IReportExporter`): laudo em HTML autocontido com visual profissional, temas claro/escuro, responsivo e estilos de impressão `@media print`.
  - `CsvReportExporter`: exportação tabular de achados e métricas em CSV com codificação UTF-8 com BOM para Excel.
  - `PdfReportExporter`: exportador nativo em conformidade com o padrão PDF 1.4, com paginação automática, cabeçalhos, cartões e badges coloridos por severidade, sem dependências externas.
  - `JsonReportExporter`: exportador completo já em produção.
  - `ReportsViewModel` e `ReportsView`: tela dedicada com histórico de laudos, seleção de formatos e botões para exportação em arquivo ou abertura imediata.
- [x] **Persistência de Preferências do Usuário (Seção 66 do PROMPT):**
  - `IConfigurationRepository` e `SqliteConfigurationRepository`: persistência chave-valor assíncrona na tabela `Configuration` do SQLite (`ui.theme`, `ui.advanced-mode`, `schedule.frequency`).
  - Carregamento automático na inicialização e sincronização imediata com `ThemeService` e `UiPreferences`.
- [x] **Agendamento via Task Scheduler & Notificações (Seção 60 do PROMPT):**
  - `IScanSchedulerService` e `WindowsTaskSchedulerService`: agendamento e gerenciamento determinístico de tarefas via `schtasks.exe` (Diário, Semanal, Mensal ou Desativado).
  - Execução silenciosa em segundo plano através dos argumentos CLI `--scheduled-scan` e `--background-scan` no `App.xaml.cs`.
  - `INotificationService` e `WindowsNotificationService`: notificações locais e logging estruturado.
  - Painel de controle de agendamento na tela de Configurações (`SettingsView`).
- [x] **Instalador e Modo Portable (Seções 61 e 62 do PROMPT):**
  - Script PowerShell `installer/package-installer.ps1` com ações automatizadas de `Build`, `Install`, `Uninstall` e `Repair`.
  - Script Inno Setup `installer/installer.iss` para instalador gráfico x64 moderno em Português do Brasil.
  - Regra de ouro da desinstalação segura: remove todos os arquivos do aplicativo preservando relatórios e backups do usuário em `%ProgramData%\LeccorWindowsHealthCenter` (limpeza completa apenas sob confirmação via `-PurgeUserData`).
  - Suporte ao modo portable através do arquivo `portable.flag`.
- [x] **Compilação e Publicação win-arm64 (Seção 4 do PROMPT):**
  - Preparação e suporte em `scripts/build.ps1` e `installer/package-installer.ps1` para target `win-arm64`.
- [x] **Testes Automatizados e Qualidade:**
  - 396 testes unitários, 46 testes de integração, 32 testes de sistema — total de **474 testes automatizados aprovados (100% de sucesso)**.
  - Compilação limpa em Release com `-warnaserror --no-incremental` (0 avisos, 0 erros).

---

## Evolução Futura — Melhorias e Otimizações Viáveis (Pós-v1.0.0) ⬜

Itens mapeados na análise técnica de viabilidade como totalmente possíveis de implementar em modo usuário e sem dependência de drivers de kernel, organizados por pacotes funcionais:

### Pacote A — Telemetria Avançada e Medição Precisa ✅
- [x] **Separação de E/S por processo (Disco vs Rede):** Implementada sessão ETW (*Event Tracing for Windows*) em tempo real via `Microsoft.Diagnostics.Tracing.TraceEvent` com `IProcessIoTracer` e `EtwProcessIoTracer`. Discriminação precisa de bytes de disco (`DiskIoBytes`) e rede (`NetworkIoBytes`) com cálculo de taxas em `ProcessRates`.
- [x] **Impacto de Inicialização baseado no tempo real de boot:** Integrada telemetria do canal `Microsoft-Windows-Diagnostics-Performance/Operational` via `IBootPerformanceProvider` e `BootPerformanceProvider` (Event IDs 101 a 110). Medição de degradação real em milissegundos (`BootDegradationMs`) e exibição enriquecida na UI ("Alto (3.2s no boot)").
- [x] **Histórico contínuo de métricas com o aplicativo fechado:** Criada a entidade `MetricSampleRecord`, interface `IMetricSampleRepository` e migration `006_MetricSamples.sql` no SQLite. Amostragem contínua em `MetricsHistoryService` e persistência via `--background-scan` com retenção automática de 7 dias.
- [x] **Verificação online de revogação de certificados (CRL/OCSP):** Implementada checagem opcional em `FileIdentityProvider` passando `WTD_REVOKE_WHOLECHAIN` para `WinVerifyTrust`. Adicionado status `SignatureStatus.Revoked`, toggle no Modo Avançado em `SettingsView` e regra `ExplorerUnsignedShellExtensionRule` com severidade `High` para certificados revogados.
- [x] **Testes Automatizados:** 406 testes unitários, 46 testes de integração, 32 testes de sistema — total de **484 testes automatizados aprovados (100% de sucesso)**.
- [x] Compilação limpa em Release com `-warnaserror --no-incremental` (0 erros, 0 avisos).

### Pacote B — Otimizações de Armazenamento e Persistência ✅
- [x] **Persistência da Análise de Espaço em Disco no SQLite:**
  - Adicionada Migration 3 (`SpaceAnalysisSnapshot`) com tabela indexada por `(VolumeRoot, CapturedAt DESC)`.
  - Criada interface `ISpaceAnalysisSnapshotRepository` e repositório `SqliteSpaceAnalysisSnapshotRepository` com retenção automática dos últimos 10 snapshots por volume.
  - Integração no `StorageViewModel`: restauração assíncrona automática em ~10ms do último snapshot salvo no SQLite ao abrir a tela (com status claro e navegação imediata) e persistência em segundo plano após cada análise.
- [x] **Deduplicação de Hard Links na Análise de Espaço:**
  - P/Invoke Win32 para `CreateFileW` e `GetFileInformationByHandle` em `FileSystemSpaceAnalyzer`.
  - Identificação única por `(VolumeSerialNumber, FileIndexHigh, FileIndexLow)` com rastreamento concorrente via `_seenHardLinks` para arquivos com `NumberOfLinks > 1`.
  - Evita contagem física repetida de bibliotecas e componentes compartilhados em `WinSxS` e pastas do sistema, acumulando os bytes economizados em `FolderNode.HardLinkDeduplicatedBytes` e exibindo a nota deduplicada na UI.
- [x] **Exportação Dedicada da Árvore de Espaço em Disco (CSV e JSON):**
  - Implementado `ISpaceAnalysisExporter` e `SpaceAnalysisExporter` no projeto Reporting.
  - Exportação em CSV tabular com cabeçalho, UTF-8 com BOM (`\uFEFF`) e delimitador `;` para compatibilidade total com Excel pt-BR, cobrindo diretórios, maiores arquivos e arquivos antigos.
  - Exportação em JSON estruturado legível e indentado.
  - Botões integrados na barra de análise de espaço em `StorageView.xaml` com comandos reativos `ExportCsvCommand` e `ExportJsonCommand` vinculados a `SaveFileDialog` através de `IFolderPicker.PickSaveFile`.
- [x] **Testes Automatizados e Qualidade:**
  - 412 testes unitários, 47 testes de integração, 32 testes de sistema — total de **491 testes automatizados aprovados (100% de sucesso)**.
  - Compilação limpa em Release com `-warnaserror --no-incremental` (0 erros, 0 avisos).

### Pacote C — Performance e UX da Interface ⬜
- [ ] **Virtualização de UI para agrupamentos em listas grandes (> 500 itens):** Configurar `VirtualizingPanel.IsVirtualizingWhenGrouping="True"` e modo de reciclagem nas telas de Inicialização e Processos para carregamento instantâneo sem congelamento de renderização.

