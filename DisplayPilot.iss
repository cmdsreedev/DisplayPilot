#define MyAppName "DisplayPilot"
#ifndef MyAppVersion
  #define MyAppVersion "1.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "publish"
#endif
[Setup]
AppId={{4FC897AE-BB16-4739-B0CD-FCC31A10A847}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\DisplayPilot
DefaultGroupName=DisplayPilot
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=installer
OutputBaseFilename=DisplayPilot-Setup
SetupIconFile=Assets\DisplayPilot.ico
UninstallDisplayIcon={app}\DisplayPilot.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "Start DisplayPilot when I sign in to Windows"; Flags: unchecked
[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "smoke-*.png"
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\DisplayPilot"; Filename: "{app}\DisplayPilot.exe"
Name: "{autodesktop}\DisplayPilot"; Filename: "{app}\DisplayPilot.exe"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "DisplayPilot"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DisplayPilot"; ValueData: """{app}\DisplayPilot.exe"" --minimized"; Tasks: startup; Flags: uninsdeletevalue
[Run]
Filename: "{app}\DisplayPilot.exe"; Description: "Launch DisplayPilot"; Flags: nowait postinstall skipifsilent
