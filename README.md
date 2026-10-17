<p align="center">
  <img src="logo1.jpg" alt="Leccor Windows Health Center" width="220" />
</p>

# Leccor Windows Health Center

Ferramenta desktop para Windows 10/11 (64-bit) que faz uma **auditoria técnica do computador** — hardware,
Windows, armazenamento, processos, drivers, rede e performance — e permite corrigir problemas de forma segura.

> **MEDIR → DETECTAR → COMPROVAR → EXPLICAR → CORRIGIR → VALIDAR → DESFAZER SE NECESSÁRIO**

Não é um "PC Cleaner". Cada problema exibido responde:

| Pergunta | Campo |
| --- | --- |
| O que aconteceu? | `Finding.Description` |
| Como sabemos disso? | `Finding.Evidence` (origem técnica de cada medição) |
| Qual o impacto? | `Finding.Impact` |
| Como corrigir? | `Finding.Recommendation` |
| Qual o risco da correção? | `Finding.RepairRisk` (SAFE · LOW · MEDIUM · HIGH · CRITICAL) |
| Posso desfazer? | `Finding.SupportsRollback` |

## Estado atual

**Passo 1 de 10 concluído** — fundação da arquitetura: orquestrador de varredura, motor de regras, análise
de risco, score, motor de reparo com backup/rollback, histórico SQLite, logs estruturados e interface WPF.
Os scanners de diagnóstico entram a partir do Passo 2. Veja [PASSOS.md](PASSOS.md) e [CONTEXTO.md](CONTEXTO.md).

## Executar

Requisitos: Windows 10/11 x64 e .NET SDK 10.

```powershell
dotnet run --project src/LeccorWindowsHealthCenter.App
```

Build + testes (o mesmo que o CI executa):

```powershell
./scripts/build.ps1
```

## Princípios

- **Local por padrão**: nenhum dado sai do computador sem consentimento explícito.
- **Sem administrador permanente**: o app roda como usuário comum; só operações específicas pedem UAC.
- **Sem inventar problemas**: sem medição não há conclusão nem nota — o score fica "sem dados".
- **Scanner ≠ Repair**: encontrar um problema nunca dispara uma correção automaticamente.
- **Backup antes de alterar**, validação depois e rollback quando aplicável.

## Documentação

| Documento | Conteúdo |
| --- | --- |
| [DOCUMENTACAO.md](DOCUMENTACAO.md) | Documentação técnica central: componentes, configuração, banco, como estender |
| [CONTEXTO.md](CONTEXTO.md) | Estado vivo do projeto, decisões, erros corrigidos, pendências |
| [PASSOS.md](PASSOS.md) | Plano de execução em passos |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Arquitetura e camadas |
| [docs/SECURITY.md](docs/SECURITY.md) | Modelo de segurança e privacidade |
| [docs/DIAGNOSTICS.md](docs/DIAGNOSTICS.md) | Scanners e o framework de diagnóstico |
| [docs/RULE_ENGINE.md](docs/RULE_ENGINE.md) | Regras, thresholds e falsos positivos |
| [docs/REPAIR_ENGINE.md](docs/REPAIR_ENGINE.md) | Reparos, risco, backup, rollback e Dry Run |
| [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) | Guia de desenvolvimento |
| [docs/BUILD.md](docs/BUILD.md) | Build, testes e publicação |
| [docs/RELEASE.md](docs/RELEASE.md) | Processo de release |
