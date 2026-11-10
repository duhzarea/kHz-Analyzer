; kHz Analyzer - setup program (Inno Setup 6).
; Built by tools\build-release.ps1 after the app has been published to dist\kHzAnalyzer.
; The destination page lets the user pick any folder on any drive.

#define AppName "kHz Analyzer"
#ifndef AppVersion
  #define AppVersion "0.9.0"
#endif
#define AppExe "kHzAnalyzer.exe"

[Setup]
AppId={{6F0B6C0E-7C55-4B7B-9E0D-3B1B6C6F0A19}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=DUHZZ
DefaultDirName={autopf}\{#AppName}
; Always show the folder page; the user can browse or type a path on another drive.
DisableDirPage=no
UsePreviousAppDir=yes
DisableProgramGroupPage=yes
; Per-user by default (no administrator prompt); the user may switch to "all users".
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\dist
OutputBaseFilename=kHzAnalyzer-{#AppVersion}-setup
SetupIconFile=..\KhzAnalyzer\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "..\dist\kHzAnalyzer\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Open {#AppName}"; Flags: nowait postinstall skipifsilent
