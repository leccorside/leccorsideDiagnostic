# CONTEXTO — Leccor Windows Health Center

Resumo vivo do projeto para retomar o trabalho em qualquer sessão. **Atualizar ao final de cada ação.**
Requisitos completos: `PROMPT.txt` (fonte da verdade). Plano: `PASSOS.md`. Referência técnica: `DOCUMENTACAO.md`.

Última atualização: **18/10/2026 — Passo 3 concluído (v0.3.0); aguardando autorização para o Passo 4.**

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
- App abre sem admin: Dashboard (score com cobertura "Avaliou X de 6 áreas", CPU/RAM/disco/GPU ao vivo), Varredura,
  **Performance**, **Hardware**, **Armazenamento** e Configurações. Demais menus: páginas "em construção" com o MVP previsto.
- Build Debug e Release com `-warnaserror`: 0 avisos, 0 erros. Testes: 252/252 (212 unitários, 29 integração, 11 sistema),
  estáveis em 3 rodadas seguidas com as suítes em paralelo.
- Telas de Performance e Hardware passaram pelo teste de estresse break-ui (16 correções, ver Histórico).
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

## Pendências / próximos passos

Na ordem combinada no `PASSOS.md`:

1. **Passo 4** — Processos e Inicialização (somente leitura).
2. Passo 5 — Event Viewer (inclui eventos de driver de GPU e de disco: Disk 7/153, NTFS), Integridade do Windows e
   **processo elevado + IPC** (habilita SMART detalhado, desgaste e horas ligadas dos discos).
3. Passos 6–10 — MVP 2 a 5 (ver `PASSOS.md`).

Itens anotados do Passo 3: fixtures do seletor de dados de desenvolvimento (break-ui) para a tela Armazenamento ainda
não existem; "Abrir no Explorer" a partir da análise de espaço não foi feito (exige passar pela política de execução).

Pequenos itens de UX anotados: tela de detalhes de finding dedicada (hoje é um `Expander`); sombra "flutuante" nos tooltips.

## Limitações conhecidas (deliberadas)

- Temperatura de CPU/GPU não é exibida: não há fonte confiável sem driver de kernel; não será estimada.
- O histórico de fundo existe só enquanto o app está aberto (30 min em memória). Logo após abrir, regras de duração
  (CPU sustentada, vazamento) ainda não têm amostras suficientes e simplesmente não concluem.
- Uso de CPU por processo não é medido em processos protegidos do sistema sem administrador (a tela informa quantos).
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
