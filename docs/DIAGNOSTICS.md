# Diagnóstico — framework e scanners

## Contrato

```csharp
public interface IDiagnosticScanner
{
    string Id { get; }              // estável: "cpu", "eventlog"...
    string Name { get; }            // exibido na UI
    ScanModule Module { get; }      // usado na varredura personalizada e na cobertura do score
    bool IncludedInQuickScan { get; }
    int Order { get; }              // ordem da seção 73: Hardware → CPU → RAM → GPU → Discos → ...
    Task<DiagnosticResult> ScanAsync(CancellationToken cancellationToken);
}
```

Um scanner **apenas coleta**:

- `Metric` — amostra numérica (nome, valor, unidade, timestamp, tags). Várias amostras com o mesmo nome formam uma série temporal.
- `Observation` — fato não numérico (ex.: "grupo de eventos System/Disk/153 com 47 ocorrências"), com atributos e evidências.

Um scanner **nunca**: decide se algo é problema (isso é do [RuleEngine](RULE_ENGINE.md)), altera o sistema,
executa processos fora do `ICommandExecutor`, ou armazena conteúdo de arquivos pessoais.

## `DiagnosticScannerBase`

Implementar `CollectAsync(ScanCollector collector, CancellationToken ct)`. A base cuida de:

- medir duração e registrar log estruturado (`Scanner {ScannerId} concluído em {DurationMs} ms`);
- converter exceções em `ScannerRunStatus.Failed` com mensagem amigável (a varredura continua);
- propagar cancelamento.

## Orquestração (`ScanOrchestrator`)

| Garantia | Implementação |
|---|---|
| Concorrência limitada | `Scan:MaxConcurrency` (padrão 2) — a ferramenta não pode causar a lentidão que mede |
| Timeout por scanner | `Scan:ScannerTimeout` (padrão 5 min) → `Failed` com "Tempo limite excedido" |
| Cancelamento | botão "Cancelar varredura"; scanners em execução recebem o token; os pendentes viram `Cancelled`; o já coletado é preservado e salvo |
| Isolamento de falhas | exceção de um scanner não derruba os demais |
| UI responsiva | scanners rodam em `Task.Run`; progresso via `IProgress<ScanProgress>` (✓ → ○ ✗ ⊘) |

## Modos

| Modo | Scanners |
|---|---|
| Rápida | `IncludedInQuickScan == true` |
| Completa | todos |
| Personalizada | os de `ScanModule` selecionados |

## Catálogo de scanners

| Scanner | Id | Módulo | Rápida | Status |
|---|---|---|---|---|
| — | — | — | — | Nenhum scanner implementado no Passo 1. Os scanners do MVP 1 entram nos Passos 2–5 (ver [PASSOS.md](../PASSOS.md)). |

Cada scanner novo deve ser documentado aqui com: fonte de dados (WMI/contador/API/EventLog/comando),
métricas e observações produzidas (nome, unidade, tags), custo estimado (tempo/CPU), necessidade de
elevação e comportamento quando a fonte não está disponível ("não disponível", nunca valor inventado).

## Métricas do dashboard (Monitoring)

`LiveMetricsProvider` usa apenas `GetSystemTimes` (CPU por delta), `GlobalMemoryStatusEx` (RAM) e
`DriveInfo` (disco do sistema) a cada 2 s, somente com o Dashboard visível e a janela não minimizada.
A primeira leitura de CPU é `null` (sem delta). GPU fica "não disponível" até o scanner de GPU.
Medido no Passo 1: **~0,3% de CPU ociosa** e **~90–110 MB de memória privada**.
