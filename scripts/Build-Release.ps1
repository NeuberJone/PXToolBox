<#
.SYNOPSIS
    Implementação real do Botão 1/3 do Stream Deck (Gerar/Refazer build DEV). Chamado pelo
    wrapper .\build-release.ps1 na raiz do repositório.

.DESCRIPTION
    - Valida versão e canal.
    - Restaura, compila e testa a solução.
    - Publica o host (PXToolBox.CorelHost.Dev) em Release.
    - Compila o instalador DEV com Inno Setup.
    - Gera checksums SHA-256 e um manifesto com versão/canal/commit de origem.
    - NUNCA publica nem cria releases — isso é responsabilidade de create-github-release.ps1 e
      publish-r2-release.ps1, chamados manualmente depois.

.PARAMETER Version
    Versão no formato X.Y.Z ou X.Y.Z-dev.N (ex.: 0.1.0-dev.1).

.PARAMETER Channel
    "Dev" (único implementado) ou "Stable" (reservado para o futuro — falha explicitamente).

.PARAMETER ReleaseBaseUrl
    Raiz pública do PRODUTO no R2 (ex.: https://assets.exemplo.com/pxtoolbox) — não inclui canal
    nem versão. Os scripts sempre completam como
    "<ReleaseBaseUrl>/<channel>/releases/<version>/<arquivo>" (ver docs/r2-and-github-releases.md).
    Opcional: sem ela, o manifesto é gerado sem URLs públicas.

.PARAMETER Force
    Permite refazer somente a pasta desta versão/canal (nunca outras versões).
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidateSet("Dev", "Stable", IgnoreCase = $true)]
    [string]$Channel,

    [string]$ReleaseBaseUrl,

    [switch]$Force
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Assert-UnderDirectory([string]$ChildPath, [string]$ParentPath) {
    $fullChild = [System.IO.Path]::GetFullPath($ChildPath)
    $fullParent = [System.IO.Path]::GetFullPath($ParentPath).TrimEnd('\') + '\'
    if (-not $fullChild.StartsWith($fullParent, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Caminho fora da pasta esperada: $fullChild"
    }
}

$channelNormalized = (Get-Culture).TextInfo.ToTitleCase($Channel.ToLowerInvariant())
$channelLower = $Channel.ToLowerInvariant()

if ($channelNormalized -eq "Stable") {
    Write-Error "O canal Stable ainda não tem empacotamento funcional nesta entrega. Nenhum pacote foi gerado — rótulo DEV não será usado para um pacote de produção incompleto. Use -Channel Dev."
    exit 1
}

# Coerência de versão x canal: versões Dev devem seguir X.Y.Z-dev.N.
if ($Version -notmatch '^\d+\.\d+\.\d+-dev\.\d+$') {
    Write-Error "Versão '$Version' não está no formato esperado para o canal Dev: X.Y.Z-dev.N (ex.: 0.1.0-dev.1)."
    exit 1
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$solution = Join-Path $repoRoot "PXToolBox.slnx"
$hostProject = Join-Path $repoRoot "host\PXToolBox.CorelHost.Dev\PXToolBox.CorelHost.Dev.csproj"
$testsProject = Join-Path $repoRoot "tests\PXToolBox.Tests\PXToolBox.Tests.csproj"
$installerScript = Join-Path $repoRoot "installer\PXToolBoxDev.iss"

$distRoot = Join-Path $repoRoot "dist\$channelLower"
$versionDist = Join-Path $distRoot $Version
Assert-UnderDirectory -ChildPath $versionDist -ParentPath $distRoot

New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

if (Test-Path -LiteralPath $versionDist) {
    $existingFiles = Get-ChildItem -LiteralPath $versionDist -Recurse -File -Force -ErrorAction SilentlyContinue
    if ($existingFiles.Count -gt 0 -and -not $Force) {
        throw "Já existe uma build para $channelNormalized $Version em '$versionDist'. Use -Force para refazer SÓ esta versão/canal."
    }
    if ($Force) {
        Write-Host "Recriando somente '$versionDist' (-Force)." -ForegroundColor Yellow
        Remove-Item -LiteralPath $versionDist -Recurse -Force
    }
}

$publishDir = Join-Path $versionDist "host-publish"
$installerDir = Join-Path $versionDist "installer"
New-Item -ItemType Directory -Path $publishDir, $installerDir -Force | Out-Null

Write-Step "Restaurando dependências"
dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw "dotnet restore falhou." }

Write-Step "Compilando (Debug)"
dotnet build $solution -c Debug --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build (Debug) falhou." }

Write-Step "Executando testes"
dotnet test $testsProject -c Debug --no-build
if ($LASTEXITCODE -ne 0) { throw "Testes falharam — build interrompida." }

Write-Step "Compilando (Release)"
dotnet build $solution -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build (Release) falhou." }

Write-Step "Publicando o host do CorelDRAW (Release)"
dotnet publish $hostProject -c Release -o $publishDir --no-self-contained
if ($LASTEXITCODE -ne 0) { throw "dotnet publish do host falhou." }

$candidateInno = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
    "C:\Program Files\Inno Setup 7\ISCC.exe"
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $candidateInno) {
    Write-Host "Inno Setup (ISCC.exe) não encontrado. Instale o Inno Setup 6/7 e rode manualmente:" -ForegroundColor Yellow
    Write-Host "  & `"C:\Program Files (x86)\Inno Setup 6\ISCC.exe`" `"$installerScript`" /DMyAppVersion=$Version /DMyPublishDir=`"$publishDir`" /O`"$installerDir`""
    throw "Instalador DEV obrigatório não foi gerado porque o Inno Setup não foi encontrado."
}

Write-Step "Compilando o instalador DEV"
& $candidateInno $installerScript "/DMyAppVersion=$Version" "/DMyPublishDir=$publishDir" "/O$installerDir"
if ($LASTEXITCODE -ne 0) { throw "Compilação do instalador (ISCC) falhou." }

$installerPath = Join-Path $installerDir "PXToolBox-DEV-Setup-$Version.exe"
if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Instalador esperado não foi encontrado após o ISCC: $installerPath"
}

Write-Step "Gerando checksums SHA-256"
$artifacts = @($installerPath)
$checksumLines = foreach ($artifact in $artifacts) {
    $hash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToUpperInvariant()
    "$hash $(Split-Path -Leaf $artifact)"
}
$checksumsPath = Join-Path $versionDist "SHA256SUMS.txt"
Set-Content -LiteralPath $checksumsPath -Value $checksumLines -Encoding ASCII

Write-Step "Registrando manifesto da build"
$commit = $null
try { $commit = & git rev-parse HEAD 2>$null } catch { $commit = $null }
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) { $commit = "sem-commit (ainda sem commits no repositorio)" }

$fileEntries = foreach ($artifact in $artifacts) {
    $name = Split-Path -Leaf $artifact
    [ordered]@{
        name   = $name
        sha256 = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToUpperInvariant()
        size   = (Get-Item -LiteralPath $artifact).Length
        url    = if ($ReleaseBaseUrl) { "$($ReleaseBaseUrl.TrimEnd('/'))/$channelLower/releases/$Version/$name" } else { $null }
    }
}

$manifest = [ordered]@{
    product   = "PXToolBox"
    channel   = $channelNormalized
    version   = $Version
    commit    = $commit
    builtAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    files     = $fileEntries
}

$manifestPath = Join-Path $versionDist "release-manifest.json"
($manifest | ConvertTo-Json -Depth 5) | Out-File -LiteralPath $manifestPath -Encoding utf8

Write-Host ""
Write-Host "Build DEV $Version gerada com sucesso." -ForegroundColor Green
Write-Host "Pasta: $versionDist"
Write-Host "Instalador: $installerPath"
Write-Host "Checksums: $checksumsPath"
Write-Host "Manifesto: $manifestPath"
Write-Host ""
Write-Host "Nada foi publicado. Use test-release.ps1 para validar os artefatos," -ForegroundColor Yellow
Write-Host "depois create-github-release.ps1 e publish-r2-release.ps1 quando estiver pronto." -ForegroundColor Yellow
exit 0
