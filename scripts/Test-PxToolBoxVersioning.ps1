<#
.SYNOPSIS
    Testa PxToolBoxVersioning.psm1 (comparação/descoberta de versões DEV/Stable) usando só
    diretórios temporários — nunca os builds reais em dist\. Chame sem parâmetros.

.DESCRIPTION
    Cobre os cenários pedidos: ordenação numérica (dev.9/10/28/100), nomes inválidos, pasta de
    build incompleta, manifesto divergente, separação Dev/Stable, tentativa de sobrescrita sem
    -Force, e escopo de -Force. Sai com código 0 se tudo passar, 1 se algo falhar (lista todas
    as falhas antes de sair, nunca para na primeira).
#>
$ErrorActionPreference = "Stop"
Import-Module (Join-Path $PSScriptRoot "PxToolBoxVersioning.psm1") -Force

$failures = [System.Collections.Generic.List[string]]::new()
$passCount = 0

function Assert-True([bool]$Condition, [string]$Message) {
    if ($Condition) {
        $script:passCount++
    } else {
        $script:failures.Add($Message)
        Write-Host "FALHA: $Message" -ForegroundColor Red
    }
}

function New-TempRepoRoot {
    $path = Join-Path ([System.IO.Path]::GetTempPath()) ("pxtoolbox-vertest-" + [System.Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    return $path
}

function New-FakeVersionFolder {
    param(
        [string]$RepoRoot,
        [string]$ChannelLower,
        [string]$ChannelNormalized,
        [string]$Version,
        [switch]$NoManifest,
        [string]$ManifestVersionOverride,
        [string]$ManifestChannelOverride,
        [string]$ArtifactNameOverride,
        [switch]$NoArtifactFile
    )

    $versionDist = Join-Path $RepoRoot "dist\$ChannelLower\$Version"
    New-Item -ItemType Directory -Path (Join-Path $versionDist "installer") -Force | Out-Null

    if ($NoManifest) {
        return $versionDist
    }

    $artifactName = if ($ArtifactNameOverride) { $ArtifactNameOverride } else { "PXToolBox-DEV-Setup-$Version.exe" }
    if (-not $NoArtifactFile) {
        Set-Content -LiteralPath (Join-Path $versionDist "installer\$artifactName") -Value "fake" -Encoding ASCII
    }

    $manifest = [ordered]@{
        product = "PXToolBox"
        channel = if ($ManifestChannelOverride) { $ManifestChannelOverride } else { $ChannelNormalized }
        version = if ($ManifestVersionOverride) { $ManifestVersionOverride } else { $Version }
        commit  = "deadbeef"
        files   = @(@{ name = $artifactName; sha256 = "AA"; size = 4; url = $null })
    }
    ($manifest | ConvertTo-Json -Depth 5) | Out-File -LiteralPath (Join-Path $versionDist "release-manifest.json") -Encoding utf8

    return $versionDist
}

# ---------------------------------------------------------------------------
# 1. Ordenação numérica — dev.9 < dev.10 < dev.27 < dev.28 < dev.100.
# ---------------------------------------------------------------------------
Write-Host "--- Ordenação numérica ---" -ForegroundColor Cyan
$v9 = ConvertFrom-PxVersionString "0.1.0-dev.9"
$v10 = ConvertFrom-PxVersionString "0.1.0-dev.10"
$v27 = ConvertFrom-PxVersionString "0.1.0-dev.27"
$v28 = ConvertFrom-PxVersionString "0.1.0-dev.28"
$v100 = ConvertFrom-PxVersionString "0.1.0-dev.100"

Assert-True ((Compare-PxVersion -A $v9 -B $v10) -lt 0) "dev.9 deve ser considerado MENOR que dev.10 (nunca maior por comparação textual)."
Assert-True ((Compare-PxVersion -A $v10 -B $v27) -lt 0) "dev.10 deve ser menor que dev.27."
Assert-True ((Compare-PxVersion -A $v27 -B $v28) -lt 0) "dev.27 deve ser menor que dev.28."
Assert-True ((Compare-PxVersion -A $v28 -B $v100) -lt 0) "dev.28 deve ser menor que dev.100."
Assert-True ((Compare-PxVersion -A $v9 -B $v28) -lt 0) "dev.9 deve ser menor que dev.28 — o incidente relatado exatamente ao contrário."
Assert-True ((Compare-PxVersion -A $v100 -B $v9) -gt 0) "dev.100 deve ser maior que dev.9 (nunca 'menor' por começar com dígito menor)."

$stable = ConvertFrom-PxVersionString "0.1.0"
Assert-True ((Compare-PxVersion -A $v100 -B $stable) -lt 0) "Um Stable X.Y.Z deve superar qualquer dev.N do mesmo X.Y.Z."

# ---------------------------------------------------------------------------
# 2. Nomes inválidos nunca lançam — só retornam $null.
# ---------------------------------------------------------------------------
Write-Host "--- Nomes inválidos ---" -ForegroundColor Cyan
foreach ($invalid in @("v0.1.0-dev.1", "0.1-dev.1", "0.1.0.dev.1", "0.1.0-beta.1", "latest", "", "0.1.0-dev.", "0.1.0-dev.1 ")) {
    Assert-True ($null -eq (ConvertFrom-PxVersionString $invalid)) "Formato inválido deveria retornar `$null, não lançar nem aceitar: '$invalid'"
}

# ---------------------------------------------------------------------------
# 3. Descoberta num repositório fake — mistura válidos, inválidos, incompletos, divergentes.
# ---------------------------------------------------------------------------
Write-Host "--- Get-PxToolBoxDistVersions / Resolve-PxToolBoxLatestVersion ---" -ForegroundColor Cyan
$repo = New-TempRepoRoot
try {
    New-FakeVersionFolder -RepoRoot $repo -ChannelLower "dev" -ChannelNormalized "Dev" -Version "0.1.0-dev.9" | Out-Null
    New-FakeVersionFolder -RepoRoot $repo -ChannelLower "dev" -ChannelNormalized "Dev" -Version "0.1.0-dev.10" | Out-Null
    New-FakeVersionFolder -RepoRoot $repo -ChannelLower "dev" -ChannelNormalized "Dev" -Version "0.1.0-dev.28" | Out-Null
    New-FakeVersionFolder -RepoRoot $repo -ChannelLower "dev" -ChannelNormalized "Dev" -Version "0.1.0-dev.100" | Out-Null

    # Nome de pasta inválido — deve ser ignorado da lista válida, mas relatado.
    New-Item -ItemType Directory -Path (Join-Path $repo "dist\dev\latest") -Force | Out-Null

    # Pasta de build incompleta (sem manifesto).
    New-FakeVersionFolder -RepoRoot $repo -ChannelLower "dev" -ChannelNormalized "Dev" -Version "0.1.0-dev.50" -NoManifest | Out-Null

    # Manifesto divergente: nome da pasta diz dev.60, manifesto diz dev.61.
    New-FakeVersionFolder -RepoRoot $repo -ChannelLower "dev" -ChannelNormalized "Dev" -Version "0.1.0-dev.60" -ManifestVersionOverride "0.1.0-dev.61" | Out-Null

    # Manifesto com canal errado.
    New-FakeVersionFolder -RepoRoot $repo -ChannelLower "dev" -ChannelNormalized "Dev" -Version "0.1.0-dev.70" -ManifestChannelOverride "Stable" | Out-Null

    # Artefato declarado no manifesto mas ausente em disco.
    New-FakeVersionFolder -RepoRoot $repo -ChannelLower "dev" -ChannelNormalized "Dev" -Version "0.1.0-dev.80" -NoArtifactFile | Out-Null

    $report = Get-PxToolBoxDistVersions -RepoRoot $repo -Channel "Dev"

    Assert-True ($report.Valid.Count -eq 4) "Esperava 4 versões válidas (dev.9/10/28/100), achou $($report.Valid.Count)."
    Assert-True ($report.Valid[0].Version.Original -eq "0.1.0-dev.9") "A primeira (menor) válida deveria ser dev.9, achou $($report.Valid[0].Version.Original)."
    Assert-True ($report.Valid[$report.Valid.Count - 1].Version.Original -eq "0.1.0-dev.100") "A última (maior) válida deveria ser dev.100, achou $($report.Valid[$report.Valid.Count - 1].Version.Original)."

    Assert-True ($report.InvalidFolderName.Count -eq 1) "Esperava 1 pasta com nome inválido ('latest'), achou $($report.InvalidFolderName.Count)."
    Assert-True ($report.IncompleteBuild.Count -eq 1) "Esperava 1 build incompleta (dev.50, sem manifesto), achou $($report.IncompleteBuild.Count)."
    Assert-True ($report.Divergent.Count -eq 3) "Esperava 3 divergências (dev.60 versão, dev.70 canal, dev.80 artefato ausente), achou $($report.Divergent.Count)."

    $resolved = Resolve-PxToolBoxLatestVersion -RepoRoot $repo -Channel "Dev"
    Assert-True ($resolved.Latest.Version.Original -eq "0.1.0-dev.100") "A versão mais recente resolvida deveria ser dev.100 (não dev.9 nem qualquer divergente/incompleta), achou $($resolved.Latest.Version.Original)."

    # ---------------------------------------------------------------------------
    # 4. Separação Dev/Stable — pastas de um canal nunca contam para o outro.
    # ---------------------------------------------------------------------------
    Write-Host "--- Separação Dev/Stable ---" -ForegroundColor Cyan
    New-FakeVersionFolder -RepoRoot $repo -ChannelLower "stable" -ChannelNormalized "Stable" -Version "9.9.9" | Out-Null
    $devReport = Get-PxToolBoxDistVersions -RepoRoot $repo -Channel "Dev"
    Assert-True ($devReport.Valid.Count -eq 4) "Criar uma versão em Stable não deveria afetar a contagem de válidas em Dev."
    $stableResolved = Resolve-PxToolBoxLatestVersion -RepoRoot $repo -Channel "Stable"
    Assert-True ($stableResolved.Latest.Version.Original -eq "9.9.9") "O canal Stable deveria resolver para a própria versão Stable, nunca para uma versão Dev."

    # ---------------------------------------------------------------------------
    # 5. Aviso de versão mais antiga — nunca bloqueia, só avisa.
    # ---------------------------------------------------------------------------
    Write-Host "--- Aviso de versão mais antiga (nunca bloqueia) ---" -ForegroundColor Cyan
    $warningForOld = Get-PxToolBoxOlderVersionWarning -RepoRoot $repo -Channel "Dev" -Version "0.1.0-dev.9"
    Assert-True ($null -ne $warningForOld) "Gerar dev.9 quando dev.100 já existe deveria produzir um aviso."
    Assert-True ($warningForOld -match "dev\.9" -and $warningForOld -match "dev\.100") "O aviso deveria citar tanto a versão pedida (dev.9) quanto a mais recente existente (dev.100)."

    $warningForNew = Get-PxToolBoxOlderVersionWarning -RepoRoot $repo -Channel "Dev" -Version "0.1.0-dev.101"
    Assert-True ($null -eq $warningForNew) "Gerar dev.101 (mais nova que a existente dev.100) nunca deveria produzir aviso."
} finally {
    Remove-Item -LiteralPath $repo -Recurse -Force -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------
# 6. Tentativa de sobrescrita sem -Force, e escopo de -Force (nunca toca outra versão).
# ---------------------------------------------------------------------------
Write-Host "--- Sobrescrita sem -Force / escopo de -Force ---" -ForegroundColor Cyan
$repo2 = New-TempRepoRoot
try {
    $v28Dist = New-FakeVersionFolder -RepoRoot $repo2 -ChannelLower "dev" -ChannelNormalized "Dev" -Version "0.1.0-dev.28"
    $v27Dist = New-FakeVersionFolder -RepoRoot $repo2 -ChannelLower "dev" -ChannelNormalized "Dev" -Version "0.1.0-dev.27"

    $threw = $false
    try {
        Assert-PxToolBoxVersionOutputWritable -VersionDist $v28Dist -ChannelNormalized "Dev" -Version "0.1.0-dev.28" | Out-Null
    } catch {
        $threw = $true
    }
    Assert-True $threw "Sem -Force, sobrescrever uma versão com arquivos existentes deveria lançar."
    Assert-True (Test-Path -LiteralPath $v28Dist) "A pasta não deveria ter sido removida quando a escrita foi recusada (sem -Force)."

    $wasRecreated = Assert-PxToolBoxVersionOutputWritable -VersionDist $v28Dist -ChannelNormalized "Dev" -Version "0.1.0-dev.28" -Force
    Assert-True $wasRecreated "Com -Force, a função deveria indicar que a pasta foi removida e precisa ser recriada."
    Assert-True (-not (Test-Path -LiteralPath $v28Dist)) "Com -Force, a pasta de dev.28 deveria ter sido removida."
    Assert-True (Test-Path -LiteralPath $v27Dist) "-Force na pasta de dev.28 NUNCA deveria remover a pasta de dev.27 (escopo restrito à versão pedida)."

    # Pasta vazia ou inexistente: sempre writable, nunca lança.
    $emptyDist = Join-Path $repo2 "dist\dev\0.1.0-dev.999"
    New-Item -ItemType Directory -Path $emptyDist -Force | Out-Null
    $result = Assert-PxToolBoxVersionOutputWritable -VersionDist $emptyDist -ChannelNormalized "Dev" -Version "0.1.0-dev.999"
    Assert-True ($result -eq $false) "Uma pasta vazia nunca deveria ser tratada como 'precisa remover' nem exigir -Force."
} finally {
    Remove-Item -LiteralPath $repo2 -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
if ($failures.Count -gt 0) {
    Write-Host "RESULTADO: $passCount passou(ram), $($failures.Count) falhou(aram)." -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "RESULTADO: todos os $passCount asserts passaram." -ForegroundColor Green
exit 0
