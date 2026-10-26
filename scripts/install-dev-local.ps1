<#
.SYNOPSIS
    Instala (copia) a build DEV do PX Tool Box diretamente na pasta de Add-ons do CorelDRAW,
    para iteração rápida durante o desenvolvimento — sem gerar o instalador .exe.

.DESCRIPTION
    Isto NÃO substitui o instalador oficial (installer\PXToolBoxDev.iss, gerado por
    build-release.ps1). É um atalho local: compila (ou reaproveita) o projeto
    PXToolBox.CorelHost.Dev e copia a saída para
        <CorelInstallDir>\Programs64\Addons\PXToolBoxDev\
    Restrito a essa pasta — nunca toca em outra pasta de Add-ons (SISBolt, MoldMotor, etc.).

.PARAMETER Configuration
    Debug ou Release. Padrão: Debug (uso local de desenvolvimento).

.PARAMETER CorelInstallDir
    Pasta de instalação do CorelDRAW (a que contém Programs64). Padrão: a instalação do
    CorelDRAW 2026 (v27) detectada nesta máquina.

.PARAMETER SkipBuild
    Pula a compilação e copia o que já estiver em bin\<Configuration>\net48.
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$CorelInstallDir = "C:\Program Files\Corel\CorelDRAW Graphics Suite\27",

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$hostProject = Join-Path $repoRoot "host\PXToolBox.CorelHost.Dev\PXToolBox.CorelHost.Dev.csproj"
$addonsRoot = Join-Path $CorelInstallDir "Programs64\Addons"
$targetDir = Join-Path $addonsRoot "PXToolBoxDev"

if (-not (Test-Path -LiteralPath $hostProject)) {
    throw "Projeto do host não encontrado: $hostProject"
}

if (-not (Test-Path -LiteralPath $addonsRoot)) {
    throw "Pasta de Add-ons do CorelDRAW não encontrada em '$addonsRoot'. Verifique -CorelInstallDir ou se o CorelDRAW 2026 (v27) está instalado."
}

# Nunca aceitar um alvo fora de .../Programs64/Addons/PXToolBoxDev — proteção contra engano de parâmetro.
$fullTarget = [System.IO.Path]::GetFullPath($targetDir)
$fullAddonsRoot = [System.IO.Path]::GetFullPath($addonsRoot).TrimEnd('\') + '\'
if (-not $fullTarget.StartsWith($fullAddonsRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    ($fullTarget.TrimEnd('\') -eq $fullAddonsRoot.TrimEnd('\'))) {
    throw "Caminho de destino inesperado: $fullTarget"
}

$isAdmin = ([Security.Principal.WindowsIdentity]::GetCurrent().Groups -contains 'S-1-5-32-544')
if (-not $isAdmin) {
    Write-Warning "Este script provavelmente precisa de privilégios de administrador para gravar em '$addonsRoot'. Se falhar, reabra o PowerShell como Administrador."
}

Write-Host "CorelDRAW precisa estar FECHADO para que os arquivos do add-on não estejam em uso." -ForegroundColor Yellow

if (-not $SkipBuild) {
    Write-Step "Compilando PXToolBox.CorelHost.Dev ($Configuration)"
    dotnet build $hostProject -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Build falhou." }
}

$publishDir = Join-Path (Split-Path -Parent $hostProject) "bin\$Configuration\net48"
if (-not (Test-Path -LiteralPath $publishDir)) {
    throw "Saída de build não encontrada em '$publishDir'. Rode sem -SkipBuild."
}

Write-Step "Copiando para $targetDir"
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $targetDir -Recurse -Force

Write-Host ""
Write-Host "Instalado em: $targetDir" -ForegroundColor Green
Write-Host "Abra (ou reinicie) o CorelDRAW 2026 para carregar/atualizar o add-on DEV." -ForegroundColor Green
Write-Host "Se o docker 'PX Tool Box DEV' não aparecer, veja docs/mvp-smoke-test.md." -ForegroundColor Green
