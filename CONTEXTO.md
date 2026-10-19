# CONTEXTO — Leccor Windows Health Center

Resumo vivo do projeto para retomar o trabalho em qualquer sessão. **Atualizar ao final de cada ação.**
Requisitos completos: `PROMPT.txt` (fonte da verdade). Plano: `PASSOS.md`. Referência técnica: `DOCUMENTACAO.md`.

Última atualização: **19/10/2026 — Passo 4 concluído (v0.4.0): Processos e Inicialização; aguardando autorização para o Passo 5.**

---

## Estado atual

- Solution `LeccorWindowsHealthCenter.sln` (formato `.sln` clássico, pedido no PROMPT) com 9 projetos em `src/` e 3 de teste.
- .NET SDK 10.0.103, WPF, CommunityToolkit.Mvvm 8.4.0, Microsoft.Extensions.* 10.0.11, Microsoft.Data.Sqlite 10.0.11,
  Serilog 4.3.1 (+ Extensions.Logging 10.0.0, Sinks.File 7.0.0, Formatting.Compact 3.0.0), System.Management 10.0.11,
  Microsoft.Extensions.Hosting.Abstractions 10.0.11, xUnit 2.9.3.
- Infraestrutura de diagnóstico completa e testada: orquestrador, RuleEngine, RiskAnalyzer, ScoringEngine,
  RepairEngine (sem ações concretas), executor de comandos seguro, SQLite com migrations, backups, logs.
- **Scanners em produção (Passo 2):** Hardware, CPU, Memória e GPU, com histórico de fundo de 30 min e 5 regras
  (CPU sustentada, possível limitação de frequência, pressão de memória, commit alto, possível vazamento).
- **Scanners em produção (Passo 3):** Discos e Armazenamento, com 5 regras (pouco espaço livre, disco sinaliza problema,
  temperatura alta, TRIM desativado em SSD, temporários/caches grandes como recomendação).
- **Scanner em produção (Passo 4):** Inicialização (somente leitura), com 3 regras (entrada ativa apontando para arquivo
  inexistente; executável em pasta temporária; script oculto via hospedeiro do Windows). 7 scanners e 13 regras no total.
- App abre sem admin: Dashboard (score com cobertura "Avaliou X de 6 áreas", CPU/RAM/disco/GPU ao vivo), Varredura,
  **Performance**, **Processos**, **Armazenamento**, **Hardware**, **Inicialização** e Configurações. Demais menus:
  páginas "em construção" com o MVP previsto.
- Build Debug e Release com `-warnaserror` e `--no-incremental`: 0 avisos, 0 erros. Testes: 356/356 (294 unitários,
  37 integração, 25 sistema).
- Telas de Performance e Hardware (16 correções) e Armazenamento (14 correções + 4 decisões) passaram pelo teste de
  estresse break-ui; o seletor de dados de desenvolvimento (só DEBUG) cobre as três telas.
- Consumo medido (Release, histórico ativo): 0,17% CPU no Dashboard, 0,46% na Performance, 0,00% minimizado;
  101–120 MB privados (~160–180 MB working set).

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

## Pendências / próximos passos

Na ordem combinada no `PASSOS.md`:

1. **Passo 5** — Event Viewer (inclui eventos de driver de GPU e de disco: Disk 7/153, NTFS), Integridade do Windows e
   **processo elevado + IPC** (habilita SMART detalhado, desgaste e horas ligadas dos discos).
2. Passos 6–10 — MVP 2 a 5 (ver `PASSOS.md`).

Itens do Passo 4 para depois: desativar entrada de inicialização com backup e desfazer (Correções, Passo 8); a lista de
processos reordena a cada 2 s pela coluna escolhida (como o Gerenciador de Tarefas), então a linha selecionada pode
sair da área visível; abrir a tela Inicialização com ~600 entradas custa ~6,6 s de CPU (todas as linhas são criadas;
virtualizar os grupos resolveria); no modo árvore, níveis além do 8º não recuam mais.

Pequenos itens de UX anotados: tela de detalhes de finding dedicada (hoje é um `Expander`); exportar a análise de
espaço (relatórios chegam no Passo 10); análise de espaço não é salva no histórico (fica em memória na sessão).

## Limitações conhecidas (deliberadas)

- Temperatura de CPU/GPU não é exibida: não há fonte confiável sem driver de kernel; não será estimada.
- O histórico de fundo existe só enquanto o app está aberto (30 min em memória). Logo após abrir, regras de duração
  (CPU sustentada, vazamento) ainda não têm amostras suficientes e simplesmente não concluem.
- Uso de CPU por processo não é medido em processos protegidos do sistema sem administrador (a tela informa quantos).
- E/S por processo soma disco, rede e dispositivos (`GetProcessIoCounters`): separar exige ETW/administrador.
- Impacto de inicialização é estimado pelo consumo atual (memória privada e CPU acumulada dos processos do mesmo
  executável), não pelo tempo de boot medido; item que não está rodando fica "não medido".
- Assinatura verificada sem consulta de revogação (offline e rápida): certificado revogado depois de assinar não é detectado.
- Processos protegidos não podem ser finalizados pelo app (lista fixa em `CriticalProcesses`); outros processos de
  administrador falham com "sem permissão" até existir o processo elevado (Passo 5).
- GPU sem contadores "GPU Engine" (VMs, drivers antigos) aparece como "não disponível".
- Discos sem administrador: vida útil, horas ligadas e SMART detalhado ficam "não disponível" (o Windows nega o acesso);
  temperatura só quando o dispositivo informa via IOCTL. Chegam com o processo elevado (Passo 5).
- Análise de espaço usa tamanho lógico: links físicos (WinSxS) contam em cada pasta, arquivos comprimidos/esparsos
  contam pelo tamanho lógico. Pastas sem permissão (sem admin) são contadas e avisadas, não lidas.
- A análise de espaço fica só em memória (não é salva no histórico); fechar o app perde o resultado.
- Preferências alteradas na UI (tema, modo avançado) valem só na sessão; persistência na tabela `Configuration` no Passo 10.
- Dry Run só é configurável via `appsettings.json` (UI de Dry Run chega com as correções no Passo 8).
- Processo elevado (`*.Elevated.exe`) e IPC ainda não existem: comandos/ações que exigem admin são recusados.
- Backups em `%ProgramData%` recebem ACL do usuário que criou a pasta; outro usuário Windows na mesma máquina
  não conseguirá gravar backups ali (falha segura: a correção é abortada antes de alterar algo).
- Exportação de relatório apenas JSON (HTML/CSV/PDF no MVP 5). Instalador não existe ainda (Passo 10).
- `win-arm64` preparado mas não publicado no CI até haver validação em hardware ARM.
