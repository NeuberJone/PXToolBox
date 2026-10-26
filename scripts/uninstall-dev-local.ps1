<#
.SYNOPSIS
    Remove a instalação local (não-instalador) do add-on DEV do PX Tool Box.

.DESCRIPTION
    Restrito estritamente a
        <CorelInstallDir>\Programs64\Addons\PXToolBoxDev\
    Nunca remove outra pasta de Add-ons, nem a instalação de produção (que usaria a pasta
    "PXToolBox", nunca "PXToolBoxDev").
#>
param(
    [string]$CorelInstallDir = "C:\Program Files\Corel\CorelDRAW Graphics Suite\27"
)

$ErrorActionPreference = "Stop"

$addonsRoot = Join-Path $CorelInstallDir "Programs64\Addons"
$targetDir = Join-Path $addonsRoot "PXToolBoxDev"

$fullTarget = [System.IO.Path]::GetFullPath($targetDir)
$fullAddonsRoot = [System.IO.Path]::GetFullPath($addonsRoot).TrimEnd('\') + '\'
if (-not $fullTarget.StartsWith($fullAddonsRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    ($fullTarget.TrimEnd('\') -eq $fullAddonsRoot.TrimEnd('\')) -or
    ($fullTarget -notlike "*\PXToolBoxDev")) {
    throw "Caminho de destino inesperado, abortando por segurança: $fullTarget"
}

if (-not (Test-Path -LiteralPath $fullTarget)) {
    Write-Host "Nada para remover em '$fullTarget'."
    exit 0
}

Write-Host "CorelDRAW precisa estar FECHADO para remover estes arquivos." -ForegroundColor Yellow
Remove-Item -LiteralPath $fullTarget -Recurse -Force
Write-Host "Removido: $fullTarget" -ForegroundColor Green
