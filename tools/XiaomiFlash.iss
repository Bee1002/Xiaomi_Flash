; Inno Setup — Xiaomi Flash by Xploit (self-contained x64)
; Requiere: publish\self-contained-x64\ generado con Build-Production.ps1
; Compilar: ISCC.exe tools\XiaomiFlash.iss
;   o: powershell -ExecutionPolicy Bypass -File tools\Build-Production.ps1 -Obfuscate -Zip -Installer

#define MyAppVersion "2.0.1"
#define MyAppName "Xiaomi Flash"
#define MyAppNameFull "Xiaomi Flash v" + MyAppVersion + " By Xploit"
#define MyAppPublisher "Xploit"
#define MyAppExeName "Xiaomi_Flash.exe"
#define PublishDir "..\publish\self-contained-x64"
#define OutputDir "..\publish\installer"

[Setup]
AppId={{A7B3C9E1-4F2D-4A8B-9C1E-010203040501}
AppName={#MyAppNameFull}
AppVersion={#MyAppVersion}
AppVerName={#MyAppNameFull}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\{#MyAppName}
DefaultGroupName={#MyAppName} By Xploit
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=Xiaomi_Flash_{#MyAppVersion}_Setup_x64
SetupIconFile=..\Assets\icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion=2.0.1.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppNameFull} — flasher fastboot universal Xiaomi
VersionInfoProductName={#MyAppNameFull}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName} By Xploit"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName} By Xploit"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName + ' By Xploit', '&', '&&')}}"; Flags: nowait postinstall skipifsilent unchecked

[Messages]
english.BeveledLabel=
spanish.BeveledLabel=
spanish.WelcomeLabel1=Bienvenido al instalador de {#MyAppName} By Xploit
spanish.WelcomeLabel2=Este asistente instalará {#MyAppNameFull} en tu equipo.%n%nEl flash de firmware puede borrar datos o dejar el dispositivo inutilizable si se usa una ROM incorrecta. Úsalo solo si sabes lo que haces.
spanish.FinishedLabel=La instalación ha finalizado. Pulsa Finalizar para cerrar el asistente.
spanish.FinishedLabelNoIcons=La instalación ha finalizado. Pulsa Finalizar para cerrar el asistente.
spanish.ClickFinish=Finalizar
