# PASSOS — Plano de execução

Regras deste plano (definidas no PROMPT.txt, seção 69):

- Os passos são executados **um por vez**.
- Ao final de cada passo: build sem avisos, todos os testes passando, `CONTEXTO.md` e `DOCUMENTACAO.md` atualizados e **texto de commit** entregue.
- **Nunca** avançar para o próximo passo sem autorização explícita do usuário.
- Commits são feitos manualmente pelo usuário (o agente não executa `git add`/`git commit`).

Legenda: ✅ concluído · ⏳ aguardando autorização · ⬜ não iniciado

---

## Passo 1 — Fundação da arquitetura ✅ (aguardando autorização para o Passo 2)

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

## Passo 2 — MVP 1 (a): Hardware, CPU, Memória e GPU ⬜

- Scanner de hardware (WMI/CIM: sistema, placa-mãe, BIOS, CPU, RAM instalada, GPUs) + `HardwareSnapshot` no banco
- Scanner de CPU com amostragem temporal (utilização, frequência, núcleos/threads, top processos, throttling quando detectável)
- Scanner de memória (instalada/em uso/committed/cached/paged/non-paged/page file/hard faults; top processos)
- Scanner de GPU (fabricante, VRAM, driver e versão; utilização via contadores "GPU Engine" quando disponíveis) + dashboard passa a exibir GPU
- Regras com thresholds justificados (CPU sustentada, paginação excessiva, memória comprometida alta, possível leak por crescimento contínuo)
- Telas Performance e Hardware com gráficos históricos
- Documentar cada scanner em `docs/DIAGNOSTICS.md`

## Passo 3 — MVP 1 (b): Discos e Armazenamento ⬜

- Discos físicos (modelo, tipo NVMe/SSD/HDD, capacidade, partições, TRIM) e saúde SMART/NVMe **somente quando o dispositivo fornecer** ("Saúde: não disponível" caso contrário)
- Análise de espaço (árvore navegável das maiores pastas, maiores arquivos, temporários, caches) com concorrência limitada e cancelamento
- Regras: pouco espaço livre, SMART com atributos críticos, temperatura alta (quando disponível)
- Tela Armazenamento

## Passo 4 — MVP 1 (c): Processos e Inicialização ⬜

- Process Manager (PID, CPU, RAM, disco, threads, handles, usuário, executável, fabricante, assinatura; árvore; pesquisa; proteção de processos críticos)
- Startup Manager **somente leitura** (Run/RunOnce HKCU/HKLM, pastas de Startup, Scheduled Tasks de logon, serviços automáticos) com impacto estimado
- Regras: entradas apontando para executáveis inexistentes, executáveis em diretórios temporários
- Telas Processos e Inicialização

## Passo 5 — MVP 1 (d): Event Viewer e Integridade do Windows ⬜

- Event Viewer Analyzer (System, Application, Setup, WHEA, Windows Update, Kernel, Disk, NTFS) com agrupamento e recorrência em 24h + `EventSummary` no banco
- Integridade do Windows **não destrutiva**: `DISM /CheckHealth` e `/ScanHealth`, `SFC /VERIFYONLY`, `CHKDSK` somente leitura — via `ICommandExecutor`, exibindo comandos e resultado
- Requer elevação → este passo implementa o **processo elevado mínimo** (`LeccorWindowsHealthCenter.Elevated.exe`) com IPC autenticado, apenas para essas verificações
- Tela de resultado completa (seção 44/73) e Histórico de varreduras

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
