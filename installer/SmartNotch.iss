; Inno Setup script - SmartNotch
; Cara pakai: install Inno Setup (https://jrsoftware.org/isdl.php), lalu jalankan build-installer.ps1

#define AppName "SmartNotch"
#define AppVersion "1.1.0"
#define AppExe "SmartNotch.exe"

[Setup]
AppId={{B7C1E2A4-5D3F-4E8A-9A21-6C0F5E7D4B13}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=SmartNotch
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=SmartNotch-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "Jalankan SmartNotch otomatis saat Windows menyala"; GroupDescription: "Opsi tambahan:"
Name: "desktopicon"; Description: "Buat shortcut di Desktop"; GroupDescription: "Opsi tambahan:"; Flags: unchecked

[Files]
Source: "..\dist\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SmartNotch"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "Jalankan {#AppName} sekarang"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#AppExe}"; Flags: runhidden; RunOnceId: "KillSmartNotch"
