<#
.SYNOPSIS
    Ponto de entrada do Botão 6 (Criar pré-release no GitHub) do Stream Deck. Delega para
    scripts\Create-GitHubRelease.ps1. Ver docs/stream-deck-buttons.md.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$Channel,

    [switch]$Create
)

& "$PSScriptRoot\scripts\Create-GitHubRelease.ps1" -Version $Version -Channel $Channel -Create:$Create
exit $LASTEXITCODE
