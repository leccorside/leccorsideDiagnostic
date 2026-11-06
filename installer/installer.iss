; Script do Instalador Inno Setup para Leccor Windows Health Center (Seção 62 do PROMPT)
#define MyAppName "Leccor Windows Health Center"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Leccor Software"
#define MyAppExeName "LeccorWindowsHealthCenter.exe"

[Setup]
AppId={{9F5B46F2-6782-4E9B-89E3-4E84B52DC319}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=LeccorWindowsHealthCenter_Setup_x64
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// A desinstalação preserva relatórios e backups do usuário em %ProgramData% conforme a Seção 62 do PROMPT
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Os dados em %ProgramData%\LeccorWindowsHealthCenter são preservados intencionalmente
    // para que o usuário não perca seus relatórios técnicos e pontos de rollback.
  end;
end;

