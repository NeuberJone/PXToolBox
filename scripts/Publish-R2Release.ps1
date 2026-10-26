<#
.SYNOPSIS
    Implementação real do Botão 7 (Publicar release DEV no R2) do Stream Deck.

.DESCRIPTION
    Convenção de destino (ver docs/r2-and-github-releases.md):
        pxtoolbox/<channel>/releases/<version>/<arquivo>
        pxtoolbox/<channel>/latest.json
    onde <channel> é "dev" ou "stable".

    Sem -Publish: só valida configuração/credenciais e artefatos, e mostra o plano. Nenhum
    upload é feito.
    Com -Publish: publica os arquivos versionados primeiro, confere o upload, e só então
    atualiza o latest.json do canal. Nunca sobrescreve uma versão já publicada em silêncio, e
    nunca remove versões anteriores.

    Configuração esperada via variáveis de ambiente (NUNCA cravadas no repositório):
        PXTOOLBOX_R2_ACCOUNT_ID
        PXTOOLBOX_R2_ACCESS_KEY_ID
        PXTOOLBOX_R2_SECRET_ACCESS_KEY
        PXTOOLBOX_R2_BUCKET
        PXTOOLBOX_R2_PUBLIC_BASE_URL   (ex.: https://assets.exemplo.com/pxtoolbox)

    Requer AWS CLI instalado (R2 é compatível com a API S3).
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidateSet("Dev", "Stable", IgnoreCase = $true)]
    [string]$Channel,

    [switch]$Publish
)

$ErrorActionPreference = "Stop"
$channelLower = $Channel.ToLowerInvariant()
$channelNormalized = (Get-Culture).TextInfo.ToTitleCase($channelLower)

if ($channelNormalized -eq "Stable") {
    Write-Error "Canal Stable ainda não tem empacotamento funcional — não há o que publicar."
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$versionDist = Join-Path $repoRoot "dist\$channelLower\$Version"
$installerPath = Join-Path $versionDist "installer\PXToolBox-DEV-Setup-$Version.exe"
$checksumsPath = Join-Path $versionDist "SHA256SUMS.txt"
$manifestPath = Join-Path $versionDist "release-manifest.json"

Write-Host "=== Plano: Publicação no R2 ($channelNormalized $Version) ===" -ForegroundColor Cyan

foreach ($required in @($installerPath, $checksumsPath, $manifestPath)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Artefato obrigatório ausente: $required. Rode build-release.ps1 e test-release.ps1 primeiro."
    }
}
Write-Host "Artefatos presentes: OK"

$requiredEnvVars = @(
    "PXTOOLBOX_R2_ACCOUNT_ID",
    "PXTOOLBOX_R2_ACCESS_KEY_ID",
    "PXTOOLBOX_R2_SECRET_ACCESS_KEY",
    "PXTOOLBOX_R2_BUCKET",
    "PXTOOLBOX_R2_PUBLIC_BASE_URL"
)
$missing = $requiredEnvVars | Where-Object { [string]::IsNullOrWhiteSpace([System.Environment]::GetEnvironmentVariable($_)) }
if ($missing.Count -gt 0) {
    Write-Host ""
    Write-Host "Infraestrutura R2 ainda não configurada. Defina estas variáveis de ambiente:" -ForegroundColor Yellow
    $missing | ForEach-Object { Write-Host "  - $_" }
    Write-Host ""
    Write-Host "Nenhum bucket/URL foi inventado — configure com os valores reais do seu R2." -ForegroundColor Yellow
    throw "Configuração do R2 incompleta."
}

$accountId = $env:PXTOOLBOX_R2_ACCOUNT_ID
$bucket = $env:PXTOOLBOX_R2_BUCKET
$publicBaseUrl = $env:PXTOOLBOX_R2_PUBLIC_BASE_URL.TrimEnd('/')
$endpoint = "https://$accountId.r2.cloudflarestorage.com"

$awsCmd = Get-Command aws -ErrorAction SilentlyContinue
if (-not $awsCmd) {
    throw "AWS CLI (aws) não encontrado no PATH. Instale: https://aws.amazon.com/cli/ (R2 é compatível com S3)."
}

$prefix = "pxtoolbox/$channelLower/releases/$Version"
$latestKey = "pxtoolbox/$channelLower/latest.json"

Write-Host "Bucket: $bucket"
Write-Host "Prefixo de destino: $prefix/"
Write-Host "Ponteiro do canal: $latestKey"
Write-Host "URL pública base: $publicBaseUrl"
Write-Host ""

$assets = @(
    @{ Path = $installerPath; Key = "$prefix/PXToolBox-DEV-Setup-$Version.exe" },
    @{ Path = $checksumsPath; Key = "$prefix/SHA256SUMS.txt" },
    @{ Path = $manifestPath; Key = "$prefix/release-manifest.json" }
)

Write-Host "Arquivos a publicar:"
$assets | ForEach-Object { Write-Host "  - $($_.Key)" }

if (-not $Publish) {
    Write-Host ""
    Write-Host "Modo plano (sem -Publish): nenhum upload foi feito." -ForegroundColor Yellow
    exit 0
}

function Test-R2ObjectExists([string]$Key) {
    & aws s3api head-object --endpoint-url $endpoint --bucket $bucket --key $Key *> $null
    return ($LASTEXITCODE -eq 0)
}

if (Test-R2ObjectExists -Key $assets[0].Key) {
    throw "Já existe um objeto publicado em '$($assets[0].Key)'. Este script nunca sobrescreve uma versão já publicada — use uma nova versão."
}

Write-Host ""
Write-Host "Publicando arquivos versionados..." -ForegroundColor Cyan
foreach ($asset in $assets) {
    & aws s3 cp $asset.Path "s3://$bucket/$($asset.Key)" --endpoint-url $endpoint
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao publicar '$($asset.Key)'. Publicação interrompida sem atualizar o latest.json."
    }
}

Write-Host "Conferindo upload..." -ForegroundColor Cyan
foreach ($asset in $assets) {
    if (-not (Test-R2ObjectExists -Key $asset.Key)) {
        throw "Verificação pós-upload falhou para '$($asset.Key)'. Publicação interrompida sem atualizar o latest.json."
    }
}

Write-Host "Atualizando ponteiro do canal ($latestKey)..." -ForegroundColor Cyan
$latest = [ordered]@{
    channel = $channelNormalized
    version = $Version
    installerUrl = "$publicBaseUrl/$($assets[0].Key)"
    checksumsUrl = "$publicBaseUrl/$($assets[1].Key)"
    manifestUrl  = "$publicBaseUrl/$($assets[2].Key)"
    publishedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
}
$latestTempFile = Join-Path $env:TEMP "pxtoolbox-latest-$channelLower.json"
($latest | ConvertTo-Json -Depth 4) | Out-File -LiteralPath $latestTempFile -Encoding utf8

& aws s3 cp $latestTempFile "s3://$bucket/$latestKey" --endpoint-url $endpoint
if ($LASTEXITCODE -ne 0) {
    throw "Arquivos versionados foram publicados, mas a atualização de '$latestKey' falhou. Publicação incompleta — não anuncie como concluída; corrija e rode de novo."
}

Write-Host ""
Write-Host "Publicação concluída: $channelNormalized $Version" -ForegroundColor Green
Write-Host "Instalador: $($latest.installerUrl)"
exit 0
