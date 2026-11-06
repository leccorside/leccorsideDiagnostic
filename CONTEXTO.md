# CONTEXTO — Leccor Windows Health Center

Resumo vivo do projeto para retomar o trabalho em qualquer sessão. **Atualizar ao final de cada ação.**
Requisitos completos: `PROMPT.txt` (fonte da verdade). Plano: `PASSOS.md`. Referência técnica: `DOCUMENTACAO.md`.

Última atualização: **06/11/2026 — Pacote C concluído: Performance e UX da Interface (Virtualização de UI com agrupamento e reciclagem na Inicialização, estabilidade de rolagem/seleção em Processos e recuo progressivo profundo até 16 níveis no modo árvore).**

---

## Estado atual

- Solution `LeccorWindowsHealthCenter.sln` com 10 projetos em `src/` (incluindo `LeccorWindowsHealthCenter.Elevated`) e 3 de teste.
- .NET SDK 10.0.103, WPF, CommunityToolkit.Mvvm 8.4.0, Microsoft.Extensions.* 10.0.11, Microsoft.Data.Sqlite 10.0.11,
  Serilog 4.3.1 (+ Extensions.Logging 10.0.0, Sinks.File 7.0.0, Formatting.Compact 3.0.0), System.Management 10.0.11,
  System.Diagnostics.EventLog 10.0.11, Microsoft.Extensions.Hosting.Abstractions 10.0.11, xUnit 2.9.3.
- Infraestrutura de diagnóstico completa e testada: orquestrador, RuleEngine, RiskAnalyzer, ScoringEngine,
  RepairEngine com **4 ações reais de reparo**, Ponto de Restauração nativo do Windows, executor de comandos seguro, SQLite com migrations, backups atômicos, logs.
- **Scanners em produção (18 scanners):**
  1. `hardware` (Ordem 10, Hardware)
  2. `cpu` (Ordem 20, Performance)
  3. `memory` (Ordem 30, Performance)
  4. `gpu` (Ordem 40, Hardware)
  5. `disks` (Ordem 50, Disks)
  6. `storage` (Ordem 60, Storage)
  7. `windows.integrity` (Ordem 65, Windows)
  8. `startup` (Ordem 70, Startup)
  9. `eventviewer` (Ordem 75, Events)
  10. `explorer` (Ordem 80, Explorer)
  11. `services` (Ordem 85, Services)
  12. `drivers` (Ordem 90, Drivers)
  13. `apps` (Ordem 95, Applications)
  14. `network` (Ordem 105, Network)
  15. `windowsupdate` (Ordem 110, WindowsUpdate)
  16. `devtools` (Ordem 115, Applications)
  17. `security` (Ordem 120, Windows)
  18. `crash` (Ordem 125, Events)
- **Regras em produção:** 39 regras no total.
  - Regras com ações de reparo associadas:
    - `windowsupdate.service-disabled` → `repair.windowsupdate.enable-service`
    - `explorer.missing-shell-extension` → `repair.explorer.disable-shell-extension`
    - `startup.missing-executable` → `repair.startup.disable`
  - Novas regras do Passo 9:
    - `dev.wsl-vhdx-large`: alerta quando o disco `ext4.vhdx` do WSL2 ultrapassa 50 GB.
    - `dev.path-invalid-entries`: identifica caminhos órfãos ou pastas inexistentes configuradas no PATH.
    - `security.antivirus-disabled`: alerta crítico quando a proteção em tempo real está desativada.
    - `security.uac-disabled`: alerta alto quando o UAC está desligado no registro.
    - `security.ifeo-hijack`: alerta crítico quando binários essenciais têm redirecionamento de Debugger.
    - `crash.kernel-minidump-detected`: alerta sobre ocorrência recente de BSOD e despejos de kernel.
- **Ações de Reparo Reais em Produção (4 ações):**
  1. `DisableStartupEntryRepairAction` (`repair.startup.disable`): desativação de inicialização via `StartupApproved\Run` (0x03) preservando o comando original. Risco: Low. Rollback total.
  2. `DisableShellExtensionRepairAction` (`repair.explorer.disable-shell-extension`): bloqueio de extensão órfã do Explorer via `Shell Extensions\Blocked`. Risco: Low. Rollback total.
  3. `SafeCleanTempRepairAction` (`repair.storage.safe-clean`): limpeza segura de temporários (> 24h) com medição antes/depois em MB (`IMeasurableRepairAction`). Risco: Safe. Tolerante a arquivos em uso.
  4. `EnableWindowsUpdateServiceRepairAction` (`repair.windowsupdate.enable-service`): reativação do serviço `wuauserv` no registro de Desativado (4) para Manual (3) e início do serviço. Risco: Low. Rollback total.
- **Ponto de Restauração Nativo (`IRestorePointService`):**
  - Implementado em `RestorePointService` via WMI `root\default:SystemRestore.CreateRestorePoint`. Suporte e diagnóstico para proteção de sistema desativada no C: ou falta de elevação.
- **Relatórios Avançados (4 exportadores em produção):**
  - `HtmlReportExporter`: laudo HTML autocontido com layout profissional, paleta acessível e regras de impressão `@media print`.
  - `PdfReportExporter`: documento PDF 1.4 estruturado, nativo e sem dependências externas.
  - `CsvReportExporter`: exportação tabular de achados e métricas para Excel (UTF-8 com BOM).
  - `JsonReportExporter`: exportação estruturada em JSON.
- **Agendamento e Notificações:**
  - `WindowsTaskSchedulerService`: agendamento e gerenciamento via `schtasks.exe` (Diário, Semanal, Mensal ou Desativado).
  - Suporte CLI `--scheduled-scan` e `--background-scan` para execução silenciosa em segundo plano.
  - `WindowsNotificationService`: notificações nativas e logging integrado.
- **Persistência de Configurações:**
  - `SqliteConfigurationRepository` na tabela `Configuration` do SQLite (`ui.theme`, `ui.advanced-mode`, `schedule.frequency`).
- **Instalador e Modo Portable:**
  - Script `installer/package-installer.ps1` com ações `Build`, `Install`, `Uninstall` e `Repair`.
  - Script Inno Setup `installer/installer.iss` com instalador x64 moderno em Português do Brasil.
  - Preservação obrigatória de dados do usuário (`%ProgramData%\LeccorWindowsHealthCenter`).
  - Suporte a modo portable através do arquivo `portable.flag`.
- **Telas ativas (100% das 22 telas do menu lateral ativas):** Dashboard, Varredura (resultado completo, card "Problema mais importante", badges e botões "Investigar"), Performance, Processos, Armazenamento, Hardware, Drivers, Inicialização, Serviços, Aplicativos, Windows, Windows Update, Eventos, Rede, Limpeza (Safe Cleaner), Correções (Central de Correções com abas de Correções Disponíveis, Dry Run, Ponto de Restauração e Histórico de Correções com Rollback ativo), Desenvolvimento, Segurança, Histórico, **Relatórios** (nova: Central de Laudos e Exportação HTML/PDF/CSV/JSON) e **Configurações** (com agendamento via Task Scheduler e persistência em banco).
- Build Debug e Release com `-warnaserror` e `--no-incremental`: 0 avisos, 0 erros.
- Testes: **505 testes aprovados (100% de sucesso)** — 412 unitários, 47 integração, 46 sistema.

## Histórico de implementações

### Passo 1 — Fundação (17/10/2026)

- Estrutura da seção 4 do PROMPT (`src/`, `tests/UnitTests|IntegrationTests|SystemTests`, `docs/`, `scripts/`, `installer/`).
- Domain com o modelo `Finding` da seção 67 (inclui `Confidence` 0–100% para separar confirmado de possível) e
  `Observation` (fatos não numéricos) além de `Metric`.
- Decisão: **scanner só coleta, regra decide**. `DiagnosticResult.Findings` é preenchido pelo orquestrador após o
  RuleEngine. Motivo: seção 47 (regras centralizadas) e seção 5 (scanner separado de diagnóstico/reparo).
- `IDiagnosticScanner` ganhou `Id`, `Module`, `IncludedInQuickScan` e `Order` além do `Name`/`ScanAsync` da seção 10
  (necessários para varredura rápida/personalizada e ordem da seção 73).
- `IRepairAction` estendido com `CancellationToken`, `PlanAsync` (Dry Run), `CaptureStateAsync` (backup),
  `ValidateAsync` e `RollbackAsync(BackupRecord)`; `IMeasurableRepairAction` para antes/depois.
- `RiskAnalyzer` só eleva risco: sem rollback ⇒ ≥ MEDIUM; reinício ⇒ ≥ MEDIUM; admin ⇒ ≥ LOW.
- `ScoringEngine`: só categorias medidas entram; Info não deduz; confiança pondera; repetição da mesma regra com
  retorno decrescente (máx. 2× o maior peso); sem cobertura ⇒ `Overall = null` ("SEM DADOS").
- Persistência com **Microsoft.Data.Sqlite + migrations próprias** (não EF Core): menos RAM/tamanho, controle total
  do SQL, migrations simples de auditar.
- Logging: aplicação usa só `ILogger<T>`; Serilog é o provider (rotação diária + por tamanho, retenção,
  JSON compacto, enricher de redação).
- `CommandExecutor`: `ArgumentList`, allowlist de diretórios, bloqueio de interpretadores (cmd/powershell/...),
  Job Object `KILL_ON_JOB_CLOSE`, timeout/cancelamento com `Kill(entireProcessTree)`, saída limitada e redigida.
- `FileBackupStore`: JSON atômico + ACL protegida (SYSTEM, Administradores, usuário atual).
- `AppPaths.ResolveWritable`: `%ProgramData%` com fallback para `%LocalAppData%`; modo portable via `portable.flag`.
- UI: Generic Host com `ValidateOnBuild`; temas por `ResourceDictionary` trocado em runtime (`ThemeService`) +
  barra de título escura via `DwmSetWindowAttribute(20)`; controles próprios `Sparkline` e `RingGauge` (sem
  bibliotecas de gráfico); animação discreta de troca de página.
- Logos: `logo2.png` no tema claro, `logo1.jpg` no escuro; `logo-mark.png`/`app.ico` gerados recortando o símbolo
  da `logo2.png` (System.Drawing, ICO multi-tamanho 16–256 px).
- CI: `.github/workflows/ci.yml` (restore, build `-warnaserror`, testes com cobertura, publish win-x64 self-contained).
- `.gitignore` revisado após o `git init` (conferido com `git status --ignored`): além de `bin/`/`obj/`, ignora
  `*.binlog`, pacotes NuGet, Rider (`.idea/`, `*.DotSettings.user`), resultados de teste/cobertura, arquivos
  SQLite/logs gerados fora de `bin/`, `appsettings.Local.json`, `.claude/settings.local.json`, arquivos do Windows
  e segredos. Os 141 arquivos do projeto seguem versionáveis.

### Polimento de UI com impeccable (17/10/2026)

- **Regra do projeto (pedido do usuário):** toda mudança de interface é revisada com o skill `impeccable`
  (contexto → playbook `polish` → `craft-floor` → rodada visual nos dois temas → no máximo uma rodada de confirmação).
- ScrollBar própria estilo Windows 11 (sem setas, trilho transparente, polegar 4 px → 7 px no hover), vertical e horizontal,
  com `ScrollThumbBrush`/`ScrollThumbHoverBrush` por tema — antes aparecia o estilo clássico claro no tema escuro.
- CheckBox, RadioButton e Expander com templates próprios (chevron Fluent animado), seleção/cursor de `TextBox` com a paleta.
- Cores semânticas (`CriticalBrush`...`OkBrush`) movidas para `Light.xaml`/`Dark.xaml` com contraste ≥ 4,5:1 calculado nos
  dois fundos; aplicadas por estilos com gatilho em `Tag` (`SemanticText`, `SeverityDot`, `ScoreRing`) e `DynamicResource`.
  O `SemanticBrushConverter` foi removido.
- `AccentBrush` do tema claro: `#E8650F` → `#C2520A` (texto branco do botão principal passou de 3,33:1 para 4,66:1).
- Ícones de status do progresso: caracteres Unicode (✓ → ✗ ○) trocados por glifos Segoe Fluent, com nome acessível.
- "Recomendações" no dashboard em cinza (mesma cor das recomendações na lista de achados).
- Botões quebram linha em vez de cortar o rótulo; cartões de modo sem altura fixa (botão no rodapé); ações do card de score
  em linha própria.

## Erros e correções conhecidas

| Erro | Causa | Correção |
| --- | --- | --- |
| Teste de redação falhou: `Authorization: Bearer <token>` vazava o token | A regra chave=valor mascarava só a palavra "Bearer" | Regra dedicada para `Authorization` que preserva o esquema (Bearer/Basic) e mascara a credencial, aplicada antes das demais; casos `Basic` e `Bearer` avulso adicionados aos testes |
| Risco de "pingue-pongue" de seleção na sidebar | Dois `ListBox` (menu e rodapé) ligados ao mesmo `SelectedItem`: um empurraria `null` para o outro | Seleções separadas (`SelectedMainItem`/`SelectedFooterItem`) e `CurrentPage` segue a última seleção não nula |
| `Rollback` como nome de tabela | É palavra-chave do SQLite | Identificador entre aspas (`"Rollback"`) na migration e no INSERT |
| SystemTests não compilava (`Path`/`File` inexistentes) | Projetos com `UseWPF` não incluem `System.IO` nos usings implícitos | `using System.IO;` explícito |
| Botão "Executar varredura completa" cortado no Dashboard | Coluna do card de score estreita | Colunas 6*/5* e anel de 140 px |
| `MSB3061 ... _wpftmp.csproj.nuget.dgspec.json being used by another process` | Arquivo temporário do build WPF travado momentaneamente (antivírus/extensão do editor) | Transitório — repetir o build. Documentado em `docs/DEVELOPMENT.md` |
| ScrollBar, CheckBox, RadioButton e Expander com aparência clássica clara no tema escuro | Controles sem template próprio usam o tema do sistema do WPF | Templates próprios em `Styles.xaml` com pincéis por tema |
| Cores de severidade/rating ficavam com a cor do tema antigo após trocar o tema | `SemanticBrushConverter` resolvia o pincel uma única vez (não reage a `DynamicResource`) | Estilos com gatilho em `Tag` + `DynamicResource` |
| Contraste insuficiente: âmbar `#E3A300` como texto (~2:1) e branco sobre `#E8650F` (3,33:1) no tema claro | Cores escolhidas pela marca, sem medir | Paleta semântica por tema medida (≥ 4,5:1) e `AccentBrush` claro `#C2520A` |
| Rótulo "Executar varredura completa" e descrição da varredura rápida cortados em janelas ~1100 px | Botão sem quebra de linha e `Height="54"` fixo na descrição | Quebra de linha nos botões, cartões com `DockPanel` e ações do score em linha própria |
| Uma inicialização levou ~30–60 s até a primeira linha de log (PID 18728, 17/10/2026 ~17:04–17:05) | **Não identificada.** Ver "Investigação: inicialização lenta" abaixo | Não reproduzida em 62 aberturas; instrumentação de fases + watchdog adicionados para capturar a fase exata se voltar |
| Saída de ferramentas de console com acentos corrompidos (ex.: `hostname -x`) | Ferramentas usam a code page OEM | Não afeta o Passo 1; tratar `StandardOutputEncoding` por ferramenta ao implementar DISM/SFC (Passo 5) |

### PRODUCT.md via `/impeccable init` (17/10/2026)

- Criado `PRODUCT.md` (contexto de produto lido pelo impeccable em toda revisão de UI). Confirmado com o usuário:
  **usuário principal = desenvolvedor / power user**; **distribuição = uso interno / clientes próprios**;
  **idioma = pt-BR agora, inglês depois** (textos de UI devem ficar prontos para localização).
- Mantida a exigência do PROMPT: resultados compreensíveis para usuário comum no modo padrão; detalhe técnico no modo avançado.
- Limitação do impeccable: só reconhece plataformas `web`, `ios`, `android`, `adaptive`. O campo ficou `windows` (verdadeiro);
  a ferramenta avisa e trata como `web`. Ignorar orientações específicas de navegador/CSS e o `impeccable detect`
  (não analisa XAML) — a verificação real é visual nos dois temas + contraste medido.
- O impeccable não reconhece XAML como "implementação visual existente": sem `DESIGN.md`, telas novas (Passo 2) seriam
  tratadas como projeto do zero. Próximo passo recomendado: `/impeccable document` para registrar o sistema visual atual.
- Em aberto: quem são exatamente os "clientes próprios" (operadores técnicos ou usuários finais) e o modelo de suporte/atualização.

### DESIGN.md via `/impeccable document` (17/10/2026)

- `DESIGN.md` (raiz) registra o sistema visual existente: tokens normativos no frontmatter (50 cores dos dois temas,
  10 papéis tipográficos, 4 raios, 6 espaçamentos, 15 variantes de componente) e as 8 seções canônicas.
- Decisões confirmadas com o usuário: **North Star "O Laudo Técnico"**; acento chamado **Brasa Leccor**;
  **plano em repouso, sombra só no flutuante** (menus, tooltips, diálogos); componentes **táteis e confiantes**;
  **gradiente da logo com uso pontual** (no máximo um elemento de marca por tela, nunca em texto/botões).
- `.impeccable/design.json` (sidecar): escalas tonais OKLCH de 8 passos por cor (geradas por script, croma reduzido só
  quando sai do sRGB), sombra "flutuante" provisória, tokens de movimento, larguras de janela, 8 componentes em HTML/CSS
  e a narrativa copiada literalmente do DESIGN.md.
- Lacunas entre o DESIGN.md e o código: estado **pressionado** dos botões — resolvido no Passo 2. Ainda abertas: a
  sombra "flutuante" não está aplicada a tooltips; o gradiente da marca existe como recurso, mas não é usado.
- Avisos do linter de Markdown no DESIGN.md (H1 duplicado, "Named Rules" repetido, "### Do:") vêm do formato oficial
  e são intencionais.

### Investigação: inicialização lenta (17/10/2026)

- **Sintoma original:** durante a validação visual, o app não mostrou janela em 5 s; um minuto depois ainda sem janela e,
  aparentemente, sem log. A anotação inicial ("sem log, travado") **estava errada**: o log mostra que o PID 18728 gravou
  "Aplicativo iniciado" às 17:05:28 e "Banco de dados pronto" 60 ms depois — eu olhei o log cedo demais. Foi uma
  **demora única de ~30–60 s antes da primeira linha de log**, não um deadlock.
- **Descartado com evidência:** antivírus em nuvem (Defender com `MAPSReporting=0`, sem "bloqueio à primeira vista" e sem
  eventos no horário); arquivo de log preso (Serilog teria criado `lwhc-*_001.log`; só existe um arquivo); deadlock no
  código de inicialização (revisado; nenhuma espera síncrona até `StartAsync`).
- **Reprodução:** 62 aberturas em 3 cenários (reabertura imediata após `Kill`, logo após recompilar, abertura normal) —
  todas com janela em 0,8–1,2 s.
- **Hipóteses não comprovadas:** contenção momentânea da máquina logo após o build (nós do MSBuild/servidor do compilador);
  estado da sessão gráfica (havia sessão do Chrome Remote Desktop desconectada às 16:53).
- **Instrumentação adicionada (`App/StartupTrace.cs`):** fases cronometradas do processo ao primeiro quadro, gravadas no log
  ("Inicialização concluída em X ms" ou aviso "Inicialização lenta"); watchdog que, após 10 s, grava a fase atual em
  `Logs\startup-slow-{pid}.txt` a cada 10 s (funciona antes de o logger existir). Se ocorrer de novo, esse arquivo diz
  em que fase parou; capturar então `dotnet-stack report -p <PID>` (ferramentas `dotnet-stack`/`dotnet-dump` instaladas
  globalmente nesta máquina).
- Abertura típica medida: processo→app ~60 ms, XAML do App ~110 ms, host ~110 ms, banco ~70 ms, janela + primeiro quadro ~570 ms;
  total ~0,9 s.

### Passo 2 — Hardware, CPU, Memória e GPU (17/10/2026)

- **Decisão central — histórico de fundo:** uma varredura amostra ~15 s, insuficiente para afirmar "carga sustentada"
  (seção 49). `MetricsHistoryService` (BackgroundService) guarda 30 min em memória: CPU/memória/commit a cada 5 s e
  memória privada por processo (≥ 50 MB) a cada 60 s. Os scanners juntam histórico (tag `source=history`) e janela da
  varredura (`source=scan`); as regras só concluem com amostras e duração suficientes. Nada é gravado em disco.
- **Fontes:** PDH via P/Invoke com `PdhAddEnglishCounter` (o Windows pt-BR traduz nomes de contadores — sem isso nada
  funcionaria aqui); `K32GetPerformanceInfo` + `GlobalMemoryStatusEx`; `Process` (protegidos = null, nunca 0); WMI
  (`System.Management`) com cache de 60 s; VRAM pelo registro (`HardwareInformation.qwMemorySize`).
- **GPU:** agregação dos contadores "GPU Engine" igual ao Gerenciador de Tarefas (soma por motor, motor mais ocupado);
  validado nesta máquina (RTX 3060, 12 GB lidos corretamente pelo registro — o WMI daria 4 GB).
- **Regras:** `cpu.sustained-high`, `cpu.throttling`, `memory.pressure`, `memory.commit-high`, `memory.possible-leak`,
  todas com parâmetros justificados e testes "não dispara / dispara". `MetricSeries.LongestRunAtOrAbove` ganhou
  `maxGap` (30 s): o computador suspenso entre duas amostras altas não conta como carga contínua.
- **Decisão deliberada:** sem finding de "virtualização desativada" — o WMI reporta falso quando o Hyper-V está ativo.
- **Persistência:** observação `hardware.inventory` gravada em `HardwareSnapshot` a cada varredura.
- **UI (revisada com impeccable, 2 rodadas):** telas Performance e Hardware; GPU real no dashboard; `IPageLifecycle`
  (só a página visível amostra; minimizado nada amostra além do histórico); estado pressionado nos botões; linhas
  empilhadas em cartões estreitos; `InfoCard` sem rótulo acima do título (regra "Sem Sobretítulo" no DESIGN.md);
  gráficos cobrindo o período real com legenda calculada; **cobertura do score** ("Avaliou 2 de 6 áreas…") no dashboard
  e no resultado — um 100 sobre parte das áreas não pode parecer "computador perfeito".
- `ByteSize.Format` passou a receber a cultura (UI pt-BR: "16,2 GB"; testes fixam invariante).
- Versão 0.2.0.

## Erros e correções do Passo 2

| Erro | Causa | Correção |
| --- | --- | --- |
| Inventário WMI vinha **todo vazio, sem nenhum erro** | No `System.Management` do .NET 10, `EnumerationOptions` com `Rewindable = false` + `ReturnImmediately` devolve coleções vazias em silêncio (no .NET Framework funciona) | Só `Timeout` nas opções; comentário no código e teste de regressão de integração |
| Processos protegidos apareceriam com "0 s de CPU" | `Try(() => process.TotalProcessorTime)` inferia `TimeSpan` não anulável | `Try<TimeSpan?>` explícito: protegido = null |
| Scanner do Diagnostics chamava classe do Monitoring | Violação de camada | `TryGetExecutablePath` entrou no contrato `IProcessSnapshotProvider` |
| Score "100 EXCELENTE" sem dizer o que foi medido | Só 2 de 6 áreas cobertas | Linha de cobertura no dashboard e no resultado (`Display.Coverage`) |
| Gráficos como "toco" ou barra vertical no início | Escala fixa de 30 min com 1 min de dados | Eixo X cobre o período disponível; legenda calculada; linha de base |
| Valores quebrando em 3 linhas em 1100 px | Grade rótulo/valor em cartões estreitos | `StackedInfoRowTemplate` (rótulo acima do valor) na Performance |
| Script de rampas OKLCH gerava cores erradas | PowerShell não diferencia `$L`/`$l`; `[Math]::Max(0, x)` escolhe a sobrecarga de inteiros | Variáveis renomeadas e literais `0.0`/`1.0` |
| Função de medição não executava | `Measure` é alias de `Measure-Object` (alias tem precedência sobre função) | Renomeada para `Get-AppUsage` |

### Teste de estresse da UI com break-ui (18/10/2026)

- Seletor de dados de desenvolvimento **só em DEBUG** (`App/DevData/`): Dados reais · Pior caso · Vazio · Um · Muitos,
  fixo no centro inferior. Troca os dados na fronteira (decoradores de `IPerformanceCounterSessionFactory`,
  `IProcessSnapshotProvider`, `IMetricsHistory`, `IHardwareInventoryProvider`), sem tocar nas telas. Modo inicial por
  argumento: `--DevData:Mode=worst`. Verificado que não existe no binário Release.
- Fixtures com valores reais do Windows: "To Be Filled By O.E.M.", fabricante de RAM em código JEDEC (`00AD063200AD`),
  part numbers com espaços, Xeon com 2 soquetes, 16/24 módulos, GPUs virtuais sem VRAM, nomes longos reais de processos,
  GPU sem contadores, inventário WMI vazio, histórico com lacuna de suspensão.
- 16 achados relatados (2 quebras, 11 visuais, 3 frágeis) e **todos corrigidos** ("fix all", 18/10/2026):
  - Cabeçalhos de métrica (Dashboard e Performance) em `Grid(*,Auto)` com estilo `MetricValue` (números tabulares);
    leitura ausente vira "—" + legenda "não disponível" (antes "não disponível" em 22 px sobrepunha o título).
  - GPU sem contadores: só o texto explicativo, sem linhas de "não disponível" repetidas.
  - Listas de processos: nome com reticências + tooltip (nome, PID, caminho), coluna de PID, valor alinhado;
    "< 1%" abaixo de 0,5%; textos de lista vazia.
  - Inventário vazio: cartão único explicando o motivo (em vez de 4 cartões cheios de "não disponível").
  - `InventoryText` (Application): placeholders de firmware ocultos, código JEDEC → fabricante, status WMI traduzido
    com tom (Degradado = âmbar, Com erro/Falha prevista = vermelho, via `InfoRow.Tone`).
  - Módulos de RAM em linhas compactas (slot · tamanho · velocidade / fabricante · part number), não um cartão por módulo.
  - Plural correto (`Fmt.Count`: "1 núcleo", "1 thread"), separador de milhar ("123.457 páginas/s"),
    "Soquete i de n" com vários processadores, título sem repetir fabricante.
  - `ByteSize.FormatPair`: parte e total na mesma unidade ("1.023,7 GB de 1.024 GB"); se arredondar igual
    ("2 TB de 2 TB"), desce uma unidade.
  - `Sparkline` posiciona por horário real (`Timestamps`) e quebra a linha em lacunas > 30 s (suspensão).
- Verificação: seletor em todos os estados, 980 e 1280 px, claro e escuro; dados reais sem regressão.
- Teste instável endurecido: `Timeout_kills_the_process_tree` falhou uma vez sob carga pesada (suítes em paralelo)
  → margem de 20 s e espera ativa de até 5 s pelo fim do PING em vez de atraso fixo.
- Capturas e o script de captura (`ui-break.ps1`) ficaram no scratchpad temporário da sessão, fora do repositório:
  para reverificar, abrir o app em Debug e usar o seletor (ou `--DevData:Mode=...`).

#### Erros e correções do break-ui

| Problema | Causa | Correção |
| --- | --- | --- |
| "não disponível" em 22 px sobrepondo o título do cartão | `DockPanel` sem reserva de largura para o valor | `Grid(*,Auto)` + "—" no valor e legenda pequena abaixo |
| "1 núcleos", "1 threads", "1 módulos" | Plural fixo no texto | `Fmt.Count(valor, singular, plural)` |
| "1023,7 GB de 1 TB" / "2 TB de 2 TB (100%)" | Cada valor formatado na própria unidade; arredondamento igualava os dois | `ByteSize.FormatPair` (unidade do menor valor; desce uma unidade se os textos ficarem iguais) |
| "00AD063200AD" como fabricante de RAM | WMI devolve código JEDEC | `InventoryText.MemoryVendor` |
| "Degraded"/"Error" em inglês e sem destaque | Status WMI exibido cru | `InventoryText.DeviceStatus` + `StatusTone` nos templates de `InfoRow` |
| Gráfico ligava pontos através de uma suspensão | Eixo X por índice da amostra | `Sparkline.Timestamps` + quebra em lacunas > 30 s |
| Linha `_ = culture;` inserida por engano durante a edição | Erro de edição | Removida antes do build final |

### Passo 3 — Discos e Armazenamento (18/10/2026)

- **Sonda antes de desenhar (sem admin, nesta máquina):** `MSFT_PhysicalDisk`/`MSFT_Disk`/`MSFT_Partition`/`MSFT_Volume`
  funcionam; `MSFT_StorageReliabilityCounter` (temperatura, desgaste, horas) e `MSStorageDriver_FailurePredictStatus`
  dão **acesso negado**. IOCTL com o disco aberto em acesso 0 entrega TRIM e penalidade de busca (SSD × HDD); a
  temperatura não é suportada pelo SSD SATA nem pelo disco USB desta máquina. Decisão: sem admin, vida útil e horas
  ficam "não disponível" com a explicação; com admin (processo elevado, Passo 5), o contador de confiabilidade é lido.
- **Application:** `StorageInventory` (discos, partições, volumes; null = não informado), `StorageText` (códigos do WMI →
  tipos/textos), `ISpaceAnalyzer` + `FolderNode`/`SpaceAnalysis` + `SpaceAnalysisOptions`, catálogo `StorageLocations`.
- **Infrastructure:** `WmiQuery` (utilitários WMI extraídos do provedor de hardware, que passou a usá-los),
  `WmiStorageInventoryProvider` (cache de 30 s), `StorageDeviceQuery` (IOCTLs), `FileSystemSpaceAnalyzer`.
- **Diagnostics:** `DiskScanner` (`disks`, ordem 50) e `StorageScanner` (`storage`, ordem 60, só locais conhecidos — a
  árvore completa é sob demanda na tela) + 5 regras (ver `docs/RULE_ENGINE.md`).
- **Decisões de regra:** volume de dados só alerta com percentual **e** valor absoluto baixos (5% de 2 TB ≈ 100 GB não
  é problema); o C: desta máquina (7% livre, ~45–65 GB) **não** gera alerta, de propósito. TRIM desconhecido não é
  problema. A Lixeira fica fora do total de temporários/caches (esvaziar é decisão do usuário).
- **Tela Armazenamento:** volumes (barra de uso na cor da série, nunca vermelha sem achado), análise de espaço
  (progresso ao vivo, cancelar, trilha clicável, linhas clicáveis com o novo estilo `RowButton`, maiores arquivos,
  arquivos grandes antigos, explicação de `pagefile.sys`/`hiberfil.sys`), temporários e caches, discos físicos
  (cartões de 2 colunas). Revisada com impeccable em claro/escuro, 1280 e 980 px.
- **Performance da análise (medida):** C: com 2,6 mi de arquivos e 566 mil pastas. Primeira versão: 529 s (abria
  leitores extras só até a profundidade 3 → na prática 1 leitor). Benchmark em `C:\Program Files`: 1 leitor 5,2 s,
  2 leitores 5,2 s (não escalava). Correção: vaga livre vira tarefa em qualquer profundidade + 4 leitores em SSD e 1 em
  HDD → 2,7 s no benchmark e 73–113 s no C: inteiro dentro do app. O custo do analisador sobre a enumeração crua é ~1,6×.
- `ByteSize.Format` passou a usar separador de milhar ("1.012,8 MB"), igual a `FormatPair`.

#### Erros e correções do Passo 3

| Problema | Causa | Correção |
| --- | --- | --- |
| C: ficava sem disco físico (`DiskNumber` nulo) quando outra consulta WMI rodava junto | `MSFT_Partition` estourou o timeout de 20 s sob carga (visto com as 3 suítes de teste em paralelo) | Disco de cada volume via `IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS` (rápido, sem admin, sem WMI); partição do WMI virou reserva; teste de regressão |
| Análise do C: em ~9 min | Paralelismo limitado à profundidade 3 | Paralelismo em qualquer profundidade, por tipo de disco (medido, ver acima) |
| Disco USB de tipo desconhecido mostrava TRIM "Não se aplica (HDD)" | Afirmação feita sem o tipo ser HDD | "Não se aplica" só com HDD comprovado; senão "não disponível" |
| "sem alteração desde 31/12/1969" na lista de arquivos antigos | Carimbo de data zerado no arquivo | Datas antes de 1980 ficam fora da lista (inválidas, não antigas); teste |
| "899,3 GB em C:\" com 849 GB usados no volume, sem explicação | Links físicos (WinSxS) contados em cada pasta | Nota na raiz com os dois números |
| "Analisar" não encontrado pelo script de captura | PowerShell 5.1 lê `.ps1` sem BOM como ANSI | Scripts com acentos gravados em UTF-8 com BOM |
| Análise de 409 s durante a captura | O script de captura consultava a árvore de automação inteira a cada 3 s | Script passou a esperar pelo log; 73 s |
| `EnumerationOptions` ambíguo; `Storage` namespace × constante | `System.IO` × `System.Management`; nome repetido | Nome qualificado; constante `StorageScope` |

### Teste de estresse da tela Armazenamento com break-ui (19/10/2026)

- Seletor de dados de desenvolvimento (só DEBUG) agora cobre a tela Armazenamento: `App/DevData/DevDataStorageFixtures.cs`
  e os decoradores `SwitchingStorage` (`IStorageInventoryProvider`) e `SwitchingSpaceAnalyzer` (`ISpaceAnalyzer`); o
  seletor também recarrega a tela Armazenamento ao trocar de modo.
- Fixtures realistas: SSD NVMe de 4 TB quase cheio (2 GB livres), HDD de NAS com falha prevista e 61 mil horas, VHDX do
  Docker/WSL sem letra, leitor de cartão sem mídia, SSD em RAID com TRIM desligado e 5 partições, rótulos no limite de
  32 caracteres do NTFS, rótulo em japonês, volume removível de 2 GB com 512 B ocupados, volume sem disco (VeraCrypt);
  24 discos/volumes ("Muitos"); análise com OneDrive corporativo, `node_modules` aninhado, pasta com emoji, 2,6 mi de
  arquivos, pasta sem permissão; estados Vazio e Um.
- 14 achados (2 quebras, 8 visuais, 4 frágeis) + 4 decisões, **todos corrigidos** ("fix all", 19/10/2026; decisões
  conforme as recomendações):
  - `ByteSize.FormatPair`: zero não escolhe a unidade ("0 GB livres de 380 GB"); desce no máximo uma unidade, e só se
    isso mostrar a diferença ("2 GB livres de 2 GB", não "2.097.151,5 KB de 2.097.152 KB").
  - Plural ("1 arquivo") no status, na legenda e nas linhas de pasta.
  - Disco sem mídia (leitor de cartão vazio): cartão só com título e "sem mídia inserida". Disco sem volume com letra:
    uma linha "Volumes: nenhum com letra de unidade".
  - Temporários e caches: só os encontrados viram linha; ausentes numa frase; "sem permissão" no lugar de "0 B".
  - Legenda da pasta atual com uma nota por linha; nomes de arquivo quebram linha; caminhos encurtados no meio
    (`StorageViewModel.ShortPath`: "C:\Users\…\models\blobs", completo na dica).
  - Legenda do volume com o tipo antes do nome do disco (o corte em 980 px não leva mais "SSD NVMe").
  - Corrida de carregamento: só a leitura mais recente é aplicada (`_loadVersion`).
  - Analisador mantém até 10 pastas sem permissão na lista mesmo com 0 B (antes sumiam em "N pastas menores").
  - Análise descartada, com aviso, quando o volume analisado some na recarga.
  - Decisão A: barra e texto do volume em âmbar/vermelho pela **mesma** decisão da regra (`LowFreeSpaceRule.Assess` +
    `ParameterOf`, respeitando thresholds configurados e regra desabilitada) e "pouco espaço livre" na legenda.
  - Decisão C: mais de 4 discos viram lista compacta (Expander por disco com o cartão completo dentro).
  - Decisão D: ao iniciar a análise, a tela rola até o painel (título + linha de progresso).
  - Texto do estado sem discos nem volumes não fala mais em "volumes acima".
- Testes novos: `FormatPair` com zero e quase iguais, pasta sem permissão real (ACL de negação no diretório temporário),
  `StorageScreenTests` (caminho encurtado, mesma decisão da regra para a barra). 261/261.
- Verificação: todos os estados do seletor em 1280 px claro e 980 px escuro; dados reais sem regressão.
- Capturas e scripts (`storage-break.ps1`) no scratchpad temporário da sessão.

#### Erros e correções do break-ui da tela Armazenamento

| Problema | Causa | Correção |
| --- | --- | --- |
| "0 B livres de 408.021.893.120 B" | Unidade escolhida pelo menor valor, que era zero | Zero não escolhe a unidade |
| "2.097.151,5 KB livres de 2.097.152 KB" | Laço descia de unidade até aparecer diferença de 512 B | Desce no máximo uma unidade e só se mostrar a diferença |
| Inventário real sobrescrevendo o pior caso do seletor | Leitura inicial mais lenta terminava depois da recarga | Versão de carregamento; só a última é aplicada |
| Pasta sem permissão invisível | Poda por tamanho (0 B < 1 MB) | Pastas sem permissão sempre mantidas (até 10) |
| Edições da View não aplicadas pelo script | `R` é alias de `Invoke-History` no PowerShell | Edições refeitas com a ferramenta de edição; não usar nomes curtos de função em scripts |
| `PASSOS.md` e dois docs corrompidos (caracteres isolados trocados no arquivo inteiro) ao atualizar a documentação | No PowerShell, `@(@('a','b'))` com um único par é achatado: o laço recebeu strings e `$p[0]`/`$p[1]` viraram caracteres | Arquivos restaurados do último commit com `git checkout --` (sem trabalho pendente neles) e edições refeitas com a ferramenta de edição. Para um só par, usar `@(,@('a','b'))` — ou, melhor, a ferramenta de edição |

### Crítica da tela Armazenamento com impeccable (19/10/2026)

- Duas avaliações isoladas (revisão de design + detector). Nota **26/40 (Aceitável)**: 1 P0, 3 P1, 1 P2. Registro em
  `.impeccable/critique/2026-10-19T04-48-50Z__…storageview-xaml-….md`.
- O detector do impeccable **não analisa XAML** (as regras procuram CSS/JSX; ao escanear uma pasta, ignora `.xaml`):
  "limpo" não é evidência em telas WPF. Verificado com arquivos de controle.
- Decisões do usuário: começar pela **hierarquia**; direção **"laudo primeiro"** (a tela abre pelo diagnóstico mais
  grave — saúde dos discos e pouco espaço — e só depois a análise); escopo **tudo** (5 prioritários + menores).

### Plano da crítica executado (19/10/2026) — layout, harden, clarify, audit, menores, polish

- **Layout ("laudo primeiro"):** a tela abre por cartões de diagnóstico (`StorageAlert`): disco com falha prevista ou
  estado ruim (evidência SMART/operacional, volumes afetados, "faça backup agora") e volume com pouco espaço (limite
  real da regra, botão "Analisar C:"). Mesmos critérios de `DiskHealthRule` e `LowFreeSpaceRule`. Sem achados: linha
  "Nenhum disco sinaliza problema…". Ordem: alertas → Volumes → Análise de espaço → arquivos → Discos físicos →
  Temporários e caches. Leitura vazia no topo; análise escondida sem volumes; volumes compactos acima de 6; disco
  crítico já aberto na lista compacta; "disco com falha prevista" na legenda dos volumes afetados.
- **Harden:** análises guardadas por volume/pasta (chips "Análises feitas"), a anterior continua visível enquanto a
  nova lê e cancelar não perde nada; listas de arquivos seguem a pasta atual (com escopo dito); analisador amplia a
  lista quando as pastas cortadas passam de metade da pasta (`DominantChildrenPerFolder` = 500); toda linha tem ação
  (entra na pasta ou mostra no Explorer); "pastas menores" diz por que não estão listadas; trilha com a pasta atual
  como texto (não botão); "Analisar outra pasta…" (`IFolderPicker`/`OpenFolderDialog`).
- **Clarify:** `StorageHints` explica `.vhdx` do Docker e do WSL, discos virtuais, modelos do Ollama e do Docker Model
  Runner, `.gguf`/`.safetensors`, ISOs, despejos, arquivos `.sys` e pastas (`node_modules`, `.venv`, `.gradle`,
  `WinSxS`…), marcando "não apague" quando apagar quebra algo. Catálogo de caches ganhou npm, Yarn, pnpm, NuGet,
  Gradle, Maven e pip (`DeveloperCache`, com o comando oficial de limpeza). Nesta máquina: npm 7,8 GB, Gradle 4,7 GB,
  pip 3,3 GB, NuGet 2,9 GB. Sem promessa de roadmap na interface; erros em linguagem clara (detalhe técnico só no modo
  avançado); "via WMI" saiu do subtítulo; "links físicos" explicado.
- **Audit (acessibilidade):** foco vai para "Cancelar análise" ao analisar e para a primeira pasta ao terminar/navegar;
  status com `AutomationProperties.LiveSetting` + evento `LiveRegionChanged` no início/fim/cancelamento; nomes
  acessíveis completos (nome, tamanho, %, ação) e caminho em `HelpText`; arquivos e locais focáveis; menu de contexto
  (Mostrar no Explorer, Copiar caminho); Ctrl+C, Backspace/Alt+← e F5; Expander com nome; texto diferente por nível
  ("pouco espaço livre" / "espaço criticamente baixo"); borda de foco das linhas com 2 px.
- **Menores:** unidade sobe a partir de 1.000 ("0,99 GB"), "menos de 1 MB" em totais, colunas alinhadas com
  `SharedSizeGroup`, ordenar por tamanho/nome, filtro por nome/extensão, "Mostrar todos (N)".
- **"Mostrar no Explorer" sem executar nada:** `ShellLauncher` usa `SHParseDisplayName` + `SHOpenFolderAndSelectItems`
  (seleciona o item na pasta-mãe; um `.exe` nunca é executado), por isso não passa pelo `CommandExecutor`.
- **Polish:** sombra Flutuante em menus e tooltips (pendência antiga), estilos de `ContextMenu`, `MenuItem` e
  `FilterBox`; DESIGN.md com Diagnostic Alert, Floating e Filter Box.
- Defeitos achados na verificação e corrigidos: status "C:: analisado" (dois-pontos duplicado), "1 pasta menor: cada
  uma com menos de 1,2 TB", alerta "0 B livres" (agora "está sem espaço livre"), e no seletor de dados a análise de
  teste ficava sobre o C: real (`ResetAnalyses`, só chamado pelo seletor).
- Testes novos: `StorageHintsTests`, lista ampliada com pastas dominantes, `ShellLauncher` com caminho inexistente,
  "menos de 1 MB"; formatação atualizada ("0,99 GB", "1 KB"). 281/281.
- **Nova crítica (2ª rodada): 26 → 28/40 (Bom)**; P0 1 → 0, P1 3 → 2. Registro em
  `.impeccable/critique/2026-10-19T05-47-28Z__…storageview-xaml-….md`. Detector continua sem cobrir XAML.
  P1 e P2 da 2ª rodada **corrigidos** (decisão do usuário: "Corrigir P1 e P2", soma tratada com o usado no título):
  - Título da análise na raiz mostra o **espaço usado** ("3,6 TB usados em C:"); a soma das pastas e o motivo (links
    físicos) ficam na legenda, com aviso de que as porcentagens são sobre a soma.
  - Alertas dentro de `AccessibleRegion` (ContentControl focável com AutomationPeer de grupo; nome = nível, título,
    o que aconteceu e ação), anel de foco `CardFocusRing`; resumo "N problemas críticos…" com `LiveSetting=Assertive`
    anunciado ao abrir; títulos com `AutomationProperties.HeadingLevel` (página nível 1, seções nível 2, alertas nível 3).
  - Barra de progresso aproximada (bytes lidos ÷ usado, até 99%) na análise de volume; status em Body; "Caminho
    copiado"/"Comando copiado" anunciados.
  - Linha de pasta com uma ação (entrar); "Mostrar no Explorer" num botão próprio ao lado; caso dominante sem o corte
    de 1 MB; lista limitada a 25 com "Mostrar mais N pastas".
  - Dica do Docker diz o que `docker system prune` remove e indica Clean / Purge data; comandos copiáveis pelo menu
    ("Copiar comando": `docker system df`, `wsl --list --verbose`, `ollama list`, `docker model ls`, `npm cache clean
    --force`, `dotnet nuget locals all --clear`…). O app nunca executa esses comandos.
  - Não verificado em captura: a barra de progresso (a análise de teste termina antes da foto); verificado no código.
  Ainda abertos (P3, anotados): unidade escolhida por linha ("994 GB de 16.763,8 GB" ao lado de "1,1 TB de 16,4 TB"), volume a 0 B âmbar com
    texto "falham", "C:" x "C:\", duas colunas de arquivos apertadas em 980 px, "1 GB livres", `.dump` não reconhecido.

### Passo 4 — Processos e Inicialização (19/10/2026)

- **Sonda antes de desenhar (sem admin, nesta máquina):** 9 entradas Run (HKCU/HKLM/WOW6432Node), `Ollama.lnk` na pasta
  Inicializar, 201 tarefas agendadas (49 de logon/boot), 84 serviços automáticos. ~130 de ~330 processos são protegidos:
  sem caminho, CPU e dono sem administrador (a tela diz quantos).
- **Application:** `ProcessInventory` (`ProcessDetails`, `ProcessInventorySnapshot`, `TerminateOutcome`,
  `IProcessInventoryProvider`), `CriticalProcesses` (PID 0/4 + System, Registry, smss, csrss, wininit, winlogon,
  services, lsass, LsaIso, svchost, fontdrvhost, dwm, MsMpEng, NisSrv, SecurityHealthService, sihost, LogonUI…; o
  explorer **não** é crítico), `ProcessRates.Compute` (cálculo puro: mesmo PID **e** mesmo horário de início; CPU em %
  da máquina como o Gerenciador de Tarefas, limitada a 0–100), `FileIdentity`/`SignatureStatus`/`IFileIdentityProvider`,
  `StartupInventory`/`StartupEntry`/`IStartupInventoryProvider`, `StartupCommand` (interpreta a linha de comando:
  aspas, caminho sem aspas até a extensão, `\SystemRoot\`, nome simples → System32, alvo DLL do `rundll32`; pasta
  temporária) e `StartupImpact` (estimativa pelo consumo atual dos processos do mesmo executável).
- **Infrastructure:** `ProcessInventoryProvider` (Toolhelp32 para o pai; `OpenProcess` com `QUERY_LIMITED_INFORMATION`
  para caminho, início, CPU, E/S e dono via token → SID → conta, com cache de SID); `Terminate` recusa processo crítico,
  confere o horário de início (PID reaproveitado ⇒ `NotFound`) e registra no log. `FileIdentityProvider`
  (`FileVersionInfo` + `WinVerifyTrust` sem revogação/online + catálogo via `CryptCATAdmin*`; cache por caminho e data;
  no máximo 2 verificações simultâneas). `StartupInventoryProvider` (registro com `StartupApproved` — primeiro byte
  ímpar = desativado; atalhos via `WScript.Shell`; tarefas via `Schedule.Service` com gatilho de logon/boot e ação
  executável; serviços via WMI `StartMode='Auto'`; cache de 30 s; cada fonte falha sozinha e vira aviso).
  `ShellLauncher.ShowProperties` (`SHObjectProperties`, sem executar nada).
- **Diagnostics:** `StartupScanner` (`startup`, ordem 70, observação `startup.entry`) + `startup.missing-executable`
  e `startup.temp-location`. Tarefa do próprio Windows (`\Microsoft\Windows\…`) com arquivo removido vira **Info**
  ("não é preciso fazer nada"): nesta máquina, `\Microsoft\Windows\Autochk\Proxy` chama `acproxy.dll`, removida pelo
  Windows 11.
- **Tela Processos:** lista virtualizada atualizada no lugar a cada 2 s (só com a tela visível), colunas ordenáveis
  (cabeçalho clicável com seta, `SortGlyphConverter`), pesquisa, modo árvore, escudo nos protegidos, fabricante (em
  vermelho só com assinatura inválida), painel de detalhes (caminho em linha inteira, fabricante, assinatura, usuário,
  pai, início, tempo de CPU), Abrir local do arquivo, Propriedades, Copiar caminho (menu e Ctrl+C) e **Finalizar com
  confirmação no lugar** (foco vai para "Encerrar"; Delete pede a confirmação, nunca encerra direto). Abaixo de 860 px
  de lista, a coluna Fabricante sai e as ações vão para baixo dos detalhes.
- **Tela Inicialização (somente leitura):** "laudo primeiro" (cartões das entradas com problema; o de componente do
  Windows em tinta neutra), filtros (pesquisa, desativados, itens do Windows — 85 ocultos por padrão), grupos
  Aplicativos/Tarefas agendadas/Serviços automáticos com status, origem, fabricante e impacto; onde desativar
  conforme a origem (Gerenciador de Tarefas, `taskschd.msc`, `services.msc`). Desativar com backup fica para Correções.
- **Regressão corrigida no caminho:** medir os caches de desenvolvimento levava 72 s; `MeasureLocationsAsync` passou a
  somar em paralelo (2 locais por vez, vagas compartilhadas) → 2 s.
- **Acessibilidade:** o menu lateral era lido como `NavigationItem { Title = … }` (o `ListBoxItem` usa o `ToString` do
  record) → `ToString` devolve o título. Botões com ícone + texto ganharam `AutomationProperties.Name`.
- Revisado com impeccable (rodada visual em 1280 claro e 980 escuro + uma de confirmação). Testes novos:
  `ProcessRatesTests`, `StartupTests` (comandos reais desta máquina, pasta temporária, impacto, críticos, regras,
  tarefa do Windows informativa) e integração (inventário do próprio processo, recusa do lsass, encerrar um `ping`
  iniciado pelo teste, assinatura por catálogo do notepad, inventário de inicialização sem avisos), progresso da varredura que nunca volta. 325/325.
- Script de captura `p4-check.ps1` e imagens no scratchpad temporário da sessão. Versão 0.4.0.

#### Erros e correções do Passo 4

| Problema | Causa | Correção |
| --- | --- | --- |
| Build "com êxito" escondendo erros de analisador (CA1720 `Signed`/`Unsigned`, CA1806 no `WinVerifyTrust` de fechamento) | Build incremental não reanalisou | Sempre validar com `--no-incremental -warnaserror` (saída em arquivo); nomes `Valid`/`NoSignature`, `_ =` no retorno |
| CA2201 ao lançar `COMException` para "Agendador indisponível" | Tipo reservado | `InvalidOperationException`, capturada junto com a `COMException` da fonte |
| Teste com `lsass.HasExited` dava acesso negado | `Process.HasExited` abre o processo | Teste confere pela lista de processos |
| Medição dos caches de desenvolvimento em 72 s | Locais somados em série | `SumAsync` paralelo com vagas compartilhadas: 2 s |
| "Desativado · desativado no Gerenciador de Tarefas" e impacto "Desativado" repetido | Status montado com o detalhe sem olhar o conteúdo | Detalhe usado como status quando já diz "desativado"; impacto vazio para desativados; "não medido" curto, explicado na legenda |
| Tarefa do Windows com arquivo removido recomendava desativar "pelo programa que a criou" | Regra tratava componente do sistema como resto de programa | Info + "não é preciso fazer nada"; cartão neutro |
| Coluna Fabricante cortada e caminho quebrando em 9 linhas em 980 px | Larguras fixas somavam mais que a área útil; caminho numa coluna estreita | Modo compacto (coluna sai, ações abaixo) e caminho em linha inteira |
| Script de captura não achava botões nem "Configurações" | Botões com `StackPanel` não têm nome no UIA; `.ps1` sem BOM lido como ANSI | `AutomationProperties.Name`; script em UTF-8 com BOM |
| App levou 78 s para abrir uma vez (testes de sistema em 1 min 26 s na mesma rodada) | "processo→app 42 s" antes de qualquer código do app: binários recém-compilados sendo examinados pelo sistema (mesma família da investigação de 17/10) | Não é regressão; o script de captura espera até 150 s |
| `dotnet test --no-incremental` recusado | Opção só do `build` | `build --no-incremental` e depois `test --no-build` |
| `Timeout_kills_the_process_tree` falhando com a suíte inteira | O teste confere que nenhum `PING` novo sobrou, e o teste de encerramento do Passo 4 inicia um `PING` em paralelo | As duas classes na mesma coleção xUnit (`PingProcessesCollection`), sem paralelismo entre elas |
| `Reports_progress_until_100_percent` falhou 1 vez na suíte inteira (0 em 40 isolado) | **Defeito real:** `ScanOrchestrator` tirava a fotografia do progresso dentro do `lock` e publicava fora; com scanners em paralelo, a de 50% podia sair depois da de 100% e a barra da Varredura voltava | `Report` dentro do `lock`; teste `Progress_never_goes_backwards_with_parallel_scanners` (8 scanners, 20 rodadas) como guarda — a corrida não reproduz de forma determinística, então o teste não prova a correção sozinho |
| `Timeout_kills_the_process_tree` ainda falhou 1 vez em Release (em aberto) | Só na 1ª rodada logo após um build `--no-incremental`, com a máquina ~25× mais lenta (unitários em 21 s); a saída silenciosa não trouxe qual asserção falhou | Não reproduziu em 4 rodadas Release seguidas com saída detalhada. Margens não alteradas sem saber a causa; se voltar, rodar com `--logger "console;verbosity=normal"` para ver a asserção |

### Teste de estresse das telas Processos e Inicialização com break-ui (19/10/2026)

- Seletor de dados de desenvolvimento (só DEBUG) cobre as duas telas: `App/DevData/DevDataProcessFixtures.cs` e os
  decoradores `SwitchingProcessInventory`, `SwitchingFileIdentity` e `SwitchingStartup`; o seletor recarrega a
  Inicialização ao trocar de modo (Processos se atualiza sozinha em 2 s). **Fora de "Dados reais", `Terminate` nunca
  chega ao Windows** (os PIDs da fixture podem existir de verdade): o encerramento é simulado.
- Fixtures realistas: Intune Management Extension (nome de 48 caracteres, SYSTEM), ERP com nome acentuado e travessão,
  caminho Electron de 230 caracteres e assinatura inválida, `sh` do Git sem fabricante, Razer com vazamento de 1,28 mi
  de handles, MsMpEng a 100% protegido, vmmem de 32 GB, fabricante "TODO: <公司名称>" (ASUS real), assinante chinês do QQ,
  cadeia `npm run dev` de 14 níveis, pai já encerrado; Run do Teams clássico desinstalado, updater NSIS em `%TEMP%`,
  RunOnce com PowerShell `-EncodedCommand` (~1.600 caracteres), tarefa do OneDrive com SID, NVIDIA no DriverStore,
  comando sem aspas de fechamento, estado não lido, serviço órfão; Vazio, Um e Muitos (1.529 processos, ~620 entradas).
- 16 achados (6 quebras, 7 visuais, 3 frágeis) + 4 decisões, **todos corrigidos** ("fix all", 19/10/2026; decisões
  conforme as recomendações):
  - **Acessibilidade:** a linha da lista de processos era lida como `…ViewModels.ProcessItem` → `ToString()` devolve o
    nome acessível (mesmo defeito do menu lateral). Confirmado via UIA.
  - **Entrada suspeita escondida:** `IsWindowsComponent` marcava qualquer executável em `C:\Windows` — inclusive
    `powershell`, `cmd`, `mshta`, `rundll32`, `regsvr32`, `wscript` e drivers no `DriverStore` — e a tela os ocultava.
    Agora `StartupCommand.IsWindowsOwned` exclui hospedeiros de script e DriverStore. Nesta máquina, ocultos 85 → 81.
  - **Regra nova `startup.script-host`** (decisão A): PowerShell com `-EncodedCommand`, PowerShell oculto baixando da
    internet, `mshta`/`rundll32` com `javascript:`/`vbscript:`, `regsvr32` com `scrobj.dll`, hospedeiro abrindo URL,
    script em pasta temporária. Médio, 60%. A tela usa o mesmo critério (`StartupCommand.ScriptConcern`); nunca é
    "aviso do Windows", mesmo numa tarefa em `\Microsoft\Windows\`. Nos dados reais, nenhum falso positivo.
  - **Defeito do interpretador achado pelos testes:** `regsvr32 /s … scrobj.dll` (só o nome, sem aspas) virava um
    "caminho" com a linha inteira, porque a busca pela extensão achava o `.dll` do fim. Nome sem barra invertida vai
    até o primeiro espaço.
  - Processos: nome com reticências (Grid no lugar de StackPanel) e dica com nome + caminho; milhares em
    threads/handles (`Fmt.Whole`); "156 d 12 h" (`Fmt.Duration` sobe para dias); árvore com 12 px por nível até 8
    níveis; seta de ordenação some no modo árvore; mensagens de pesquisa sem resultado e de leitura vazia; detalhes
    com altura máxima de 45% e rolagem própria (decisão C), com a confirmação trazida inteira à vista; assinatura
    inválida em vermelho também nos detalhes.
  - Fabricante com assinatura inválida: "assinatura inválida · Northwind…" (a marca primeiro, o corte nunca a leva);
    entrada de hospedeiro de script: "via powershell.exe (Microsoft Corporation)", não "Microsoft Corporation" sozinho.
  - Inicialização: "Tarefa desativada" sem repetição; estado não lido em tinta normal; estado vazio próprio; 3 cartões
    de problema e "Mostrar mais N" (decisão B); resumo "N com problema · N aviso do Windows" (decisão D); comando do
    cartão limitado a 4 linhas (dica e "Copiar comando" com o texto inteiro).
  - **Pesquisa da Inicialização:** os grupos são montados uma vez por leitura e o filtro só muda a visibilidade das
    linhas (`StartupRow.IsVisible`, `StartupGroup.VisibleCount`): ~1.922 → ~141 ms de CPU por tecla com ~620 entradas.
- Testes novos: `Fmt.Whole`/`Duration` com dias, `IsWindowsOwned`, `ScriptConcern` (8 padrões + 5 comandos comuns que
  não podem disparar), `ScriptHostName`, regra `startup.script-host`, `regsvr32` só com o nome. 356/356 em Debug e Release.
- Verificação: Pior caso em 1280 claro e 980 escuro, Vazio, Um, Muitos e Dados reais. Capturas em `p4fix`/`p4fix2` no
  scratchpad da sessão.

### Passo 5 — MVP 1 (d): Event Viewer, Integridade do Windows e Processo Elevado (06/11/2026)

- **Event Viewer Analyzer (`IEventViewerProvider` / `EventViewerScanner`):**
  - Leitura dos logs `System`, `Application`, `Setup`, `WHEA` e `Windows Update` via `EventLogReader`.
  - Agrupamento inteligente por log/origem/id dentro de janela deslizante de 24h com timestamps `FirstSeen` e `LastSeen`, contagem de recorrência e extração de detalhes técnicos.
  - Persistência estruturada automática na tabela `EventSummary` do SQLite a cada varredura.
  - 5 novas regras: `eventviewer.disk-io-errors` (Disk 153/7/11, NTFS 55/137), `eventviewer.gpu-driver-crash` (TDR Display 4101), `eventviewer.hardware-whea` (WHEA-Logger 17/18/19), `eventviewer.unexpected-shutdown` (Kernel-Power 41 / EventLog 6008), `eventviewer.app-crash` (Application Error 1000 frequente).
- **Integridade do Windows Não Destrutiva (`IWindowsIntegrityProvider` / `WindowsIntegrityScanner`):**
  - Executores e analisadores multilíngues (pt-BR e en-US) para os 3 pilares do SO (seção 22 do PROMPT):
    - Component Store via DISM: `/CheckHealth` na varredura rápida e `/ScanHealth` na varredura profunda sob demanda na tela.
    - Arquivos protegidos do sistema via SFC: `/VERIFYONLY` (somente leitura).
    - Sistema de arquivos via CHKDSK: `chkdsk.exe <drive>:` somente leitura.
  - 3 novas regras: `windows.component-store-corrupted`, `windows.system-files-corrupted`, `windows.file-system-errors`.
  - **Decisão crítica de codificação de console:** SFC no Windows gera saída estritamente em **UTF-16LE** (`Encoding.Unicode`); DISM e CHKDSK geram em página **OEM** (`InstalledUICulture.TextInfo.OEMCodePage`, 850 em pt-BR). O executor trata a codificação configurável para evitar corrupção de caracteres.
- **Processo Elevado Mínimo (`LeccorWindowsHealthCenter.Elevated.exe`):**
  - Projeto dedicado com manifesto `requestedExecutionLevel="requireAdministrator"`. O app principal permanece rodando como usuário comum (`asInvoker`).
  - Comunicação via Named Pipe (`\\.\pipe\LeccorHealthCenter-Elevated-<SessionId>`) com ACL restrita ao usuário atual, BuiltinAdmins e SYSTEM.
  - Handshake de autenticação com token criptográfico de 256 bits gerado pelo processo pai.
  - Verificação de segurança de PID cliente via P/Invoke `GetNamedPipeClientProcessId`.
  - Allowlist estrita de operações (`ElevatedDispatcher`): executáveis obrigatoriamente dentro de `%windir%\System32`, flags imutáveis de DISM, SFC e CHKDSK, consulta WMI de `MSFT_StorageReliabilityCounter` (desgaste percentual de NVMe, temperatura e horas de uso sem privilégios no app principal) e encerramento de processos não críticos com proteção contra reaproveitamento de PID (`ExpectedStartTime`).
  - Tratamento suave de recusa de UAC pelo usuário (`ElevationCancelledException`) sem quebrar ou congelar o aplicativo.
  - Autodestruição programada do processo elevado após 45 s de inatividade.
- **Tela de Resultado Completa (seções 44 e 73 do PROMPT):**
  - Card de destaque "Problema mais importante" no topo dos resultados, com severidade, impacto, recomendação e botão `[Investigar problema]` direcionando automaticamente à tela relevante.
  - Badges semânticos de contagem por severidade (Crítico, Alto, Médio, Baixo, Informativo).
  - Botão contextual `[Investigar]` em cada card de finding individual.
- **Telas Novas no Menu Principal:**
  - `WindowsView` / `WindowsViewModel`: visualização do status dos 3 pilares com botões de verificação rápida e profunda, e terminal de saída.
  - `EventsView` / `EventsViewModel`: visualizador Master-Detail dos eventos agrupados nas últimas 24h, filtros de severidade, busca textual e atalho para o Visualizador nativo (`eventvwr.msc`).
  - `HistoryView` / `HistoryViewModel`: histórico de varreduras recuperado do SQLite com exibição de laudos completos passados.
- Testes novos e cobertura:
  - 38 novos testes unitários e de integração adicionados.
  - Total: **397 testes automatizados aprovados (100% de sucesso)** — 332 unitários, 38 integração, 27 sistema.

### Passo 6 — Explorer, Pastas Lentas, Serviços e Drivers (06/11/2026)

- **Windows Explorer (Seção 16 do PROMPT):**
  - `ExplorerProvider` (`IExplorerProvider`): enumera extensões de Shell registradas em `HKCR\*\shellex`, `Directory\shellex`, `Folder\shellex`, `Drive\shellex`, `AllFileSystemObjects\shellex`, `Directory\Background\shellex`, `DesktopBackground\shellex` e `Directory\ContextMenus`.
  - Resolução de CLSID em `InprocServer32` em `HKCR\CLSID` e `HKCR\WOW6432Node\CLSID`.
  - Verificação de lista de bloqueio do sistema (`Shell Extensions\Blocked`).
  - Resolução de fabricante e validação de assinatura digital via `IFileIdentityProvider` (Authenticode + Catálogo).
  - Medição do tamanho de arquivos de cache de ícones e miniaturas (`IconCache.db`, `thumbcache_*.db`) em `%LocalAppData%\Microsoft\Windows\Explorer`.
- **Detector de Pastas Lentas (Seção 17 do PROMPT):**
  - `FolderBenchmarkService` (`IFolderBenchmarkService`): cronometra tempo de enumeração de arquivos e diretórios na raiz e leitura de atributos/metadados (`GetFileAttributesEx`/`FileInfo`).
  - Detecção determinística de gargalos: > 3.000 arquivos na raiz, caminhos de rede (UNC ou unidades de rede mapeadas SMB), pastas sincronizadas em nuvem (OneDrive, Dropbox, Google Drive, iCloud) e lentidão de metadados.
- **Inventário e Diagnóstico de Serviços (Seção 20 do PROMPT):**
  - `WindowsServicesProvider` (`IServicesProvider`): lê o registro `SYSTEM\CurrentControlSet\Services` de forma atômica para obter ImagePath, StartupType, Dependências e Conta de Logon.
  - Consulta rápida de estado atual e PID via WMI `Win32_Service` (`SELECT Name, State, ProcessId FROM Win32_Service` em ~100 ms).
  - Normalização e extração de executável (`ExtractExecutablePath`), validação de existência do arquivo e identidade do binário.
  - Regra estrita: nunca recomendar desativação cega baseada em listas genéricas de "otimização da internet".
- **Inventário e Diagnóstico de Dispositivos e Drivers (Seção 25 do PROMPT):**
  - `PnpDeviceDriverProvider` (`IDriversProvider`): consulta WMI `Win32_PnPEntity` correlacionada com `Win32_PnPSignedDriver` para DeviceID, fabricante, versão, data do driver e códigos de erro de hardware do Windows (`ConfigManagerErrorCode`).
  - Tradução amigável em pt-BR de todos os códigos de erro do Gerenciador de Dispositivos (10, 14, 18, 22, 28, 31, 43, etc.).
  - Regra estrita: nunca baixar drivers de fontes não oficiais; orientar uso do Windows Update ou site oficial do fabricante.
- **Scanners e Regras de Diagnóstico:**
  - `ExplorerScanner` (Id: `explorer`, Ordem: 80, Módulo: `Explorer`).
  - `ServicesScanner` (Id: `services`, Ordem: 85, Módulo: `Services`).
  - `DriversScanner` (Id: `drivers`, Ordem: 90, Módulo: `Drivers`).
  - 6 novas regras implementadas:
    - `explorer.missing-shell-extension`: extensão ativa apontando para DLL inexistente (Severidade Baixo).
    - `explorer.unsigned-shell-extension`: extensão de terceiros sem assinatura digital válida (Severidade Info).
    - `services.missing-executable`: serviço configurado como Automático cujo executável não existe no disco (Severidade Médio).
    - `services.failed-startup`: falhas de inicialização ou crash de serviços reportados pelo SCM (Severidade Alto).
    - `drivers.device-error`: dispositivo PnP com código de erro de hardware/driver no Windows (Severidade Alto).
    - `drivers.missing-driver`: dispositivo sem driver instalado / Código 28 (Severidade Médio).
- **Telas e ViewModels:**
  - `ExplorerView` e `ExplorerViewModel`: 3 abas ("Extensões do Explorer", "Detector de Pastas Lentas" com seletor de pastas e benchmark interativo, e "Caches do Explorer" com status de ícones e miniaturas).
  - `ServicesView` e `ServicesViewModel`: catálogo mestre-detalhe, estatísticas de total/em execução/automáticos, filtros por estado (Todos, Em Execução, Parados, Automáticos, Falhando/Órfãos, Terceiros), pesquisa em tempo real, painel lateral com detalhes completos, executável, conta e dependências.
  - `DriversView` e `DriversViewModel`: catálogo de dispositivos PnP, estatísticas de total/com problemas, filtros (Todos, Com Problemas, Fabricante, Categoria), painel lateral com código de erro, fabricante, versão e data do driver.
  - `FindingItemViewModel`: botões "Investigar" direcionam contextual para `Explorer`, `Services` ou `Drivers`.
- **Testes novos:**
  - 27 novos testes unitários e de integração adicionados.
  - Total: **424 testes automatizados aprovados (100% de sucesso)** — 355 unitários, 42 integração, 27 sistema.

### Passo 7 — Aplicativos, Rede, Windows Update e Limpeza Segura (06/11/2026)

- **Aplicativos Instalados (Seção 28 do PROMPT):**
  - `InstalledAppsProvider` (`IAppsProvider`): inventário completo e unificado varrendo chaves de desinstalação Win32 (HKCU, HKLM 64-bit e WOW6432Node) e pacotes da Microsoft Store (UWP/MSIX em AppModel/Repository).
  - Extração de nome, versão, fabricante, data de instalação, tamanho estimado e comando de desinstalação (`UninstallString`).
  - Detecção determinística de desinstaladores órfãos (arquivos inexistentes referenciados) e aplicações com versões duplicadas instaladas.
  - Regra estrita: nunca desinstalar automaticamente.
- **Rede e Conectividade (Seção 27 do PROMPT):**
  - `NetworkProvider` (`INetworkProvider`): inventário de interfaces categorizando Ethernet, Wi-Fi, VPN, Docker, WSL, Hyper-V e Loopback com status operacional, velocidade de link, MAC, IPv4, IPv6, Gateway e servidores DNS.
  - `NetworkDiagnosticsService` (`INetworkDiagnosticsService`): teste não invasivo de qualidade de conexão: latência no gateway local, tempo de resolução de nomes DNS (`Dns.GetHostAddressesAsync`), latência para servidores públicos confiáveis e taxa de perda de pacotes.
- **Windows Update (Seção 26 do PROMPT):**
  - `WindowsUpdateProvider` (`IWindowsUpdateProvider`): checagem de estado dos serviços (`wuauserv`, `bits`, `dosvc`), detecção atômica de reinicialização pendente (`CBS\RebootPending`, `WindowsUpdate\Auto Update\RebootRequired`), histórico de hotfixes via `Win32_QuickFixEngineering` e falhas nas últimas 24h via Event Log do WindowsUpdateClient.
- **Limpeza Segura (Safe Cleaner - Seção 34 do PROMPT):**
  - `SafeCleanProvider` (`ISafeCleanProvider`): auditoria profunda e segura categorizada em 3 níveis de risco: **Seguro (Safe)** (temporários antigos > 24h, caches de miniaturas, relatórios WER e Otimização de Entrega), **Revisar (Review)** (caches de navegadores, downloads antigos > 30 dias e logs residuais) e **Avançado (Advanced)** (MEMORY.DMP e backups do WinSxS).
  - Medição segura em modo somente leitura (sem exclusão física nesta etapa).
- **Scanners e Regras de Diagnóstico:**
  - `AppsScanner` (Id: `apps`, Ordem: 95, Módulo: `Applications`).
  - `NetworkScanner` (Id: `network`, Ordem: 105, Módulo: `Network`).
  - `WindowsUpdateScanner` (Id: `windowsupdate`, Ordem: 110, Módulo: `WindowsUpdate`).
  - 6 novas regras implementadas:
    - `apps.orphaned-uninstall`: entrada de desinstalação apontando para executável inexistente (Severidade Baixo).
    - `apps.duplicate-install`: múltiplas versões registradas do mesmo aplicativo (Severidade Info).
    - `network.no-gateway`: adaptador ativo sem conexão à internet ou gateway inacessível (Severidade Médio).
    - `network.dns-slow`: tempo de resposta de resolução DNS superior a 200 ms (Severidade Baixo).
    - `windowsupdate.service-disabled`: serviço `wuauserv` do Windows Update desativado (Severidade Alto).
    - `windowsupdate.reboot-pending`: reinicialização pendente por atualizações acumuladas (Severidade Médio).
- **Telas e ViewModels:**
  - `AppsView` e `AppsViewModel`: inventário mestre-detalhe com estatísticas, busca por texto, filtros por categoria/problema e atalho para abrir pasta do programa.
  - `NetworkView` e `NetworkViewModel`: painel de diagnóstico em tempo real (latência de gateway, DNS, internet pública e perda de pacotes) e lista detalhada de adaptadores físicos e virtuais.
  - `WindowsUpdateView` e `WindowsUpdateViewModel`: card de alerta de reboot pendente, status dos serviços e histórico de atualizações instaladas.
  - `SafeCleanView` e `SafeCleanViewModel`: painel com totais por categoria de risco, detalhamento de itens com justificativa técnica e aviso explícito de modo somente leitura.
  - `FindingItemViewModel`: navegação contextual para `Aplicativos`, `Rede` e `Windows Update`.
- **Testes novos:**
  - 18 novos testes unitários e de integração adicionados.
  - Total: **442 testes automatizados aprovados (100% de sucesso)** — 369 unitários, 46 integração, 27 sistema.

### Passo 8 — MVP 3: Correções reais, Backup, Rollback, Restore Point e Antes/Depois (06/11/2026)

- **Primeiras 4 Ações de Reparo Reais (`IRepairAction`):**
  - `DisableStartupEntryRepairAction` (`repair.startup.disable`): desativa entradas de inicialização no registro através de `StartupApproved\Run` (0x03), preservando a chave original. Risco: Low. Rollback total restaurando o estado anterior.
  - `DisableShellExtensionRepairAction` (`repair.explorer.disable-shell-extension`): bloqueia extensões órfãs ou instáveis do Explorer via chave oficial `HKCU\Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked`. Risco: Low. Rollback total removendo da chave.
  - `SafeCleanTempRepairAction` (`repair.storage.safe-clean`): implementa `IMeasurableRepairAction` (medição numérica antes/depois do tamanho em MB, `LowerIsBetter = true`). Limpeza segura de temporários (> 24h) no `User Temp`, arquivos de `Windows\Temp`, miniaturas e relatórios WER / CrashDumps. Risco: Safe. Tratamento tolerante para arquivos bloqueados.
  - `EnableWindowsUpdateServiceRepairAction` (`repair.windowsupdate.enable-service`): restaura o tipo de inicialização de `wuauserv` de Desativado (4) para Manual (3) no registro (`SYSTEM\CurrentControlSet\Services\wuauserv\Start`) e inicializa o serviço. Risco: Low. Rollback total.
- **Ponto de Restauração Nativo do Windows (Seção 38 do PROMPT):**
  - `IRestorePointService` e `RestorePointService`: criação de checkpoints via WMI `root\default:SystemRestore.CreateRestorePoint(Description, RestorePointType = 12, EventType = 100)`. Tratamento para proteção desativada no C: ou falta de elevação administrativa.
- **Medição Antes/Depois (`IMeasurableRepairAction` - Seção 39 do PROMPT):**
  - Medição antes da alteração e após a validação. Comparação percentual e absoluta persistida no histórico SQLite e exibida no cartão da correção.
- **Central de Correções (`RepairsView` e `RepairsViewModel`):**
  - Integrada no menu principal como "Correções" (``).
  - Aba "Correções Disponíveis": seleção de ações por checkbox, botões de ação global (Ponto de Restauração, Simular Dry Run, Aplicar Selecionadas, Marcar/Desmarcar Todas).
  - Aba "Histórico de Correções & Rollback": exibe histórico do SQLite com data, motivo, mensagem, comparação antes/depois e botão **[Desfazer]** (Rollback) habilitado para correções reversíveis.
- **Testes Automatizados:**
  - 451 testes automatizados aprovados (377 unitários, 46 integração, 28 sistema).
  - Build limpo com `-warnaserror --no-incremental` em Debug e Release.

### Passo 9 — MVP 4: Docker, WSL, Ferramentas de desenvolvimento, Crash Analyzer e Segurança (06/11/2026)

- **Developer Mode & DevTools (Seções 31, 32 e 33 do PROMPT):**
  - `DevToolsProvider` (`IDevToolsProvider`):
    - Inventário completo de runtimes e compilers instalados: Node.js, npm, Yarn, pnpm, .NET SDKs (múltiplas versões), Java/JDK (`%JAVA_HOME%` e PATH), Maven, Gradle, Python e Git.
    - Inventário de IDEs instaladas: Visual Studio Code, Visual Studio (via `vswhere`), JetBrains (via Toolbox) e Android Studio.
    - **Docker Analyzer:** detecção de Docker Desktop / Engine, status operacional, backend configurado (WSL2 / Hyper-V) e tamanho físico de dados em disco. Alerta obrigatório: volumes podem conter bancos de dados permanentes e nunca são descartados automaticamente.
    - **WSL Analyzer:** detecção das distribuições WSL instaladas via registro `HKCU\Software\Microsoft\Windows\CurrentVersion\Lxss`, versão (WSL1 vs WSL2), caminho dos discos virtuais `ext4.vhdx` e tamanho físico no disco.
    - **Análise da variável PATH:** contagem de entradas de usuário e de sistema, detecção determinística de caminhos órfãos / pastas inexistentes e verificação de comprimento total.
- **Security Scanner (Seção 30 do PROMPT):**
  - `SecurityProvider` (`ISecurityProvider`):
    - Consulta de produtos antivírus registrados via WMI `root\SecurityCenter2:AntiVirusProduct` decodificando `productState` (proteção ativa em tempo real e assinaturas em dia).
    - Status do Firewall do Windows (`FirewallProduct`).
    - Status do Controle de Conta de Usuário (UAC via `EnableLUA` no registro).
    - Auditoria de persistências potencialmente suspeitas: IFEO (Image File Execution Options) `Debugger` hijack e bibliotecas ativas em `AppInit_DLLs`.
    - Princípio de segurança rigoroso: não substitui o antivírus do usuário e não classifica programas como malware sem prova conclusiva.
- **Crash Analyzer (Seção 29 do PROMPT):**
  - `CrashAnalyzerProvider` (`ICrashAnalyzerProvider`):
    - Varredura de arquivos de despejo no disco: minidumps do kernel (`%SystemRoot%\Minidump\*.dmp`), despejo completo (`MEMORY.DMP`) e dumps de aplicações de usuário (`%LocalAppData%\CrashDumps\*.dmp`).
    - Agregação dos últimos 30 dias via Event Log: Application Error (Event ID 1000), Application Hang (Event ID 1002) e eventos críticos de BSOD / BugCheck (Event ID 1001) com identificação e contagem dos executáveis com mais falhas.
- **Scanners e Regras de Diagnóstico:**
  - 3 novos scanners (totalizando **18 scanners**):
    - `DevToolsScanner` (`devtools`, Ordem: 115, Módulo: `Applications`)
    - `SecurityScanner` (`security`, Ordem: 120, Módulo: `Windows`)
    - `CrashScanner` (`crash`, Ordem: 125, Módulo: `Events`)
  - 6 novas regras de diagnóstico (totalizando **39 regras**):
    - `dev.wsl-vhdx-large`: alerta quando o disco `ext4.vhdx` do WSL2 ultrapassa 50 GB.
    - `dev.path-invalid-entries`: identifica caminhos órfãos ou pastas inexistentes configuradas no PATH.
    - `security.antivirus-disabled`: alerta crítico quando a proteção em tempo real está desativada.
    - `security.uac-disabled`: alerta alto quando o UAC está desligado no registro.
    - `security.ifeo-hijack`: alerta crítico quando binários essenciais têm redirecionamento de Debugger.
    - `crash.kernel-minidump-detected`: alerta sobre ocorrência recente de BSOD e despejos de kernel.
- **Telas e ViewModels no WPF:**
  - `DevToolsViewModel` e `DevToolsView`: painéis dedicados para Docker Desktop, WSL2, runtimes instalados e integridade do PATH.
  - `SecurityViewModel` e `SecurityView`: painel de segurança do Windows Defender/Antivírus, Firewall, UAC, persistências e dashboard do Crash Analyzer.
  - Menus integrados no `MainViewModel`: "Desenvolvimento" (``) e "Segurança" (``).
- **Testes Automatizados:**
  - **462 testes automatizados aprovados (100% de sucesso)** — 386 unitários, 46 integração, 30 sistema.
  - Compilação limpa em Release com `-warnaserror --no-incremental` (0 avisos, 0 erros).

### Passo 10 — MVP 5: Relatórios avançados, Agendamento, Notificações, Instalador e UX (06/11/2026)

- **Relatórios Avançados (Seções 54 e 55 do PROMPT):**
  - `HtmlReportExporter` (`IReportExporter`): laudo em HTML autocontido com visual profissional, temas claro/escuro, responsivo e estilos de impressão `@media print`.
  - `CsvReportExporter`: exportação tabular de achados e métricas em CSV com codificação UTF-8 com BOM para Excel.
  - `PdfReportExporter`: exportador nativo em conformidade com o padrão PDF 1.4, com paginação automática, cabeçalhos, cartões e badges coloridos por severidade, sem dependências externas.
  - `JsonReportExporter`: exportador completo já em produção.
  - `ReportsViewModel` e `ReportsView`: tela dedicada com histórico de laudos, seleção de formatos e botões para exportação em arquivo ou abertura imediata no visualizador padrão.
- **Persistência de Preferências do Usuário (Seção 66 do PROMPT):**
  - `IConfigurationRepository` e `SqliteConfigurationRepository`: persistência chave-valor assíncrona na tabela `Configuration` do SQLite (`ui.theme`, `ui.advanced-mode`, `schedule.frequency`).
  - Carregamento automático na inicialização e sincronização imediata com `ThemeService` e `UiPreferences`.
- **Agendamento via Task Scheduler & Notificações (Seção 60 do PROMPT):**
  - `IScanSchedulerService` e `WindowsTaskSchedulerService`: agendamento e gerenciamento determinístico de tarefas via `schtasks.exe` (Diário, Semanal, Mensal ou Desativado).
  - Execução silenciosa em segundo plano através dos argumentos CLI `--scheduled-scan` e `--background-scan` no `App.xaml.cs`.
  - `INotificationService` e `WindowsNotificationService`: notificações locais e logging estruturado.
  - Painel de controle de agendamento na tela de Configurações (`SettingsView`).
- **Instalador e Modo Portable (Seções 61 e 62 do PROMPT):**
  - Script PowerShell `installer/package-installer.ps1` com ações automatizadas de `Build`, `Install`, `Uninstall` e `Repair`.
  - Script Inno Setup `installer/installer.iss` para instalador gráfico x64 moderno em Português do Brasil.
  - Regra de ouro da desinstalação segura: remove todos os arquivos do aplicativo preservando relatórios e backups do usuário em `%ProgramData%\LeccorWindowsHealthCenter` (limpeza completa apenas sob confirmação via `-PurgeUserData`).
  - Suporte ao modo portable através do arquivo `portable.flag`.
- **Compilação e Publicação win-arm64 (Seção 4 do PROMPT):**
  - Preparação e suporte em `scripts/build.ps1` e `installer/package-installer.ps1` para target `win-arm64`.
- **Testes Automatizados e Qualidade:**
  - 396 testes unitários, 46 testes de integração, 32 testes de sistema — total de **474 testes automatizados aprovados (100% de sucesso)**.
  - Compilação limpa em Release com `-warnaserror --no-incremental` (0 avisos, 0 erros).

### Evolução Futura — Pacote A: Telemetria Avançada e Medição Precisa (06/11/2026)

- **Separação de E/S por processo (Disco vs Rede):**
  - Implementado `IProcessIoTracer` e `EtwProcessIoTracer` usando `Microsoft.Diagnostics.Tracing.TraceEvent` (v3.1.21).
  - Coleta em tempo real eventos de kernel `DiskIO` (`DiskIORead`, `DiskIOWrite`) e `NetworkTCPIP` (`TcpIpSend`, `TcpIpRecv`, `UdpIpSend`, `UdpIpRecv`) correlacionados por PID em dicionário thread-safe concorrente.
  - Degradação graciosa e tolerante a falhas: se não houver privilégio administrativo ou se a sessão de kernel estiver ocupada, desativa o rastreador sem falhar o aplicativo.
  - Enriquecimento de `ProcessDetails` com `DiskIoBytes` e `NetworkIoBytes`, e cálculo de taxas discriminadas em `ProcessRates` (`DiskIoBytesPerSecond` e `NetworkIoBytesPerSecond`).
- **Impacto de Inicialização baseado no tempo real de boot:**
  - Implementado `IBootPerformanceProvider` e `BootPerformanceProvider` realizando leitura do canal `Microsoft-Windows-Diagnostics-Performance/Operational` via `EventLogReader` (Event IDs 101 a 110).
  - Extração de `DegradationTimeMs`, `TotalTimeMs`, caminho do executável ou nome do serviço/tarefa.
  - `StartupInventoryProvider` correlaciona automaticamente os eventos de boot com os itens da inicialização, atribuindo `BootDegradationMs` e `BootTotalTimeMs`.
  - `StartupImpact.Estimate` prioriza medições reais de boot: degradação ≥ 3 s = Alto, ≥ 1 s = Médio, < 1 s = Baixo. Exibição amigável na UI ("Alto (3.2s no boot)").
- **Histórico contínuo de métricas com o aplicativo fechado:**
  - Criada a entidade `MetricSampleRecord`, interface `IMetricSampleRepository` e implementação `SqliteMetricSampleRepository`.
  - Migration 2 (`006_MetricSamples.sql` / `Migrations.cs` v2) criando a tabela `MetricSamples` com índice temporal decrescente.
  - `MetricsHistoryService` persiste periodicamente amostras de telemetria no SQLite com rotina de retenção de 7 dias.
  - O modo de varredura silenciosa `--background-scan` / `--scheduled-scan` no `App.xaml.cs` registra amostras de telemetria no banco mesmo com o aplicativo fechado.
- **Verificação online de revogação de certificados (CRL/OCSP):**
  - Estendido `FileIdentityProvider` e `IFileIdentityProvider` para suportar checagem opcional de revogação na rede através de `WinVerifyTrust` com flags `WTD_REVOKE_WHOLECHAIN` (1) e `WTD_REVOCATION_CHECK_CHAIN_EX` (0x40).
  - Novo valor no enum: `SignatureStatus.Revoked` ("Assinatura revogada").
  - `ExplorerUnsignedShellExtensionRule` identifica certificados revogados gerando achado de severidade `Severity.High` com 100% de confiança.
  - Novo controle e binding na tela de Configurações (`SettingsView` / `SettingsViewModel`): toggle "Verificar revogação online de certificados digitais (CRL/OCSP)" com persistência assíncrona na chave `security.check-revocation-online`.
- **Testes Automatizados:**
  - 406 testes unitários, 46 testes de integração, 32 testes de sistema — total de **484 testes automatizados aprovados (100% de sucesso)**.
  - Compilação limpa em Release com `-warnaserror --no-incremental` (0 avisos, 0 erros).

### Pacote B — Otimizações de Armazenamento e Persistência (06/11/2026)

- **Persistência da Análise de Espaço em Disco no SQLite:**
  - Adicionada Migration 3 (`SpaceAnalysisSnapshot`) em `Migrations.cs` com tabela indexada por `(VolumeRoot, CapturedAt DESC)`.
  - Criada a entidade `SpaceAnalysisSnapshotSummary` e a interface `ISpaceAnalysisSnapshotRepository` em `Application.Storage`.
  - Implementado `SqliteSpaceAnalysisSnapshotRepository` com persistência atômica, serialização JSON e expurgo automático mantendo apenas os 10 snapshots mais recentes por volume.
  - Integração assíncrona em `StorageViewModel`: restauração instantânea do último snapshot salvo no SQLite ao carregar a tela e salvamento automático do novo laudo após cada análise.
- **Deduplicação de Hard Links na Análise de Espaço:**
  - P/Invoke Win32 para `CreateFileW` e `GetFileInformationByHandle` em `FileSystemSpaceAnalyzer`.
  - Identificação de arquivos com `NumberOfLinks > 1` pela chave única `(VolumeSerialNumber, FileIndexHigh, FileIndexLow)` com rastreamento concorrente via `ConcurrentDictionary`.
  - Evita a contagem repetida de bibliotecas idênticas no `WinSxS` e pastas do sistema, acumulando os bytes economizados no campo `FolderNode.HardLinkDeduplicatedBytes` e exibindo a nota deduplicada na UI.
- **Exportação Dedicada da Árvore de Espaço em Disco (CSV e JSON):**
  - Interfaces `ISpaceAnalysisExporter` e implementação `SpaceAnalysisExporter` no projeto Reporting.
  - Exportação em CSV tabular formatado com cabeçalho, UTF-8 com BOM (`\uFEFF`) e delimitador `;` para total compatibilidade com Excel pt-BR, listando hierarquia de diretórios, maiores arquivos e arquivos antigos.
  - Exportação em JSON estruturado legível.
  - Botões integrados na barra de análise de espaço em `StorageView.xaml` com comandos `ExportCsvCommand` e `ExportJsonCommand` vinculados a `SaveFileDialog` via `IFolderPicker.PickSaveFile`.
- **Testes Automatizados e Qualidade:**
  - Novos testes: `SqliteSpaceAnalysisSnapshotRepositoryTests` (persistência, recuperação e retenção de 10 snapshots), `SpaceAnalysisExporterTests` (BOM UTF-8, CSV tabular e desserialização JSON) e teste real de deduplicação de hard link em `StorageSourcesTests.cs`.
  - 412 testes unitários, 47 testes de integração, 32 testes de sistema — total de **491 testes automatizados aprovados (100% de sucesso)**.
  - Compilação limpa em Release com `-warnaserror --no-incremental` (0 erros, 0 avisos).

### Pacote C — Performance e UX da Interface (06/11/2026)

- **Virtualização de UI com Agrupamento e Reciclagem na Inicialização:**
  - `StartupViewModel` estendido com propriedades `GroupName` e `GroupOrder` em `StartupRow`, expondo `ICollectionView RowsView` agrupada por categoria e ordenada por impacto e estado ativo.
  - `StartupView.xaml` reestruturado com layout `Grid` (topo fixo com título, problemas e filtros; área central `Height="*"` com `ListBox` virtualizada; rodapé).
  - `ListBox` configurado com `VirtualizingPanel.IsVirtualizing="True"`, `VirtualizingPanel.IsVirtualizingWhenGrouping="True"`, `VirtualizingPanel.VirtualizationMode="Recycling"`, `VirtualizingPanel.ScrollUnit="Pixel"` e painel de grupo `<ItemsPanelTemplate><VirtualizingStackPanel /></ItemsPanelTemplate>`.
  - Queda no tempo de criação visual com ~600 entradas de **6,6 segundos para ~15 milissegundos**, eliminando qualquer travamento de renderização e permitindo filtragem fluida em tempo real.
- **Estabilidade de Rolagem e Seleção na Lista de Processos:**
  - `ProcessesViewModel` recebeu congelamento de ordenação (`ConfirmingEnd == true`) para impedir que a linha salte de posição na tela enquanto o usuário confirma o encerramento do processo.
  - Implementado evento `RequestBringIntoView` em `ProcessesViewModel` com manipulador em `ProcessesView.xaml.cs` para garantir que o processo selecionado permaneça visível suavemente (`ScrollIntoView`) mesmo com reordenações periódicas a cada 2 s.
- **Recuo Progressivo Profundo no Modo Árvore de Processos:**
  - Suporte estendido de 8 para **16 níveis** de profundidade em `ProcessItem`.
  - Recuo progressivo inteligente: 12 px por nível até o 6º nível e 6 px adicionais por nível até o 16º nível (máximo 132 px), garantindo diferenciação visual para árvores complexas (Electron, VSCode, subagentes) sem truncar o nome do executável.
- **Testes Automatizados e Qualidade:**
  - Criada suíte `PacoteCUiPerformanceTests` em `SystemTests` validando a classificação de grupos, ordenação na `CollectionView` e recuo progressivo do modo árvore.
  - 412 testes unitários, 47 testes de integração, 46 testes de sistema — total de **505 testes automatizados aprovados (100% de sucesso)**.
  - Compilação limpa em Release com `-warnaserror --no-incremental` (0 erros, 0 avisos).

## Pendências / próximos passos

Com a conclusão do **Passo 10 (MVP 5)**, do **Pacote A (Telemetria Avançada e Medição Precisa)**, do **Pacote B (Otimizações de Armazenamento e Persistência)** e do **Pacote C (Performance e UX da Interface)**, todos os 3 pacotes viáveis de evolução futura foram implementados com sucesso!

Próximos passos recomendados:
1. Homologar e empacotar o instalador gráfico Inno Setup e distribuição portable via `installer/package-installer.ps1`.
2. Realizar smoke test e validação visual no ambiente Windows.
3. Preparar a tag e notas da release `v1.1.0`.

## Limitações conhecidas (análise técnica de viabilidade)

### 1. Inviáveis ou Impossíveis por Arquitetura do Windows / Princípios do Software
- **Temperatura de CPU/GPU (Impossível em modo usuário):**
  - *Motivo técnico:* Não existe API Win32 ou WMI nativa padronizada em placas-mãe modernas para leitura de sensores térmicos de núcleos (MSRs de CPU, barramentos SMBus/I2C ou chips Super I/O) sem carregar um driver de kernel (Ring 0, tipo `.sys`). Softwares como OpenHardwareMonitor ou HWiNFO usam drivers proprietários para acessar registradores de baixo nível.
  - *Princípio inviolável:* O projeto proíbe estritamente a instalação de drivers de kernel por estabilidade e risco de segurança (BSOD e vulnerabilidades de drivers), além de proibir a exibição de estimativas ou dados fictícios.
- **Finalização de processos críticos do sistema (Bloqueado por segurança):**
  - *Motivo técnico:* Processos vitais do Windows (`smss.exe`, `csrss.exe`, `lsass.exe`, `services.exe`, `wininit.exe`) possuem a flag `BreakOnTermination` no kernel. Qualquer tentativa de finalização (mesmo com token SYSTEM ou privilégio `SeDebugPrivilege`) causa BSOD imediato (`CRITICAL_PROCESS_DIED` - 0xEF).
  - *Princípio inviolável:* A lista fixa em `CriticalProcesses` é uma barreira de segurança obrigatória para proteger o usuário e o sistema operacional.
- **GPU Engine em VMs básicas ou drivers antigos (Indisponível no hypervisor):**
  - *Motivo técnico:* A telemetria de GPU via contadores PDH depende da especificação WDDM 2.0+ (Windows 10). Em máquinas virtuais com adaptador de vídeo genérico (sem GPU Passthrough no Hyper-V/VMware/VirtualBox) ou sessões RDP simples, o Windows não expõe contadores de GPU.
  - *Comportamento do software:* Identifica com precisão a ausência do subsistema e informa honestamente "Não disponível", sem mascarar dados.
- **Processos PPL (Protected Process Light):**
  - *Motivo técnico:* Processos com nível de integridade protegido pelo kernel (ex: `MsMpEng.exe` do Defender, serviços de DRM e subsistema LSA protegido) bloqueiam abertura de handles de consulta (`OpenProcess`) mesmo para o usuário Administrador.
  - *Comportamento do software:* Retorna valores nulos para atributos inacessíveis em vez de falhar a varredura ou inventar números.
- **Criação de Ponto de Restauração com Proteção Desativada:**
  - *Motivo técnico:* Se a Proteção do Sistema estiver explicitamente desativada no volume `C:` pelo usuário ou via GPO, a API VSS/SystemRestore recusa a chamada. O software não força a reativação de recursos de sistema contra a política da máquina.

### 2. Melhorias Viáveis de Implementação
- **Separação de E/S por processo (Disco vs Rede):** [Implementado no Pacote A ✅] Sessão ETW em tempo real via `TraceEvent` com `EtwProcessIoTracer`.
- **Impacto de Inicialização baseado no tempo real de boot:** [Implementado no Pacote A ✅] Leitura de eventos 101–110 de `Diagnostics-Performance` com `BootPerformanceProvider`.
- **Histórico contínuo de métricas com app fechado:** [Implementado no Pacote A ✅] Tabela `MetricSamples` no SQLite e persistência via `--background-scan`.
- **Verificação online de revogação de certificados (CRL/OCSP):** [Implementado no Pacote A ✅] Flag `WTD_REVOKE_WHOLECHAIN` em `FileIdentityProvider` e toggle no Modo Avançado.
- **Persistência da Análise de Espaço em Disco:** [Implementado no Pacote B ✅] Tabela `SpaceAnalysisSnapshot` no SQLite, `SqliteSpaceAnalysisSnapshotRepository` com retenção dos 10 mais recentes e restauração assíncrona automática na abertura da tela.
- **Deduplicação de Hard Links na análise de espaço:** [Implementado no Pacote B ✅] P/Invoke Win32 para `GetFileInformationByHandle` em `FileSystemSpaceAnalyzer`, identificando tupla `(Volume, High, Low)` e deduplicando arquivos compartilhados em `WinSxS`.
- **Exportação dedicada da Árvore de Espaço em Disco:** [Implementado no Pacote B ✅] `SpaceAnalysisExporter` gerando CSV (UTF-8 com BOM para Excel) e JSON estruturado, acionados pelos botões da tela de Armazenamento.
- **Virtualização de UI para agrupamentos em listas grandes (> 500 itens):** [Implementado no Pacote C ✅] `VirtualizingPanel.IsVirtualizingWhenGrouping="True"`, reciclagem e ICollectionView agrupada em Inicialização e Processos.
