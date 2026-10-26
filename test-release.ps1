<#
.SYNOPSIS
    Ponto de entrada do Botão 4 (Validar release DEV) do Stream Deck. Delega para
    scripts\Test-Release.ps1. Ver docs/stream-deck-buttons.md.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$Channel
)

& "$PSScriptRoot\scripts\Test-Release.ps1" -Version $Version -Channel $Channel
exit $LASTEXITCODE
