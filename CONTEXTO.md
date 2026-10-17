# CONTEXTO — Leccor Windows Health Center

Resumo vivo do projeto para retomar o trabalho em qualquer sessão. **Atualizar ao final de cada ação.**
Requisitos completos: `PROMPT.txt` (fonte da verdade). Plano: `PASSOS.md`. Referência técnica: `DOCUMENTACAO.md`.

Última atualização: **17/10/2026 — Passo 1 concluído, aguardando autorização para o Passo 2.**

---

## Estado atual

- Solution `LeccorWindowsHealthCenter.sln` (formato `.sln` clássico, pedido no PROMPT) com 9 projetos em `src/` e 3 de teste.
- .NET SDK 10.0.103, WPF, CommunityToolkit.Mvvm 8.4.0, Microsoft.Extensions.* 10.0.11, Microsoft.Data.Sqlite 10.0.11,
  Serilog 4.3.1 (+ Extensions.Logging 10.0.0, Sinks.File 7.0.0, Formatting.Compact 3.0.0), xUnit 2.9.3.
- Infraestrutura de diagnóstico completa e testada: orquestrador, RuleEngine, RiskAnalyzer, ScoringEngine,
  RepairEngine (sem ações concretas), executor de comandos seguro, SQLite com migrations, backups, logs.
- App abre sem admin, mostra Dashboard (score "SEM DADOS", CPU/RAM/disco ao vivo), Varredura (rápida/completa/
  personalizada, progresso, cancelar, resultado) e Configurações (tema claro/escuro). Demais menus: páginas
  "em construção" com o MVP previsto.
- **Nenhum scanner nem regra de produção ainda** — uma varredura hoje termina sem dados e, por design, sem score.
- Build Debug e Release com `-warnaserror`: 0 avisos, 0 erros. Testes: 136/136 (115 unitários, 16 integração, 5 sistema).
- Consumo medido do app ocioso no Dashboard: ~0,3% CPU, ~90–110 MB privados (~140–170 MB working set).

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

## Pendências / próximos passos

Na ordem combinada no `PASSOS.md`:

1. **Passo 2** — Hardware, CPU, Memória e GPU (scanners + regras + telas Performance e Hardware).
2. Passo 3 — Discos (SMART/NVMe quando disponível) e Armazenamento.
3. Passo 4 — Processos e Inicialização (somente leitura).
4. Passo 5 — Event Viewer, Integridade do Windows (DISM/SFC/CHKDSK não destrutivos) e **processo elevado + IPC**.
5. Passos 6–10 — MVP 2 a 5 (ver `PASSOS.md`).

Pequenos itens de UX anotados: tela de detalhes de finding dedicada (hoje é um `Expander`); criar `PRODUCT.md`/`DESIGN.md`
com `/impeccable init` e `/impeccable document` para fixar o sistema visual antes das telas do Passo 2.

## Limitações conhecidas (deliberadas)

- Sem scanners/regras/ações concretas no Passo 1 — a seção 75 pede a infraestrutura antes dos scanners.
- GPU no dashboard aparece "não disponível": sem fonte confiável ainda, não exibimos estimativa.
- Preferências alteradas na UI (tema, modo avançado) valem só na sessão; persistência na tabela `Configuration` no Passo 10.
- Dry Run só é configurável via `appsettings.json` (UI de Dry Run chega com as correções no Passo 8).
- Processo elevado (`*.Elevated.exe`) e IPC ainda não existem: comandos/ações que exigem admin são recusados.
- Backups em `%ProgramData%` recebem ACL do usuário que criou a pasta; outro usuário Windows na mesma máquina
  não conseguirá gravar backups ali (falha segura: a correção é abortada antes de alterar algo).
- Exportação de relatório apenas JSON (HTML/CSV/PDF no MVP 5). Instalador não existe ainda (Passo 10).
- `win-arm64` preparado mas não publicado no CI até haver validação em hardware ARM.
