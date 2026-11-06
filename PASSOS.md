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

## Passo 6 — MVP 2 (a): Explorer, Pastas lentas, Serviços e Drivers ⬜

## Passo 7 — MVP 2 (b): Aplicativos, Rede, Windows Update e Limpeza segura (visualização) ⬜

## Passo 8 — MVP 3: Correções reais, Backup, Rollback, Restore Point e Antes/Depois ⬜

- Primeiras `IRepairAction` reais (desativar startup, desativar shell extension, limpeza SAFE), cada uma com rollback testado
- Telas Correções ("Correções seguras — N disponíveis"), Histórico de Correções com **Desfazer**, Dry Run na UI
- Restore Point para ações de maior impacto

## Passo 9 — MVP 4: Docker, WSL, Ferramentas de desenvolvimento, Crash Analyzer, Segurança ⬜

## Passo 10 — MVP 5: Relatórios avançados, Agendamento, Notificações, Instalador e UX ⬜

- Relatórios HTML (visual profissional), CSV e PDF
- Agendamento via Task Scheduler (opcional), notificações
- Persistência das preferências do usuário (tabela `Configuration`)
- Instalador (instalar/atualizar/reparar/desinstalar preservando backups e relatórios)
- Build `win-arm64`
