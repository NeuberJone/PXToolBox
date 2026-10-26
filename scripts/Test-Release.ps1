<#
.SYNOPSIS
    Implementação real do Botão 4 (Validar release DEV) do Stream Deck.

.DESCRIPTION
    Valida os artefatos já gerados por build-release.ps1 para a versão/canal informados:
    presença dos arquivos obrigatórios, integridade dos checksums, coerência do manifesto e
    ausência de credenciais/arquivos indevidos.

    IMPORTANTE: isto é validação de PACOTE, não um smoke test real dentro do CorelDRAW. Não
    comprova que o add-on carrega ou funciona no Corel — ver docs/mvp-smoke-test.md para o
    roteiro manual real.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidateSet("Dev", "Stable", IgnoreCase = $true)]
    [string]$Channel
)

$ErrorActionPreference = "Stop"
$channelLower = $Channel.ToLowerInvariant()
$channelNormalized = (Get-Culture).TextInfo.ToTitleCase($channelLower)

$repoRoot = Split-Path -Parent $PSScriptRoot
$versionDist = Join-Path $repoRoot "dist\$channelLower\$Version"

$failures = [System.Collections.Generic.List[string]]::new()

function Test-Required([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        $failures.Add($Message)
        Write-Host "FALHA: $Message" -ForegroundColor Red
    }
    else {
        Write-Host "OK: $Message" -ForegroundColor Green
    }
}

Write-Host "Validando $channelNormalized $Version em '$versionDist'" -ForegroundColor Cyan

Test-Required (Test-Path -LiteralPath $versionDist) "Pasta da versão existe"
if (-not (Test-Path -LiteralPath $versionDist)) {
    Write-Error "Pasta da versão não existe — rode build-release.ps1 primeiro."
    exit 1
}

$installerPath = Join-Path $versionDist "installer\PXToolBox-DEV-Setup-$Version.exe"
Test-Required (Test-Path -LiteralPath $installerPath) "Instalador presente: PXToolBox-DEV-Setup-$Version.exe"

$checksumsPath = Join-Path $versionDist "SHA256SUMS.txt"
Test-Required (Test-Path -LiteralPath $checksumsPath) "SHA256SUMS.txt presente"

$manifestPath = Join-Path $versionDist "release-manifest.json"
Test-Required (Test-Path -LiteralPath $manifestPath) "release-manifest.json presente"

if (Test-Path -LiteralPath $checksumsPath) {
    foreach ($line in Get-Content -LiteralPath $checksumsPath) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split '\s+', 2
        if ($parts.Count -ne 2) {
            Test-Required $false "Linha de checksum mal formada: '$line'"
            continue
        }
        $expectedHash = $parts[0]
        $fileName = $parts[1]
        $candidatePaths = @(
            (Join-Path $versionDist $fileName),
            (Join-Path (Join-Path $versionDist "installer") $fileName)
        )
        $actualPath = $candidatePaths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (-not $actualPath) {
            Test-Required $false "Arquivo listado no checksum não encontrado: $fileName"
            continue
        }
        $actualHash = (Get-FileHash -LiteralPath $actualPath -Algorithm SHA256).Hash.ToUpperInvariant()
        Test-Required ($actualHash -eq $expectedHash.ToUpperInvariant()) "Checksum coincide: $fileName"
    }
}

if (Test-Path -LiteralPath $manifestPath) {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    Test-Required ($manifest.version -eq $Version) "Manifesto: versão coincide ($($manifest.version))"
    Test-Required ($manifest.channel -eq $channelNormalized) "Manifesto: canal coincide ($($manifest.channel))"
    Test-Required (-not [string]::IsNullOrWhiteSpace($manifest.commit)) "Manifesto: commit de origem registrado"
}

Write-Host ""
Write-Host "Verificando ausência de credenciais/arquivos indevidos" -ForegroundColor Cyan
$suspiciousPatterns = @('password', 'secret', 'apikey', 'api_key', 'access_key', 'accesskey', 'r2_secret')
$textFiles = Get-ChildItem -Path (Join-Path $versionDist "*") -Recurse -File -Include "*.json", "*.txt", "*.config", "*.xml"
foreach ($file in $textFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($null -eq $content) { continue }
    foreach ($pattern in $suspiciousPatterns) {
        if ($content -match $pattern) {
            Test-Required $false "Possível credencial/termo sensível ('$pattern') em $($file.FullName)"
        }
    }
}
if ($failures.Count -eq 0) {
    Write-Host "Nenhum termo suspeito encontrado nos arquivos de texto do pacote." -ForegroundColor Green
}

Write-Host ""
if ($failures.Count -gt 0) {
    Write-Host "VALIDAÇÃO FALHOU ($($failures.Count) problema(s))." -ForegroundColor Red
    exit 1
}

Write-Host "Validação de pacote concluída com sucesso." -ForegroundColor Green
Write-Host "Lembrete: isto não substitui o smoke test real dentro do CorelDRAW (docs/mvp-smoke-test.md)." -ForegroundColor Yellow
exit 0
