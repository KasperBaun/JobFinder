; Inno Setup script for the jobfinder Windows installer.
; Built in CI by .github/workflows/release.yml, which passes the version via:
;   ISCC /DAppVersion=0.1.<run> src\installer\jobfinder.iss
; It packages the self-contained publish output at ..\..\publish\win-x64 into a single Setup.exe.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "Jobfinder"
#define AppExe "Jobmatch.Host.exe"

[Setup]
AppId={{5F5C7C2B-9E4A-4E2E-9B7E-6E1D3B0A9C10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Kasper Baun
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=jobfinder-setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "..\..\publish\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
