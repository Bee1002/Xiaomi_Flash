; Inno Setup script — Xiaomi Flash (self-contained x64)
; Requiere: publish\self-contained-x64\ generado con Build-Production.ps1
; Compilar: ISCC.exe tools\XiaomiFlash.iss

#define MyAppName "Xiaomi Flash"
#define MyAppVersion "2.0.1"
#define MyAppPublisher "Xiaomi Flash"
#define MyAppExeName "Xiaomi_Flash.exe"
#define PublishDir "..\publish\self-contained-x64"
#define OutputDir "..\publish\installer"

[Setup]
AppId={{A7B3C9E1-4F2D-4A8B-9C1E-010203040501}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\{#MyAppName}
DefaultGroupName={#MyAppName}
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
VersionInfoDescription={#MyAppName} — fastboot flasher for Xiaomi devices
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Messages]
english.BeveledLabel=Fastboot flasher for Xiaomi devices. Requires unlocked bootloader and USB drivers.
spanish.BeveledLabel=Flasher fastboot para Xiaomi. Requiere bootloader desbloqueado y drivers USB.
