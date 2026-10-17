# Segurança e privacidade

O Leccor Windows Health Center é tratado como **ferramenta privilegiada** (seção 63 do PROMPT).

## Princípios aplicados

| Princípio | Como está implementado |
|---|---|
| Menor privilégio | `app.manifest` com `requestedExecutionLevel=asInvoker`. O app nunca pede admin para abrir. `CommandExecutor` recusa comandos `RequiresElevation` se o processo não estiver elevado; `RepairEngine` bloqueia ações que exigem elevação. |
| Sem command injection | Todo processo externo passa por `ICommandExecutor`. Argumentos vão em `ProcessStartInfo.ArgumentList` (nunca concatenados), `UseShellExecute=false`. |
| Validação de executáveis | `ExecutablePolicy`: caminho absoluto e normalizado (sem `..`), apenas `.exe`, existente, dentro de `%windir%`, `System32`, `SysWOW64`, `Sysnative` ou da allowlist explícita (`CommandExecution:AdditionalTrustedExecutables`). |
| Interpretadores bloqueados | `cmd.exe`, `powershell.exe`, `pwsh.exe`, `wscript.exe`, `cscript.exe`, `mshta.exe`, `rundll32.exe`, `bash.exe` são recusados — eles reinterpretam argumentos e reabririam a porta para injeção. |
| Argumentos | Rejeição de `\0`, `\r`, `\n`. Metacaracteres (`&`, `|`) são inofensivos sem shell. |
| Processos órfãos | Cada processo filho entra em um Job Object com `KILL_ON_JOB_CLOSE`; cancelamento/timeout fazem `Kill(entireProcessTree: true)`. |
| Memória | stdout/stderr capturados com limite (`MaxOutputChars`), drenando o pipe para não travar o processo. |
| Segredos em logs | `SensitiveDataRedactor` + `RedactionEnricher` mascaram propriedades com nomes sensíveis (password, token, apikey, cookie, credential, authorization, connectionstring...) e padrões em texto (`chave=valor`, `Authorization: <esquema> <credencial>`, `Bearer`, tokens GitHub/Slack/AWS/`sk-`). O stdout/stderr de comandos também é redigido. |
| Backups protegidos | `FileBackupStore` aplica ACL protegida (sem herança) ao diretório: SYSTEM, Administradores e o usuário atual. Escrita atômica (`.tmp` + `Move`). |
| Banco local | SQLite em `%ProgramData%\LeccorWindowsHealthCenter\Data\health.db`. Não armazena conteúdo de arquivos pessoais. |
| Paths | `AppPaths` exige caminho absoluto; fallback para `%LocalAppData%` quando `%ProgramData%` não é gravável. |

## Privacidade (seção 41)

- Funciona **100% localmente**. Não há telemetria, chamadas de rede nem upload.
- Não são coletados conteúdos de documentos. Nomes de arquivos aparecem apenas localmente (ex.: análise de espaço).
- Qualquer recurso futuro que envie dados exigirá consentimento explícito e será documentado aqui.

## Riscos de correção

Ver [REPAIR_ENGINE.md](REPAIR_ENGINE.md). Resumo: SAFE pode ser automático; LOW/MEDIUM exigem confirmação;
HIGH exige confirmação explícita; CRITICAL nunca é executado pela ferramenta.

## O que a ferramenta nunca faz (seção 57)

RAM booster, limpeza indiscriminada de RAM/Prefetch, "registry cleaner", desativar serviços por lista genérica,
desabilitar Defender ou Windows Update, remover telemetria indiscriminadamente, desabilitar SysMain sem diagnóstico.

## Pendências de segurança

- Processo elevado separado com IPC autenticado (Passo 5).
- Assinatura de código (Authenticode) do executável e do instalador (Passo 10).
- Verificação de assinatura dos executáveis da allowlist adicional.

## Reportar vulnerabilidades

Abra um aviso privado de segurança no repositório (GitHub → Security → Report a vulnerability). Não publique
detalhes de exploração em issues públicas.
