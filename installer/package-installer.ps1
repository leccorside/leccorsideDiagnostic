<#
.SYNOPSIS
    Script de empacotamento, instalação e desinstalação segura do Leccor Windows Health Center (Seção 62 do PROMPT).
.DESCRIPTION
    Realiza o publish self-contained da aplicação para win-x64 ou win-arm64 e gera
    a estrutura pronta para instalação ou modo portable.
.PARAMETER Action
    Ação a ser executada: Build, Install, Uninstall, Repair.
.PARAMETER Runtime
    Arquitetura de destino: win-x64 (padrão) ou win-arm64.
.PARAMETER InstallDir
    Diretório de instalação de destino. Padrão: $env:ProgramFiles\Leccor Windows Health Center.
.PARAMETER PurgeUserData
    Na desinstalação, remove também os dados de relatórios e backups em %ProgramData% (exige confirmação explícita).
#>
param(
    [ValidateSet('Build', 'Install', 'Uninstall', 'Repair')]
    [string]$Action = 'Build',
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',
    [string]$InstallDir = "$env:ProgramFiles\Leccor Windows Health Center",
    [switch]$PurgeUserData
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "publish\$Runtime"
$appProject = Join-Path $root "src\LeccorWindowsHealthCenter.App\LeccorWindowsHealthCenter.App.csproj"
$elevatedProject = Join-Path $root "src\LeccorWindowsHealthCenter.Elevated\LeccorWindowsHealthCenter.Elevated.csproj"
$dataDir = "$env:ProgramData\LeccorWindowsHealthCenter"

function Build-Publish {
    Write-Host "[1/3] Publicando aplicativo principal ($Runtime)..." -ForegroundColor Cyan
    dotnet publish $appProject -c Release -r $Runtime --self-contained true -p:PublishReadyToRun=true -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "Falha no publish do app" }

    Write-Host "[2/3] Publicando processo elevado ($Runtime)..." -ForegroundColor Cyan
    dotnet publish $elevatedProject -c Release -r $Runtime --self-contained true -p:PublishReadyToRun=true -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "Falha no publish do processo elevado" }

    Write-Host "[3/3] Publicação concluída com sucesso em: $publishDir" -ForegroundColor Green
}

function Install-App {
    Build-Publish

    Write-Host "Instalando em $InstallDir..." -ForegroundColor Cyan
    if (!(Test-Path $InstallDir)) {
        New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    }

    Copy-Item -Path "$publishDir\*" -Destination $InstallDir -Recurse -Force

    # Criação do atalho no Menu Iniciar
    $startMenuDir = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Leccor Software"
    if (!(Test-Path $startMenuDir)) {
        New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
    }

    $wshShell = New-Object -ComObject WScript.Shell
    $shortcut = $wshShell.CreateShortcut("$startMenuDir\Leccor Windows Health Center.lnk")
    $shortcut.TargetPath = "$InstallDir\LeccorWindowsHealthCenter.exe"
    $shortcut.WorkingDirectory = $InstallDir
    $shortcut.Description = "Diagnóstico, reparo e monitoramento de saúde do Windows"
    $shortcut.Save()

    Write-Host "Instalação concluída com sucesso!" -ForegroundColor Green
    Write-Host "Executável: $InstallDir\LeccorWindowsHealthCenter.exe"
}

function Uninstall-App {
    Write-Host "Iniciando desinstalação..." -ForegroundColor Cyan

    # Remove atalho no Menu Iniciar
    $startMenuShortcut = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Leccor Software\Leccor Windows Health Center.lnk"
    if (Test-Path $startMenuShortcut) {
        Remove-Item $startMenuShortcut -Force
    }

    # Remove arquivos do aplicativo
    if (Test-Path $InstallDir) {
        Remove-Item $InstallDir -Recurse -Force
        Write-Host "Arquivos do aplicativo removidos de $InstallDir" -ForegroundColor Yellow
    }

    # Preservação obrigatória de dados do usuário (Seção 62 do PROMPT)
    if ($PurgeUserData) {
        if (Test-Path $dataDir) {
            Remove-Item $dataDir -Recurse -Force
            Write-Host "Dados do usuário em $dataDir removidos conforme solicitação." -ForegroundColor Red
        }
    } else {
        Write-Host "Relatórios e backups preservados em $dataDir (Use -PurgeUserData para remover)." -ForegroundColor Green
    }

    Write-Host "Desinstalação concluída com sucesso." -ForegroundColor Green
}

switch ($Action) {
    'Build' { Build-Publish }
    'Install' { Install-App }
    'Uninstall' { Uninstall-App }
    'Repair' {
        Write-Host "Reparando instalação existente..." -ForegroundColor Cyan
        Install-App
    }
}

