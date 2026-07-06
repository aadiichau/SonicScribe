; SonicScribe Windows installer (Inno Setup)
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName "SonicScribe"
#define AppPublisher "Aditya Chauhan"
#define AppPublisherShort "aadiichau"
#define AppURL "https://github.com/aadiichau/SonicScribe"
#define AppExeName "SonicScribe.exe"

[Setup]
AppId={{A4B8C2E1-7F3D-4A9B-9C1E-5D6F8A2B4C3E}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=SonicScribe installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoCopyright=Copyright (C) 2026 {#AppPublisherShort}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=..\releases
OutputBaseFilename=SonicScribe-Setup-v{#AppVersion}
SetupIconFile=..\LocalScribe\Assets\AppIcon.ico
UninstallDisplayIcon={app}\Assets\AppIcon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\SonicScribe\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\AppIcon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.%n%nIf Windows SmartScreen blocked the download, you already chose Run anyway — you're good.%n%nSonicScribe transcribes audio locally using Whisper. Python and faster-whisper are still required on first use — the app can install them for you.