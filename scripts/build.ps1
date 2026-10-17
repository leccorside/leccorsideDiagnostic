<#
.SYNOPSIS
    Restore, build (com analisadores) e testes da solution — o mesmo que o CI executa.
.EXAMPLE
    ./scripts/build.ps1
    ./scripts/build.ps1 -Configuration Release -Publish
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Publish,
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'LeccorWindowsHealthCenter.sln'

dotnet restore $solution
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build $solution --no-restore -c $Configuration -warnaserror
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test $solution --no-build -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Publish) {
    $app = Join-Path $root 'src/LeccorWindowsHealthCenter.App/LeccorWindowsHealthCenter.App.csproj'
    $out = Join-Path $root "publish/$Runtime"
    dotnet publish $app -c $Configuration -r $Runtime --self-contained true -p:PublishReadyToRun=true -o $out
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "Publicado em $out"
}
