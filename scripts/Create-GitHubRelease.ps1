<#
.SYNOPSIS
    Implementação real do Botão 6 (Criar pré-release no GitHub) do Stream Deck.

.DESCRIPTION
    Sem -Create: só verifica pré-requisitos (gh CLI, autenticação, repositório, tag já
    publicada, artefatos válidos) e mostra o plano. Nenhuma mutação remota.
    Com -Create: cria a release no GitHub (sempre como pré-release para o canal Dev) e anexa
    os artefatos + checksums. Recusa sobrescrever uma release existente para a mesma tag.

    Requer GitHub CLI (gh) instalado e autenticado (gh auth login).
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidateSet("Dev", "Stable", IgnoreCase = $true)]
    [string]$Channel,

    [switch]$Create
)

$ErrorActionPreference = "Stop"
$channelLower = $Channel.ToLowerInvariant()
$channelNormalized = (Get-Culture).TextInfo.ToTitleCase($channelLower)

if ($channelNormalized -eq "Stable") {
    Write-Error "Canal Stable ainda não tem empacotamento funcional — não há o que publicar como release de produção."
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$tag = "dev-v$Version"
$versionDist = Join-Path $repoRoot "dist\$channelLower\$Version"
$installerPath = Join-Path $versionDist "installer\PXToolBox-DEV-Setup-$Version.exe"
$checksumsPath = Join-Path $versionDist "SHA256SUMS.txt"
$manifestPath = Join-Path $versionDist "release-manifest.json"

Write-Host "=== Plano: Release do GitHub ($channelNormalized $Version) ===" -ForegroundColor Cyan

$ghCmd = Get-Command gh -ErrorAction SilentlyContinue
if (-not $ghCmd) {
    throw "GitHub CLI (gh) não encontrado no PATH. Instale: https://cli.github.com/"
}

$authStatus = & gh auth status 2>&1
Write-Host "gh auth status:"
Write-Host $authStatus
if ($LASTEXITCODE -ne 0) {
    throw "GitHub CLI não está autenticado. Rode 'gh auth login' antes de continuar."
}

$repoInfo = & gh repo view --json nameWithOwner 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Não foi possível identificar o repositório GitHub a partir da pasta atual. $repoInfo"
}
$repoName = ($repoInfo | ConvertFrom-Json).nameWithOwner
Write-Host "Repositório: $repoName"

Write-Host "Tag exigida (precisa já existir e estar publicada): $tag"
$remoteTag = & git ls-remote --tags origin "refs/tags/$tag" 2>&1
if ([string]::IsNullOrWhiteSpace($remoteTag)) {
    throw "Tag '$tag' não encontrada no remoto 'origin'. Crie e publique a tag primeiro (Botão 5), depois rode este botão novamente."
}
Write-Host "Tag encontrada no remoto: OK"

foreach ($required in @($installerPath, $checksumsPath, $manifestPath)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Artefato obrigatório ausente: $required. Rode build-release.ps1 e test-release.ps1 primeiro."
    }
}
Write-Host "Artefatos presentes: OK"

$existingRelease = & gh release view $tag --json tagName 2>&1
$releaseAlreadyExists = ($LASTEXITCODE -eq 0)
if ($releaseAlreadyExists) {
    Write-Host "Já existe uma release para a tag '$tag' no GitHub." -ForegroundColor Yellow
}

$assets = @($installerPath, $checksumsPath, $manifestPath)
Write-Host ""
Write-Host "Assets a anexar:"
$assets | ForEach-Object { Write-Host "  - $_" }
Write-Host "Tipo: pré-release (canal Dev)"

if (-not $Create) {
    Write-Host ""
    Write-Host "Modo plano (sem -Create): nenhuma mutação remota foi feita." -ForegroundColor Yellow
    exit 0
}

if ($releaseAlreadyExists) {
    throw "Release para a tag '$tag' já existe — este script não sobrescreve releases existentes. Apague-a manualmente no GitHub se realmente quiser recriá-la."
}

Write-Host ""
Write-Host "Criando release (pré-release) no GitHub..." -ForegroundColor Cyan
& gh release create $tag @assets --repo $repoName --title "PX Tool Box DEV $Version" --notes "Build DEV automatizada. Canal: Dev." --prerelease
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao criar a release no GitHub."
}

Write-Host "Release criada: https://github.com/$repoName/releases/tag/$tag" -ForegroundColor Green
exit 0
