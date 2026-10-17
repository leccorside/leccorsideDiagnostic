# Guia de desenvolvimento

## Ambiente

- Windows 10/11 x64
- .NET SDK 10 (fixado por `global.json`, `rollForward: latestFeature`)
- VS 2026 / VS Code com C# Dev Kit / Rider

## Estrutura

```text
src/        9 projetos (ver ARCHITECTURE.md)
tests/      UnitTests · IntegrationTests · SystemTests
docs/       documentação técnica
scripts/    build.ps1
installer/  (Passo 10)
```

Versões de pacotes ficam **só** em `Directory.Packages.props`. Configurações de compilação comuns em
`Directory.Build.props` (nullable, analisadores `latest-recommended`, estilo no build).

## Convenções

- Textos de UI, logs e documentação em **português do Brasil**; identificadores em inglês.
- Logs estruturados com placeholders (`"Scanner {ScannerId} concluído em {DurationMs} ms"`), nunca interpolação.
- Toda operação longa recebe `CancellationToken`.
- Processos externos **somente** via `ICommandExecutor`.
- Campos privados `_camelCase`; um tipo público por arquivo (exceto records pequenos relacionados).

## Como adicionar um scanner

1. Criar a classe em `src/LeccorWindowsHealthCenter.Diagnostics/<Area>/` herdando `DiagnosticScannerBase`.
2. Emitir `Metric`/`Observation` em `CollectAsync` — sem decidir nada.
3. Registrar em `Diagnostics/DependencyInjection.cs`: `services.AddSingleton<IDiagnosticScanner, MeuScanner>();`
4. Testar o parser/coleta com dados gravados (sem depender do estado da máquina).
5. Documentar em `docs/DIAGNOSTICS.md`.

## Como adicionar uma regra

1. Implementar `IRule` (mesmo projeto do scanner), declarando `RequiredScanners` e `Parameters` com justificativa.
2. Usar `MetricSeries` para duração/recorrência/amostras mínimas.
3. Toda `Finding` com evidência, impacto, recomendação e (se houver correção) `RepairActionId` + `RepairRisk`.
4. Registrar: `services.AddSingleton<IRule, MinhaRegra>();`
5. Teste unitário com série que **não** deve disparar e série que deve.
6. Documentar em `docs/RULE_ENGINE.md`.

## Como adicionar uma ação de reparo (a partir do MVP 3)

1. Implementar `IRepairAction` (e `IMeasurableRepairAction` quando houver métrica de antes/depois).
2. `CaptureStateAsync` precisa capturar tudo que `ExecuteAsync` altera; `RollbackAsync` restaura só a partir do backup.
3. Testes com dublês — nunca alterar o sistema real em teste automatizado.
4. Documentar em `docs/REPAIR_ENGINE.md`.

## Testes

| Projeto | O que cobre | Regras |
|---|---|---|
| UnitTests | RuleEngine, MetricSeries, RiskAnalyzer, RepairEngine, ScoringEngine, ScanOrchestrator, ExecutablePolicy, redação, domínio | sem I/O real |
| IntegrationTests | SQLite real (migrations/repositórios), CommandExecutor real (`hostname`, `ping 127.0.0.1`), logging em arquivo, backups, métricas Win32 | só comandos inofensivos e somente-leitura |
| SystemTests | host real do App (mesma DI) com diretório temporário, varredura ponta a ponta | sem abrir janelas |

```powershell
dotnet test LeccorWindowsHealthCenter.sln
```

## Problemas conhecidos de ambiente

- `MSB3061 ... _wpftmp.csproj.nuget.dgspec.json being used by another process`: arquivo temporário do build WPF
  travado momentaneamente (antivírus/extensão do editor). Repetir o build resolve.
