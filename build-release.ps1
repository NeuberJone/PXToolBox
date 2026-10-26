<#
.SYNOPSIS
    Ponto de entrada do Botão 1 (Gerar build DEV) e Botão 3 (Refazer build DEV, com -Force) do
    Stream Deck. Delega para scripts\Build-Release.ps1. Ver docs/stream-deck-buttons.md.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$Channel,

    [string]$ReleaseBaseUrl,

    [switch]$Force
)

& "$PSScriptRoot\scripts\Build-Release.ps1" -Version $Version -Channel $Channel -ReleaseBaseUrl $ReleaseBaseUrl -Force:$Force
exit $LASTEXITCODE
