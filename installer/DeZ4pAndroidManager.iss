; ═══════════════════════════════════════════════════════════════
; DeZ4p Android Manager — Inno Setup Script
; © DeZ4p | t.me/DeZ4p | All Rights Reserved
; ═══════════════════════════════════════════════════════════════

#define AppName        "DeZ4p Android Manager"
#define AppShortName   "DeZ4p"
#define AppVersion     "1.0.0"
#define AppPublisher   "DeZ4p"
#define AppURL         "https://t.me/DeZ4p"
#define AppExeName     "DeZ4pAndroidManager.exe"
#define SourceDir      "..\artifacts\portable-win-x64"

[Setup]
AppId={{5A4D3B2C-1E6F-4A8B-9C7D-3E2F1A0B9C8D}
SetupIconFile=..\src\DeZ4pAndroidManager\app.ico
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
AppCopyright=Copyright (C) 2024-2026 DeZ4p

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableReadyPage=no
DisableDirPage=no
AllowNoIcons=yes

LicenseFile=..\LICENSE
InfoBeforeFile=installer-readme.txt

OutputDir=Output
OutputBaseFilename=DeZ4pAndroidManager-Setup-x64-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes

WizardStyle=modern
WizardSizePercent=110

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoCopyright=© DeZ4p | t.me/DeZ4p
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";   Description: "{cm:CreateDesktopIcon}";   GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenu";     Description: "Create a Start Menu shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce

[Files]
; Main executable (single-file self-contained)
Source: "{#SourceDir}\DeZ4pAndroidManager.exe"; DestDir: "{app}"; Flags: ignoreversion

; Tools folder — adb, fastboot, scrcpy (bundled)
Source: "{#SourceDir}\tools\*"; DestDir: "{app}\tools"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu
Name: "{group}\{#AppName}";              Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: startmenu
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"; Tasks: startmenu

; Desktop (optional)
Name: "{autodesktop}\{#AppName}";        Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\tools"
Type: filesandordirs; Name: "{app}\logs"
Type: dirifempty;     Name: "{app}"

[Registry]
; File association for .apks (Split APK package) — optional context menu
Root: HKCR; Subkey: "DeZ4pAndroidManager.apks"; ValueType: string; ValueName: ""; ValueData: "APKS Package"; Flags: uninsdeletekey
Root: HKCR; Subkey: "DeZ4pAndroidManager.apks\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"