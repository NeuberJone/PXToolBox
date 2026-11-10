<#
.SYNOPSIS
    Comparação/descoberta de versões DEV/Stable do PX Tool Box, centralizada num único lugar.

.DESCRIPTION
    Motivo desta rodada: foi relatada uma execução que considerou "0.1.0-dev.9" mais recente do
    que "0.1.0-dev.28" já existente. Investigação (ver docs/ARCHITECTURE.md, seção "Versões DEV
    e proteções de empacotamento") não encontrou nenhum script deste repositório que
    descobrisse "a versão mais recente" por ordenação de texto/pasta/data — build-release.ps1,
    test-release.ps1, create-github-release.ps1 e publish-r2-release.ps1 sempre exigem -Version
    explícito, sem nenhuma seleção automática. O incidente relatado é consistente com uma
    decisão ad-hoc (comando de shell improvisado, não um bug de código já existente) — mas o
    risco é real: "0.1.0-dev.9" > "0.1.0-dev.28" como STRING pura (comparando caractere a
    caractere, '9' > '2'), então qualquer ordenação textual futura cometeria o mesmo erro. Este
    módulo existe para que nenhuma comparação de versão deste projeto precise reimplementar essa
    lógica (e arriscar o mesmo erro) de novo.

    Convenção de versão aceita: "X.Y.Z" (Stable) ou "X.Y.Z-dev.N" (Dev) — todos os componentes
    inteiros não-negativos. Qualquer outro formato é tratado como INVÁLIDO (nunca uma
    aproximação silenciosa) pelas funções abaixo.
#>

Set-StrictMode -Version Latest

<#
.SYNOPSIS
    Converte uma string de versão numa chave comparável, ou $null se o formato for inválido.
.DESCRIPTION
    Nunca lança para um formato inválido — quem chama decide o que fazer (ignorar com aviso,
    reportar inconsistência, etc.), conforme pedido ("ignore diretórios sem formato válido,
    informando inconsistências relevantes").
#>
function ConvertFrom-PxVersionString {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Version
    )

    # Stable: X.Y.Z — Dev: X.Y.Z-dev.N. Nenhum outro formato (espaços, prefixo "v", sufixos
    # diferentes de "-dev.N") é aceito — evita a ambiguidade de tentar "adivinhar" um formato
    # parecido.
    if ($Version -match '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(-dev\.(?<dev>\d+))?$') {
        $major = [int]$Matches['major']
        $minor = [int]$Matches['minor']
        $patch = [int]$Matches['patch']
        $isDev = $null -ne $Matches['dev'] -and $Matches['dev'] -ne ''
        $devNum = if ($isDev) { [int]$Matches['dev'] } else { -1 }

        return [PSCustomObject]@{
            Original = $Version
            Major    = $major
            Minor    = $minor
            Patch    = $patch
            IsDev    = $isDev
            DevNumber = $devNum
        }
    }

    return $null
}

<#
.SYNOPSIS
    Compara duas versões já convertidas por ConvertFrom-PxVersionString. Retorna -1/0/1.
.DESCRIPTION
    Ordenação NUMÉRICA de cada componente (nunca texto) — dev.9 < dev.10 < dev.27 < dev.28 <
    dev.100. Dentro do mesmo Major.Minor.Patch, uma versão Stable (sem "-dev.N") é sempre maior
    que qualquer pré-release Dev dela (convenção comum de semver: um release estável supera
    seus próprios pré-lançamentos).
#>
function Compare-PxVersion {
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$A,

        [Parameter(Mandatory = $true)]
        [PSCustomObject]$B
    )

    foreach ($part in @('Major', 'Minor', 'Patch')) {
        if ($A.$part -ne $B.$part) {
            return [Math]::Sign($A.$part - $B.$part)
        }
    }

    if ($A.IsDev -ne $B.IsDev) {
        # Stable (IsDev=$false) sempre vence um Dev do mesmo X.Y.Z.
        return $(if ($A.IsDev) { -1 } else { 1 })
    }

    if (-not $A.IsDev) {
        return 0 # Dois Stable com o mesmo X.Y.Z — nunca deveria existir duas pastas iguais.
    }

    return [Math]::Sign($A.DevNumber - $B.DevNumber)
}

<#
.SYNOPSIS
    Varre dist\<channel>\ e classifica cada pasta encontrada: válida/coerente, ou com um
    problema específico (nome inválido, build incompleta, divergência de manifesto).
.DESCRIPTION
    Diferencia três coisas que NUNCA devem ser tratadas como equivalentes (pedido do usuário):
      - versão da PASTA (o nome do diretório em dist\<channel>\);
      - versão do MANIFESTO (release-manifest.json .version/.channel dentro da pasta);
      - versão do ARTEFATO (extraída do nome do instalador .exe listado no manifesto/arquivo).
    Uma pasta só entra na lista "Valid" (candidata a ser considerada a mais recente) quando as
    três coincidem E o arquivo do instalador de fato existe em disco. Qualquer divergência vira
    uma entrada em "Divergent" (nunca descartada silenciosamente, nunca promovida à lista Valid).
#>
function Get-PxToolBoxDistVersions {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,

        [Parameter(Mandatory = $true)]
        [ValidateSet("Dev", "Stable", IgnoreCase = $true)]
        [string]$Channel
    )

    $channelLower = $Channel.ToLowerInvariant()
    $channelNormalized = (Get-Culture).TextInfo.ToTitleCase($channelLower)
    $channelDir = Join-Path $RepoRoot "dist\$channelLower"

    $result = [ordered]@{
        Channel          = $channelNormalized
        Valid            = @()
        InvalidFolderName = @()
        IncompleteBuild  = @()
        Divergent        = @()
    }

    if (-not (Test-Path -LiteralPath $channelDir)) {
        return [PSCustomObject]$result
    }

    $folders = Get-ChildItem -LiteralPath $channelDir -Directory -ErrorAction SilentlyContinue
    foreach ($folder in $folders) {
        $folderVersion = ConvertFrom-PxVersionString -Version $folder.Name
        if ($null -eq $folderVersion) {
            $result.InvalidFolderName += [PSCustomObject]@{
                Path   = $folder.FullName
                Reason = "Nome de pasta fora do formato X.Y.Z ou X.Y.Z-dev.N: '$($folder.Name)'."
            }
            continue
        }

        $manifestPath = Join-Path $folder.FullName "release-manifest.json"
        if (-not (Test-Path -LiteralPath $manifestPath)) {
            $result.IncompleteBuild += [PSCustomObject]@{
                Path   = $folder.FullName
                Reason = "Pasta com nome de versão válido ($($folder.Name)), mas sem release-manifest.json — build incompleta ou interrompida."
            }
            continue
        }

        $manifest = $null
        try {
            $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        } catch {
            $result.Divergent += [PSCustomObject]@{
                Path   = $folder.FullName
                Reason = "release-manifest.json não pôde ser lido/interpretado como JSON: $($_.Exception.Message)"
            }
            continue
        }

        if ($manifest.version -ne $folder.Name) {
            $result.Divergent += [PSCustomObject]@{
                Path   = $folder.FullName
                Reason = "Nome da pasta ('$($folder.Name)') não coincide com manifest.version ('$($manifest.version)')."
            }
            continue
        }

        if ($manifest.channel -ne $channelNormalized) {
            $result.Divergent += [PSCustomObject]@{
                Path   = $folder.FullName
                Reason = "manifest.channel ('$($manifest.channel)') não coincide com o canal esperado ('$channelNormalized')."
            }
            continue
        }

        $artifactProblem = $null
        foreach ($fileEntry in @($manifest.files)) {
            if ($null -eq $fileEntry -or [string]::IsNullOrWhiteSpace($fileEntry.name)) { continue }

            $artifactPath = Join-Path $folder.FullName "installer\$($fileEntry.name)"
            if (-not (Test-Path -LiteralPath $artifactPath)) {
                $artifactProblem = "Arquivo listado no manifesto não foi encontrado em disco: $($fileEntry.name)."
                break
            }

            # Versão embutida no NOME do artefato (ex.: PXToolBox-DEV-Setup-0.1.0-dev.28.exe)
            # precisa conter exatamente a versão da pasta/manifesto — nunca só "parecida".
            if ($fileEntry.name -notmatch [regex]::Escape($folder.Name)) {
                $artifactProblem = "Nome do artefato ('$($fileEntry.name)') não contém a versão esperada ('$($folder.Name)')."
                break
            }
        }

        if ($artifactProblem) {
            $result.Divergent += [PSCustomObject]@{
                Path   = $folder.FullName
                Reason = $artifactProblem
            }
            continue
        }

        $result.Valid += [PSCustomObject]@{
            Path    = $folder.FullName
            Version = $folderVersion
        }
    }

    # Sort-Object não entende o comparador custom diretamente (ele compararia Version como
    # objeto, não numericamente) — ordena explicitamente via Compare-PxVersion abaixo, para
    # garantir ordenação NUMÉRICA (nunca textual) independente de como o PowerShell compararia
    # PSCustomObject por padrão. Lista pequena (pastas de build) — um bubble sort simples é
    # suficiente e deixa a lógica de comparação óbvia de auditar.
    $result.Valid = [System.Collections.Generic.List[object]]::new([object[]]$result.Valid)
    for ($i = 0; $i -lt $result.Valid.Count; $i++) {
        for ($j = 0; $j -lt $result.Valid.Count - 1 - $i; $j++) {
            if ((Compare-PxVersion -A $result.Valid[$j].Version -B $result.Valid[$j + 1].Version) -gt 0) {
                $tmp = $result.Valid[$j]
                $result.Valid[$j] = $result.Valid[$j + 1]
                $result.Valid[$j + 1] = $tmp
            }
        }
    }

    return [PSCustomObject]$result
}

<#
.SYNOPSIS
    Resolve a versão mais recente e coerente de um canal, ou lança um erro claro em caso de
    ambiguidade — NUNCA escolhe silenciosamente uma versão antiga/inconsistente.
.DESCRIPTION
    "Ambiguidade", aqui, é qualquer situação em que o conjunto de pastas válidas está vazio
    (nenhuma versão coerente encontrada) — quem chama decide o que fazer nesse caso (não é
    necessariamente um erro: um canal/repositório recém-criado não tem build nenhuma ainda).
    Divergências e builds incompletas NUNCA impedem a resolução da versão mais recente entre as
    pastas que SÃO coerentes — elas só são relatadas em $Report.Divergent/IncompleteBuild para
    quem chama decidir se quer agir (ex.: avisar o operador).
#>
function Resolve-PxToolBoxLatestVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,

        [Parameter(Mandatory = $true)]
        [ValidateSet("Dev", "Stable", IgnoreCase = $true)]
        [string]$Channel
    )

    $report = Get-PxToolBoxDistVersions -RepoRoot $RepoRoot -Channel $Channel

    if ($report.Valid.Count -eq 0) {
        return [PSCustomObject]@{
            Report  = $report
            Latest  = $null
        }
    }

    return [PSCustomObject]@{
        Report = $report
        Latest = $report.Valid[$report.Valid.Count - 1]
    }
}

<#
.SYNOPSIS
    Decide se é seguro gerar/sobrescrever a saída de uma versão — mesma regra usada por
    build-release.ps1 ("Proteção dos artefatos"), extraída para aqui para ser testável com
    diretórios temporários, sem precisar rodar uma build .NET real.
.DESCRIPTION
    - Pasta inexistente ou vazia: sempre pode escrever.
    - Pasta existente com arquivos, sem -Force: lança (nunca sobrescreve em silêncio).
    - Pasta existente com arquivos, com -Force: remove e autoriza recriar — SÓ esta pasta exata
      (VersionDist), nunca outra (quem chama é responsável por nunca passar um caminho fora de
      dist\<channel>\<version>; ver Assert-UnderDirectory em build-release.ps1).
    Retorna $true quando a pasta foi removida e precisa ser recriada por quem chama; $false
    quando não havia nada para remover.
#>
function Assert-PxToolBoxVersionOutputWritable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VersionDist,

        [Parameter(Mandatory = $true)]
        [string]$ChannelNormalized,

        [Parameter(Mandatory = $true)]
        [string]$Version,

        [switch]$Force
    )

    if (-not (Test-Path -LiteralPath $VersionDist)) {
        return $false
    }

    # @(...) força um array mesmo quando não há nenhum arquivo (Get-ChildItem retorna $null
    # nesse caso, não um array vazio) — sem isso, .Count lança sob Set-StrictMode.
    $existingFiles = @(Get-ChildItem -LiteralPath $VersionDist -Recurse -File -Force -ErrorAction SilentlyContinue)
    if ($existingFiles.Count -eq 0) {
        return $false
    }

    if (-not $Force) {
        throw "Já existe uma build para $ChannelNormalized $Version em '$VersionDist'. Use -Force para refazer SÓ esta versão/canal."
    }

    Remove-Item -LiteralPath $VersionDist -Recurse -Force
    return $true
}

<#
.SYNOPSIS
    Mensagem de aviso (nunca um bloqueio) quando a versão sendo gerada é mais antiga que a mais
    recente já existente no canal — para que ninguém trate por engano uma versão antiga como se
    fosse a mais nova. Retorna $null quando não há nada a avisar.
.DESCRIPTION
    Nunca impede a geração: "preservar a possibilidade de reproduzir versões antigas por um
    fluxo explícito" (pedido do usuário) — só garante que isso nunca aconteça sem o operador
    perceber.
#>
function Get-PxToolBoxOlderVersionWarning {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,

        [Parameter(Mandatory = $true)]
        [ValidateSet("Dev", "Stable", IgnoreCase = $true)]
        [string]$Channel,

        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    $target = ConvertFrom-PxVersionString -Version $Version
    if ($null -eq $target) {
        return $null # Formato inválido é responsabilidade de quem chama validar antes.
    }

    $resolved = Resolve-PxToolBoxLatestVersion -RepoRoot $RepoRoot -Channel $Channel
    if ($null -eq $resolved.Latest) {
        return $null
    }

    if ((Compare-PxVersion -A $target -B $resolved.Latest.Version) -lt 0) {
        return "Atenção: $Version é uma versão NUMERICAMENTE MAIS ANTIGA que a mais recente já existente neste canal ($($resolved.Latest.Version.Original), em '$($resolved.Latest.Path)'). Isto não torna $Version a versão mais recente — se a intenção é reproduzir uma versão antiga, confirme que é isso mesmo antes de continuar."
    }

    return $null
}

Export-ModuleMember -Function `
    ConvertFrom-PxVersionString, `
    Compare-PxVersion, `
    Get-PxToolBoxDistVersions, `
    Resolve-PxToolBoxLatestVersion, `
    Assert-PxToolBoxVersionOutputWritable, `
    Get-PxToolBoxOlderVersionWarning
