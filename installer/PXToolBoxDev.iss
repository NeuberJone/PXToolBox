; PX Tool Box DEV — instalador do add-on DEV para CorelDRAW 2026 (v27).
;
; Importante:
;  - Instala em C:\Program Files\Corel\CorelDRAW Graphics Suite\27\Programs64\Addons\PXToolBoxDev
;    (pasta EXCLUSIVA do DEV — nunca a mesma pasta/identidade de uma futura distribuição Prod).
;  - Requer privilégios de administrador (grava em Program Files).
;  - O CorelDRAW precisa estar FECHADO durante a instalação/remoção, porque o host mantém o
;    arquivo PXToolBoxDev.dll aberto enquanto está em execução.
;  - MyAppVersion é passado pelo build-release.ps1 via /DMyAppVersion=X.Y.Z-dev.N;
;    MyPublishDir é passado via /DMyPublishDir=<caminho absoluto da publicação>.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev.0"
#endif
#ifndef MyPublishDir
  #define MyPublishDir "..\host\PXToolBox.CorelHost.Dev\bin\Release\net48"
#endif

#define MyAppName "PX Tool Box DEV"
#define MyAddonFolderName "PXToolBoxDev"
#define MyCorelVersionSegment "27"

[Setup]
AppId={{2B9B9C7B-9A5D-4C53-9E34-2C2F6A5E8F2A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=PX Tool Box
DefaultDirName=C:\Program Files\Corel\CorelDRAW Graphics Suite\{#MyCorelVersionSegment}\Programs64\Addons\{#MyAddonFolderName}
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=no
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
OutputBaseFilename=PXToolBox-DEV-Setup-{#MyAppVersion}
OutputDir=.
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName} {#MyAppVersion}
InfoBeforeFile=InstallInfo.txt

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
; Todo o conteúdo publicado do host (PXToolBoxDev.dll + dependências gerenciadas +
; CorelDrw.addon + AppUI.xslt + UserUI.xslt). Corel.Interop.*.dll NÃO estão aqui porque o
; projeto as referencia com Private=false (pertencem à instalação do CorelDRAW, não a este
; instalador — ver docs/ARCHITECTURE.md, "Pré-requisitos externos").
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Code]
// Mesmo padrão do instalador do MoldMotor (C:\Projetos\MoldMotor\installer\MoldMotor.iss):
// detecta de verdade se o CorelDRAW está em execução (via WMI), em vez de só perguntar e
// confiar na resposta do usuário. Se estiver aberto, pede para fechar e RE-VERIFICA de novo
// antes de continuar — em loop, até o processo não existir mais ou o usuário cancelar. Nunca
// encerra o CorelDRAW sozinho (para não descartar trabalho não salvo do usuário).
function IsProcessRunning(const ExeName: String): Boolean;
var
  Locator, Service, Items: Variant;
begin
  Result := False;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    Items := Service.ExecQuery('SELECT Name FROM Win32_Process WHERE Name = "' + ExeName + '"');
    Result := Items.Count > 0;
  except
    // Sem WMI não há como confirmar; segue o fluxo e o próprio Windows avisará caso o
    // PXToolBoxDev.dll esteja em uso (arquivo bloqueado) ao tentar copiar/remover.
    Result := False;
  end;
end;

function EnsureCorelClosed(const Action: String): Boolean;
begin
  Result := True;
  while IsProcessRunning('CorelDRW.exe') do
  begin
    if MsgBox('O CorelDRAW está aberto.' + #13#10#13#10 +
              'Feche o CorelDRAW normalmente, salvando os documentos que precisar, e clique ' +
              'em Repetir para continuar a ' + Action + ' do PX Tool Box DEV.' + #13#10 +
              'O instalador não encerra o CorelDRAW para não descartar trabalho não salvo.',
              mbError, MB_RETRYCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

// Chamado pelo Inno Setup logo antes de copiar os arquivos de verdade (depois de todas as
// telas do assistente) — é aqui que a verificação precisa ser a real, não um MsgBox de
// confirmação cedo na instalação que poderia ficar desatualizado até a cópia de fato ocorrer.
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not EnsureCorelClosed('instalação') then
    Result := 'A instalação foi cancelada porque o CorelDRAW continua aberto.';
end;

function InitializeUninstall(): Boolean;
begin
  Result := EnsureCorelClosed('remoção');
end;
