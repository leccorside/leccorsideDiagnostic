# Arquitetura

Clean Architecture + MVVM + módulos independentes, em C# / .NET 10 / WPF.

## Projetos e dependências

```text
                 ┌──────────────────────────┐
                 │  App (WPF, MVVM, host)   │  composition root
                 └────────────┬─────────────┘
       ┌──────────────┬───────┼────────┬─────────────┬────────────┐
       ▼              ▼       ▼        ▼             ▼            ▼
 Infrastructure  Diagnostics  Repair  Monitoring  Reporting   (todos dependem de)
       │              │       │        │             │
       └──────────────┴───────┴────┬───┴─────────────┘
                                   ▼
                             Application   (casos de uso + abstrações)
                                   ▼
                                Domain     (modelos, enums, regras de negócio puras)
                                   ▼
                                Shared     (utilitários sem dependências: paths, redação, formatação)
```

| Projeto | TFM | Responsabilidade |
| --- | --- | --- |
| `Shared` | net10.0 | `AppPaths`, `SensitiveDataRedactor`, `ByteSize` |
| `Domain` | net10.0 | `Finding`, `Evidence`, `Metric`, `Observation`, `Confidence`, `DiagnosticResult`, `ScanReport`, `HealthScore`, enums, modelos de reparo |
| `Application` | net10.0 | `IDiagnosticScanner`, `ScanOrchestrator`, `RuleEngine`, `RiskAnalyzer`, `ScoringEngine`, `IRepairAction`/`IRepairEngine`, `ICommandExecutor`, repositórios (interfaces) |
| `Infrastructure` | net10.0-windows | `CommandExecutor`, `ExecutablePolicy`, `JobObject`, SQLite (migrations + repositórios), `FileBackupStore`, logging Serilog, `PrivilegeService` |
| `Diagnostics` | net10.0-windows | Scanners concretos e suas regras (a partir do Passo 2) |
| `Repair` | net10.0 | `RepairEngine` e, a partir do MVP 3, ações concretas |
| `Monitoring` | net10.0-windows | `LiveMetricsProvider` (amostragem leve do dashboard) |
| `Reporting` | net10.0 | Exportadores (JSON hoje; HTML/CSV/PDF no MVP 5) |
| `App` | net10.0-windows | WPF, ViewModels (CommunityToolkit.Mvvm), temas, `HealthCenterHost` |

Regra: dependências apontam **para dentro**. `Domain` e `Application` não conhecem WPF, SQLite, Serilog nem Win32.

## Fluxo de uma varredura

```text
ScanViewModel ──► IScanOrchestrator.RunAsync(plan, progress, ct)
                     │
                     ├─ seleciona scanners pelo ScanPlan (rápida / completa / personalizada)
                     ├─ executa com SemaphoreSlim(MaxConcurrency) + timeout por scanner
                     │      scanner: COLETA Metric/Observation (nunca decide, nunca altera)
                     ├─ IRuleEngine.Evaluate  ──► Findings (com evidência obrigatória)
                     ├─ IScoringEngine.Calculate ──► HealthScore (null se não há dados)
                     └─ IScanRepository.SaveAsync (SQLite)  ──► ScanReport
```

## Fluxo de uma correção (MVP 3)

```text
Finding ─► Recommendation ─► IRepairEngine.ExecuteAsync(request)
             RiskAnalyzer ─► Dry Run? ─► aprovação suficiente? ─► elevação?
             medição "antes" ─► Backup (salvo ANTES) ─► Execute ─► Validate ─► medição "depois"
             ─► RepairRecord no histórico ─► Rollback disponível
```

## Composição (DI)

`App/HealthCenterHost.cs` monta o Generic Host: configuração (`appsettings.json`, variáveis de ambiente,
argumentos), logging (Serilog como provider do `Microsoft.Extensions.Logging`) e os módulos via
`AddHealthCenterApplication/Infrastructure/Diagnostics/Repair/Monitoring/Reporting`. `ValidateOnBuild`
e `ValidateScopes` estão ligados — erro de registro falha na inicialização, não em produção.

Os System Tests sobem exatamente esse host com um diretório de dados temporário.

## Arquitetura de privilégios (planejada — Passo 5)

```text
LeccorWindowsHealthCenter.exe  (asInvoker, sem admin)
          │  IPC autenticado e limitado (named pipe com ACL + token por sessão + allowlist de operações)
          ▼
LeccorWindowsHealthCenter.Elevated.exe  (iniciado via UAC sob demanda; encerra ao terminar)
```

Plugins (`IDiagnosticPlugin`, futuro) nunca recebem privilégios administrativos automaticamente.

## Preparado para o futuro

- **Plugins**: scanners e regras são descobertos por DI (`IEnumerable<IDiagnosticScanner>`, `IEnumerable<IRule>`).
- **Portable**: `portable.flag` ao lado do `.exe` faz os dados irem para `.\Data`.
- **ARM64**: `RuntimeIdentifiers` já inclui `win-arm64`; publicação ativada quando houver validação em hardware.
