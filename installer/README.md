# Instalador & Modo Portable — Leccor Windows Health Center

Implementação profissional do empacotamento, instalação, reparo e desinstalação conforme a **Seção 62** do PROMPT.

---

## 1. Modos de Uso

### Modo Instalado (Padrão)
- **Local de instalação:** `%ProgramFiles%\Leccor Windows Health Center`
- **Atalhos:** Menu Iniciar (`Leccor Software\Leccor Windows Health Center`)
- **Permissões:** O executável principal roda como usuário comum (`asInvoker`). Apenas operações privilegiadas usam o processo auxiliar mínimo `LeccorWindowsHealthCenter.Elevated.exe`.
- **Dados:** Persistidos em `%ProgramData%\LeccorWindowsHealthCenter` (SQLite, logs, relatórios e backups).

### Modo Portable (Seção 61)
- O aplicativo pode ser executado diretamente de qualquer pasta ou pendrive:
  - Crie um arquivo vazio chamado `portable.flag` ao lado de `LeccorWindowsHealthCenter.exe`.
  - Todos os dados locais (banco SQLite, configurações, relatórios e backups) passarão a ser gravados na subpasta `data/` relativa ao próprio executável.

---

## 2. Comandos de Empacotamento e Instalação

Utilize o script PowerShell `package-installer.ps1`:

### Publicar binários self-contained
```powershell
./installer/package-installer.ps1 -Action Build -Runtime win-x64
```
Para ARM64:
```powershell
./installer/package-installer.ps1 -Action Build -Runtime win-arm64
```

### Instalar
```powershell
./installer/package-installer.ps1 -Action Install
```

### Reparar instalação existente
```powershell
./installer/package-installer.ps1 -Action Repair
```

### Desinstalar preservando dados
```powershell
./installer/package-installer.ps1 -Action Uninstall
```
> [!IMPORTANT]
> A desinstalação padrão remove todos os binários do aplicativo, mas **preserva intactos** os relatórios técnicos e pontos de rollback em `%ProgramData%\LeccorWindowsHealthCenter`.

### Desinstalar removendo dados (Purge completo)
```powershell
./installer/package-installer.ps1 -Action Uninstall -PurgeUserData
```

---

## 3. Instalador Gráfico (Inno Setup)

O arquivo `installer.iss` está configurado para compilar o executável de setup `LeccorWindowsHealthCenter_Setup_x64.exe` com visual moderno, suporte a idioma Português do Brasil e lógica de preservação de dados do usuário.
