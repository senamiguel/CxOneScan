[Setup]
AppId=CxOneScan
AppName=CxOneScan
AppVersion=2.2.0
AppVerName=CxOneScan v2.2.0
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

[Messages]
WelcomeLabel2=This will install [Name] on your computer.%n%nVersion [AppVersion]%n%nClick Next to continue, or Cancel to exit Setup.
FinishedLabel=Setup has finished installing [Name] (version [AppVersion]) on your computer.%n%nThe application can be launched from the Start Menu or Desktop shortcut.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\bin\Release\net8.0-windows\win-x64\publish\CxOneScan.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\CxOneScan v2.2.0"; Filename: "{app}\CxOneScan.exe"
Name: "{userdesktop}\CxOneScan v2.2.0"; Filename: "{app}\CxOneScan.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\CxOneScan.exe"; Description: "{cm:LaunchProgram,CxOneScan}"; Flags: nowait postinstall skipifsilent
