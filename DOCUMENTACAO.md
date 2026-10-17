# Documentação técnica — Leccor Windows Health Center

Documento central de referência. Detalhes por assunto em `docs/` (links ao final de cada seção).
**Atualizar ao final de cada ação** (regra do projeto).

Versão documentada: **0.1.0 (Passo 1 — fundação)**.

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
| `LiveMetricsProvider` | Monitoring | CPU/RAM/disco do dashboard via Win32 |
| `JsonReportExporter` | Reporting | Exportação do `ScanReport` em JSON |
| `HealthCenterHost` | App | Composition root (Generic Host + DI validada) |
| Telas | App/Views | Dashboard, Varredura, Configurações e páginas "em construção" para os demais módulos |

## 3. Interface

- Sidebar com o menu completo da seção 8. Módulos ainda não implementados mostram uma página honesta
  "Módulo em construção" com o MVP previsto — nunca dados não medidos.
- **Dashboard**: anel de score (ou "SEM DADOS"), resultado da última varredura (críticos, importantes, menores,
  recomendações, problema mais importante), uso atual de CPU/RAM com histórico de 2 min, disco do sistema e GPU
  ("não disponível" até o Passo 2). Logo de boas-vindas antes da primeira varredura.
- **Varredura**: rápida, completa, personalizada (16 módulos selecionáveis), progresso por scanner com
  "Cancelar varredura", resultado com score e lista de findings expansíveis respondendo às 6 perguntas da seção 72.
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

## 4. Configuração (`src/LeccorWindowsHealthCenter.App/appsettings.json`)

| Chave | Padrão | Descrição |
| --- | --- | --- |
| `HealthCenter:AdvancedMode` | `false` | Exibe detalhes técnicos (referências de evidência, comandos...) |
| `HealthCenter:Theme` | `System` | `System`, `Light` ou `Dark` |
| `HealthCenter:DashboardRefreshInterval` | `00:00:02` | Intervalo de amostragem do dashboard (mín. 1 s) |
| `Scan:MaxConcurrency` | `2` | Scanners em paralelo |
| `Scan:ScannerTimeout` | `00:05:00` | Tempo máximo por scanner |
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
| Logs | `...\Logs\lwhc-AAAAMMDD.log` (JSON compacto: `@t`, `@mt`, propriedades) | `.\Data\Logs` |
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
| `HardwareSnapshot` | fotografia do hardware (Passo 2) |
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

## 10. Testes (Passo 1)

| Projeto | Testes |
| --- | --- |
| UnitTests | 115 |
| IntegrationTests | 16 |
| SystemTests | 3 |
| **Total** | **134 — todos passando** (Debug e Release com `-warnaserror`) |
