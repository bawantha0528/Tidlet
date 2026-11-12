[Setup]
AppId=TidletMonitor
AppName=Tidlet Monitor
AppVersion=0.1.0
AppPublisher=Tidlet Monitor contributors
VersionInfoDescription=Tidlet Monitor Setup
DefaultDirName={commonpf}\Tidlet Monitor
PrivilegesRequired=admin
DisableDirPage=no
DisableWelcomePage=no
WizardStyle=modern
SetupIconFile=src\IsletMonitor\Tidlet.ico
UninstallDisplayIcon={app}\IsletMonitor.exe
OutputDir=artifacts\installer
OutputBaseFilename=TidletSetup

[Files]
Source: "artifacts\publish\win-x86\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs
Source: "LICENSE"; DestDir: "{app}"

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[Icons]
Name: "{commonprograms}\Tidlet Monitor"; Filename: "{app}\IsletMonitor.exe"; WorkingDir: "{app}"
Name: "{commondesktop}\Tidlet Monitor"; Filename: "{app}\IsletMonitor.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\IsletMonitor.exe"; Description: "Launch Tidlet Monitor"; WorkingDir: "{app}"; Flags: postinstall runasoriginaluser nowait skipifsilent