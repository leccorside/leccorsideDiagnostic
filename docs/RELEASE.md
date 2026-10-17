# Release

Processo previsto (o instalador e a assinatura entram no Passo 10).

## Versionamento

SemVer em `Directory.Build.props` (`<Version>`). Enquanto estiver em `0.x`, cada passo concluído do
[PASSOS.md](../PASSOS.md) incrementa o minor (Passo 1 = `0.1.0`).

## Checklist

1. `./scripts/build.ps1 -Configuration Release` — 0 avisos, todos os testes verdes.
2. Atualizar `Version`, `CONTEXTO.md` (histórico) e notas de versão.
3. Smoke test manual: abrir o app sem admin, varredura completa, cancelar varredura, alternar tema,
   verificar logs em `%ProgramData%\LeccorWindowsHealthCenter\Logs`.
4. Medir consumo ocioso (meta: < 1% CPU, < 200 MB RAM).
5. Publicar `win-x64` (CI) e, quando suportado, `win-arm64`.
6. Assinar executável e instalador (Authenticode) — pendente.
7. Criar tag `vX.Y.Z` e release com os artefatos.

## Requisitos do instalador (seção 62)

Instalar, atualizar, reparar e desinstalar. A desinstalação **não** remove relatórios e backups do usuário
sem confirmação.
