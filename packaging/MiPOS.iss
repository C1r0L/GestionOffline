#define AppVersion "0.1.0"
[Setup]
AppId={{A72FB117-41E8-41EB-8E9E-1068AF445837}
AppName=MiPOS
AppVersion={#AppVersion}
AppPublisher=MiPOS
DefaultDirName={localappdata}\Programs\MiPOS
DefaultGroupName=MiPOS
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\artifacts\installer
OutputBaseFilename=MiPOS-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\POS.Desktop.exe
[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; Flags: unchecked
[Files]
Source: "..\artifacts\windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\MiPOS"; Filename: "{app}\POS.Desktop.exe"
Name: "{userdesktop}\MiPOS"; Filename: "{app}\POS.Desktop.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\POS.Desktop.exe"; Description: "Abrir MiPOS"; Flags: nowait postinstall skipifsilent
