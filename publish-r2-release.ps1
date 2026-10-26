<#
.SYNOPSIS
    Ponto de entrada do Botão 7 (Publicar release DEV no R2) do Stream Deck. Delega para
    scripts\Publish-R2Release.ps1. Ver docs/stream-deck-buttons.md.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$Channel,

    [switch]$Publish
)

& "$PSScriptRoot\scripts\Publish-R2Release.ps1" -Version $Version -Channel $Channel -Publish:$Publish
exit $LASTEXITCODE
