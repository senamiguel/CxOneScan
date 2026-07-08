[Setup]
AppName=CxOneScan
AppVersion=2.1.0
AppPublisher=CxOneScan Contributors
DefaultDirName={userpf}\CxOneScan
DefaultGroupName=CxOneScan
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=CxOneScanSetup
SetupIconFile=..\app_logo.ico
UninstallDisplayIcon={app}\CxOneScan.exe
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\bin\Release\net8.0-windows\win-x64\publish\CxOneScan.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\CxOneScan"; Filename: "{app}\CxOneScan.exe"
Name: "{userdesktop}\CxOneScan"; Filename: "{app}\CxOneScan.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\CxOneScan.exe"; Description: "{cm:LaunchProgram,CxOneScan}"; Flags: nowait postinstall skipifsilent
