# Build

## Local

```powershell
# restore + build (avisos como erro) + testes — igual ao CI
./scripts/build.ps1

# Release + publicação self-contained win-x64 em ./publish/win-x64
./scripts/build.ps1 -Configuration Release -Publish
```

Comandos equivalentes:

```powershell
dotnet restore LeccorWindowsHealthCenter.sln
dotnet build   LeccorWindowsHealthCenter.sln -c Release -warnaserror
dotnet test    LeccorWindowsHealthCenter.sln -c Release --no-build
dotnet publish src/LeccorWindowsHealthCenter.App -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o publish/win-x64
```

Executável gerado: `LeccorWindowsHealthCenter.exe` (`AssemblyName` do projeto App).

## Modo portable

Criar um arquivo vazio `portable.flag` ao lado do `.exe`: banco, logs e backups vão para `.\Data`.

## CI (GitHub Actions)

`.github/workflows/ci.yml`:

| Job | Etapas |
| --- | --- |
| `build-test` | restore → build Release com analisadores e `-warnaserror` → testes com cobertura (artefato `test-results`) |
| `publish` | publish self-contained `win-x64` (ReadyToRun) → artefato `LeccorWindowsHealthCenter-win-x64` |

`win-arm64` está preparado (`RuntimeIdentifiers`) e entra na matriz quando houver validação em hardware ARM.
