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
function InitializeSetup(): Boolean;
begin
  Result := True;
  if MsgBox('Feche o CorelDRAW antes de continuar (o add-on DEV não pode ser atualizado ' +
            'enquanto o CorelDRAW estiver em execução). Clique OK para confirmar que o ' +
            'CorelDRAW está fechado, ou Cancelar para interromper a instalação.',
            mbConfirmation, MB_OKCANCEL) = IDCANCEL then
    Result := False;
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  if MsgBox('Feche o CorelDRAW antes de continuar a remoção do PX Tool Box DEV.',
            mbConfirmation, MB_OKCANCEL) = IDCANCEL then
    Result := False;
end;
