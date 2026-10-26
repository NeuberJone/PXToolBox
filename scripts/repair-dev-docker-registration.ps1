<#
.SYNOPSIS
    Corrige, em workspaces do CorelDRAW já inicializados nesta máquina, o registro do docker
    do PX Tool Box DEV: remove o item antigo (bug) e insere o item correto que falta.

.DESCRIPTION
    Branch fix/dev-docker-registration — evidência coletada nesta máquina (ver
    docs/ARCHITECTURE.md, seção 3, e docs/mvp-smoke-test.md):

    1) A versão anterior do empacotamento registrava o controle WPF do PX Tool Box DEV
       diretamente dentro de `uiConfig/commandBars`, em vez de um docker nativo. Isso deixou
       persistido, dentro do arquivo .cdws de CADA workspace já aberto com a build antiga, um
       item solto na barra de comandos padrão (`commandBarData guid="c2b44f69-..."`):

           <item guidRef="c7124037-25c8-4d03-80b2-97b716ed95ef"/>

       Esse item sozinho na barra é o que renderiza o painel comprimido ao lado do SISBolt.

    2) O pacote corrigido (AppUI.xslt/UserUI.xslt) registra o item certo — um `checkButton` no
       menu "Janela > Janelas de encaixe" (`commandBarData guid="3eaa9bbe-..."`). Mas,
       confirmado por leitura direta dos arquivos .cdws reais desta máquina, o CorelDRAW só
       mescla UserUI.xslt num workspace quando ele é CRIADO — não ao reabrir o app, e não
       quando um add-on já instalado é apenas atualizado. Por isso, mesmo depois de reinstalar
       a build corrigida e reiniciar o CorelDRAW, nenhum workspace já existente ganha o item
       novo automaticamente (confirmado: comparando com os itens equivalentes do MoldMotor e do
       eCutDocker, que JÁ estavam nesse mesmo menu antes do PX DEV existir).

    Este script corrige os dois pontos em cada .cdws já inicializado nesta máquina, e
    **somente** eles:
    - Remove o item antigo (c7124037) onde ele está solto num `toolbar`/`menu` (nunca onde já
      está corretamente dentro de um `dockerData`).
    - Insere o item novo (`checkButton` do PX DEV) no menu "Janelas de encaixe", só se ainda
      não estiver lá.
    Nada mais é tocado: barras personalizadas do usuário, itens de outros add-ins (SISBolt,
    MoldMotor, eCutDocker, etc.) e qualquer outro conteúdo do workspace permanecem intactos.
    Cada .cdws alterado é copiado para um .bak com timestamp antes da gravação.

.PARAMETER WorkspaceDir
    Pasta com os arquivos .cdws do usuário. Padrão: a pasta do CorelDRAW 2026 (v27) nesta
    máquina, `%APPDATA%\Corel\CorelDRAW Graphics Suite 2026\Draw\Workspace`.

.PARAMETER StaleGuid
    GUID do conteúdo WPF do PX Tool Box DEV a remover de onde está errado. Padrão: o GUID real
    usado por esta build (`c7124037-25c8-4d03-80b2-97b716ed95ef`).

.PARAMETER ToggleGuid
    GUID do `checkButton` (botão de alternância do docker) a garantir presente no menu
    "Janelas de encaixe". Padrão: o GUID real usado por esta build
    (`0dd0d869-201a-40a9-b3d0-801f9138f700`).

.PARAMETER DockersMenuGuid
    GUID fixo do CorelDRAW para o menu "Janela > Janelas de encaixe". Padrão:
    `3eaa9bbe-28fd-4672-9128-02974ee96332` (confirmado idêntico no MoldMotor e no eCutDocker).

.PARAMETER WhatIf
    Só relata o que seria alterado, sem gravar nada.
#>
param(
    [string]$WorkspaceDir = (Join-Path $env:APPDATA "Corel\CorelDRAW Graphics Suite 2026\Draw\Workspace"),

    [string]$StaleGuid = "c7124037-25c8-4d03-80b2-97b716ed95ef",

    [string]$ToggleGuid = "0dd0d869-201a-40a9-b3d0-801f9138f700",

    [string]$DockersMenuGuid = "3eaa9bbe-28fd-4672-9128-02974ee96332",

    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

if (-not (Test-Path -LiteralPath $WorkspaceDir)) {
    throw "Pasta de workspace não encontrada: $WorkspaceDir. Informe -WorkspaceDir explicitamente."
}

$corelRunning = Get-Process -Name "CorelDrw" -ErrorAction SilentlyContinue
if ($corelRunning) {
    throw "CorelDRAW está em execução. Feche-o antes de rodar este script (os arquivos .cdws do workspace ativo são gravados por ele ao sair, e sobrescreveriam a correção)."
}

$cdwsFiles = Get-ChildItem -LiteralPath $WorkspaceDir -Filter "*.cdws" -File
if ($cdwsFiles.Count -eq 0) {
    Write-Host "Nenhum arquivo .cdws encontrado em '$WorkspaceDir'. Nada para fazer."
    exit 0
}

$repaired = @()
$untouched = @()

foreach ($file in $cdwsFiles) {
    Write-Step "Verificando '$($file.Name)'"

    $zip = [System.IO.Compression.ZipFile]::Open($file.FullName, [System.IO.Compression.ZipArchiveMode]::Read)
    try {
        $entry = $zip.GetEntry("content/workspace.xml")
        if (-not $entry) {
            Write-Host "  (sem content/workspace.xml — formato inesperado, ignorando)" -ForegroundColor Yellow
            $untouched += $file.Name
            continue
        }
        $stream = $entry.Open()
        $reader = New-Object System.IO.StreamReader($stream)
        $xmlText = $reader.ReadToEnd()
        $reader.Close()
    }
    finally {
        $zip.Dispose()
    }

    [xml]$xml = $xmlText

    # 1) Item antigo (bug): só remove onde está solto num toolbar/menu — nunca onde já está
    #    corretamente dentro de um dockerData (workspace já corrigido, ou GUID reaproveitado de
    #    propósito em outro lugar válido).
    $staleNodes = $xml.SelectNodes("//toolbar/item[@guidRef='$StaleGuid'] | //menu/item[@guidRef='$StaleGuid']")

    # 2) Item novo (faltando): o checkButton do PX DEV no menu "Janelas de encaixe".
    $dockersMenu = $xml.SelectSingleNode("//commandBarData[@guid='$DockersMenuGuid']/menu")
    $toggleAlreadyPresent = $false
    if ($dockersMenu) {
        $existing = $dockersMenu.SelectSingleNode("item[@guidRef='$ToggleGuid']")
        $toggleAlreadyPresent = ($null -ne $existing)
    }

    $needsRemoval = $staleNodes.Count -gt 0
    $needsInsertion = ($null -ne $dockersMenu) -and (-not $toggleAlreadyPresent)

    if (-not $dockersMenu) {
        Write-Host "  Aviso: menu 'Janelas de encaixe' (guid $DockersMenuGuid) não encontrado neste workspace — item novo não pode ser inserido aqui." -ForegroundColor Yellow
    }

    if (-not $needsRemoval -and -not $needsInsertion) {
        Write-Host "  Registro do PX Tool Box DEV já está correto. Nada a fazer." -ForegroundColor Gray
        $untouched += $file.Name
        continue
    }

    if ($needsRemoval) {
        Write-Host "  Encontrado(s) $($staleNodes.Count) item(ns) incorreto(s) referenciando '$StaleGuid' (barra padrão)." -ForegroundColor Yellow
    }
    if ($needsInsertion) {
        Write-Host "  Item '$ToggleGuid' ausente do menu 'Janelas de encaixe' — será inserido." -ForegroundColor Yellow
    }

    if ($WhatIf) {
        Write-Host "  (-WhatIf) Nada gravado." -ForegroundColor Yellow
        $repaired += $file.Name
        continue
    }

    $backupPath = "$($file.FullName).bak-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Copy-Item -LiteralPath $file.FullName -Destination $backupPath -Force
    Write-Host "  Backup salvo em: $backupPath" -ForegroundColor Gray

    if ($needsRemoval) {
        foreach ($node in @($staleNodes)) {
            [void]$node.ParentNode.RemoveChild($node)
        }
    }

    if ($needsInsertion) {
        $newItem = $xml.CreateElement("item")
        $newItem.SetAttribute("guidRef", $ToggleGuid)
        [void]$dockersMenu.AppendChild($newItem)
    }

    $newXmlText = $xml.OuterXml

    $zip = [System.IO.Compression.ZipFile]::Open($file.FullName, [System.IO.Compression.ZipArchiveMode]::Update)
    try {
        $entry = $zip.GetEntry("content/workspace.xml")
        $entry.Delete()
        $newEntry = $zip.CreateEntry("content/workspace.xml")
        $writer = New-Object System.IO.StreamWriter($newEntry.Open())
        $writer.Write($newXmlText)
        $writer.Close()
    }
    finally {
        $zip.Dispose()
    }

    $summary = @()
    if ($needsRemoval) { $summary += "removido(s) $($staleNodes.Count) item(ns) antigo(s)" }
    if ($needsInsertion) { $summary += "inserido o item novo no menu 'Janelas de encaixe'" }
    Write-Host "  Corrigido ($($summary -join '; ')) em '$($file.Name)'." -ForegroundColor Green
    $repaired += $file.Name
}

Write-Host ""
if ($WhatIf) {
    Write-Host "Simulação concluída. Workspaces com registro incorreto: $($repaired.Count); sem alteração: $($untouched.Count)." -ForegroundColor Cyan
} else {
    Write-Host "Concluído. Workspaces corrigidos: $($repaired.Count); sem alteração necessária: $($untouched.Count)." -ForegroundColor Green
    if ($repaired.Count -gt 0) {
        Write-Host "Reabra o CorelDRAW e confira o docker 'PX Tool Box DEV' (ver docs/mvp-smoke-test.md)." -ForegroundColor Green
    }
}
